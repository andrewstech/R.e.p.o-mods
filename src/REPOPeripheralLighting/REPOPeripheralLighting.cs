using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace Andrew.REPO.PeripheralLighting
{
    [BepInPlugin("andrew.repo.peripheral-lighting", "R.E.P.O Peripheral Lighting", "0.1.0")]
    public sealed class PeripheralLightingPlugin : BaseUnityPlugin
    {
        private static PeripheralLightingPlugin Instance;
        private Harmony harmony;
        private LogitechLighting logitech;
        private LightingState state;
        private ConfigEntry<bool> modEnabled;
        private ConfigEntry<bool> perKeyKeyboard;
        private ConfigEntry<bool> healthKeys;
        private ConfigEntry<bool> movementKeys;
        private ConfigEntry<bool> mouseZones;
        private ConfigEntry<float> brightness;
        private ConfigEntry<float> smoothing;
        private ConfigEntry<float> lowHealthThreshold;
        private ConfigEntry<float> dangerDecaySeconds;
        private ConfigEntry<float> treasureFlashSeconds;
        private ConfigEntry<float> healFlashSeconds;
        private ConfigEntry<float> effectIntensity;
        private ConfigEntry<float> idleBreathSpeed;
        private ConfigEntry<bool> idleBreathing;
        private ConfigEntry<bool> dangerEffects;
        private ConfigEntry<bool> healEffects;
        private ConfigEntry<bool> treasureEffects;
        private ConfigEntry<string> idleColor;
        private ConfigEntry<string> dangerColor;
        private ConfigEntry<string> healColor;
        private ConfigEntry<string> treasureColor;
        private ConfigEntry<KeyboardShortcut> uiKey;
        private bool showUi;
        private bool lightingSuspended;
        private Rect uiRect = new Rect(30f, 30f, 390f, 560f);
        private Vector2 uiScroll;
        private Color current;

        private void Awake()
        {
            Instance = this;
            modEnabled = Config.Bind("General", "Enabled", true, "Enable peripheral lighting integration.");
            perKeyKeyboard = Config.Bind("Logitech", "PerKeyKeyboard", true, "Use per-key bitmap effects on supported Logitech keyboards.");
            healthKeys = Config.Bind("Effects", "HealthNumberKeys", true, "Show health as green/red number-row keys on per-key keyboards.");
            movementKeys = Config.Bind("Effects", "MovementKeys", true, "Highlight WASD and space on per-key keyboards.");
            mouseZones = Config.Bind("Logitech", "MouseZones", true, "Also set Logitech mouse target zones when available.");
            brightness = Config.Bind("General", "Brightness", 0.65f, new ConfigDescription("Global brightness multiplier.", new AcceptableValueRange<float>(0f, 1f)));
            smoothing = Config.Bind("General", "Smoothing", 10f, new ConfigDescription("Color smoothing speed.", new AcceptableValueRange<float>(0f, 40f)));
            lowHealthThreshold = Config.Bind("Effects", "LowHealthThreshold", 0.35f, new ConfigDescription("Health ratio where idle lighting blends toward danger.", new AcceptableValueRange<float>(0.05f, 0.9f)));
            dangerDecaySeconds = Config.Bind("Effects", "DangerFlashSeconds", 1.8f, new ConfigDescription("Seconds for damage/death flashes to fade.", new AcceptableValueRange<float>(0.1f, 10f)));
            treasureFlashSeconds = Config.Bind("Effects", "TreasureFlashSeconds", 1.1f, new ConfigDescription("Seconds for valuable/discovery flashes to fade.", new AcceptableValueRange<float>(0.1f, 10f)));
            healFlashSeconds = Config.Bind("Effects", "HealFlashSeconds", 1.2f, new ConfigDescription("Seconds for heal flashes to fade.", new AcceptableValueRange<float>(0.1f, 10f)));
            effectIntensity = Config.Bind("Effects", "Intensity", 1f, new ConfigDescription("Strength of event lighting effects.", new AcceptableValueRange<float>(0f, 1f)));
            idleBreathing = Config.Bind("Effects", "IdleBreathing", true, "Gently pulse idle lighting.");
            idleBreathSpeed = Config.Bind("Effects", "IdleBreathSpeed", 0.35f, new ConfigDescription("Idle breathing cycles per second.", new AcceptableValueRange<float>(0.05f, 2f)));
            dangerEffects = Config.Bind("Effects", "Danger", true, "Damage, death and low-health effects.");
            healEffects = Config.Bind("Effects", "Heal", true, "Healing effects.");
            treasureEffects = Config.Bind("Effects", "Treasure", true, "Valuable, shop and enemy-hit effects.");
            idleColor = Config.Bind("Colors", "Idle", "20,60,100", "RGB 0-255.");
            dangerColor = Config.Bind("Colors", "Danger", "255,0,0", "RGB 0-255.");
            healColor = Config.Bind("Colors", "Heal", "0,255,80", "RGB 0-255.");
            treasureColor = Config.Bind("Colors", "Treasure", "255,160,0", "RGB 0-255.");
            uiKey = Config.Bind("UI", "ToggleConfigUi", new KeyboardShortcut(KeyCode.F6), "Toggle the in-game peripheral lighting config UI.");

            state = new LightingState();
            current = ParseColor(idleColor.Value, new Color(0.2f, 0.6f, 1f));
            logitech = new LogitechLighting(Logger);
            logitech.Initialize();

            harmony = new Harmony("andrew.repo.peripheral-lighting");
            PatchIfExists("PlayerHealth", "Hurt", typeof(PlayerHealthHurtPatch));
            PatchIfExists("PlayerHealth", "Heal", typeof(PlayerHealthHealPatch));
            PatchIfExists("PlayerHealth", "Death", typeof(PlayerHealthDeathPatch));
            PatchIfExists("EnemyHealth", "Hurt", typeof(EnemyHealthHurtPatch));
            PatchIfExists("ValuableObject", "Discover", typeof(ValuableDiscoverPatch));
            PatchIfExists("ValuableObject", "DollarValueSetLogic", typeof(ValuableValuePatch));
            PatchIfExists("ShopManager", "ShopInitialize", typeof(ShopPatch));
            Logger.LogInfo("R.E.P.O Peripheral Lighting loaded. Logitech SDK active=" + logitech.Available);
        }

        private void PatchIfExists(string typeName, string methodName, Type patchType)
        {
            try
            {
                Type t = AccessTools.TypeByName(typeName);
                MethodInfo m = t == null ? null : AccessTools.Method(t, methodName);
                MethodInfo postfix = AccessTools.Method(patchType, "Postfix");
                if (m != null && postfix != null)
                {
                    harmony.Patch(m, postfix: new HarmonyMethod(postfix));
                    Logger.LogInfo("Patched " + typeName + "." + methodName);
                }
                else
                {
                    Logger.LogWarning("Patch target missing: " + typeName + "." + methodName);
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning("Patch failed for " + typeName + "." + methodName + ": " + ex.Message);
            }
        }

        private void Update()
        {
            if (uiKey.Value.IsDown()) showUi = !showUi;
            if (logitech == null || !logitech.Available) return;
            if (!modEnabled.Value)
            {
                if (!lightingSuspended)
                {
                    logitech.Suspend();
                    lightingSuspended = true;
                }
                return;
            }
            if (lightingSuspended) logitech.Resume();
            lightingSuspended = false;

            state.Tick(Time.unscaledDeltaTime, dangerDecaySeconds.Value, healFlashSeconds.Value, treasureFlashSeconds.Value);
            RefreshLocalPlayerHealth();

            Color idle = ParseColor(idleColor.Value, new Color(0.2f, 0.6f, 1f));
            Color danger = ParseColor(dangerColor.Value, Color.red);
            Color heal = ParseColor(healColor.Value, Color.green);
            Color treasure = ParseColor(treasureColor.Value, new Color(1f, 0.55f, 0f));

            Color target = idle;
            if (idleBreathing.Value)
                target *= 0.8f + 0.2f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * idleBreathSpeed.Value * 2f * Mathf.PI));
            if (dangerEffects.Value && state.HealthKnown && state.HealthMax > 0)
            {
                float ratio = Mathf.Clamp01((float)state.Health / state.HealthMax);
                if (ratio < lowHealthThreshold.Value)
                {
                    float t = 1f - Mathf.Clamp01(ratio / lowHealthThreshold.Value);
                    target = Color.Lerp(target, danger, t * 0.85f * effectIntensity.Value);
                }
            }
            if (treasureEffects.Value) target = Color.Lerp(target, treasure, state.TreasurePulse * effectIntensity.Value);
            if (healEffects.Value) target = Color.Lerp(target, heal, state.HealPulse * effectIntensity.Value);
            if (dangerEffects.Value) target = Color.Lerp(target, danger, state.DangerPulse * effectIntensity.Value);

            target *= Mathf.Clamp01(brightness.Value);
            target.a = 1f;
            float lerp = smoothing.Value <= 0 ? 1f : Mathf.Clamp01(Time.unscaledDeltaTime * smoothing.Value);
            current = Color.Lerp(current, target, lerp);
            logitech.Apply(current, state.HealthKnown ? state.Health : -1, state.HealthKnown ? state.HealthMax : -1,
                healthKeys.Value, movementKeys.Value, perKeyKeyboard.Value, mouseZones.Value, Mathf.Clamp01(brightness.Value));
        }

        private void RefreshLocalPlayerHealth()
        {
            try
            {
                Type playerAvatarType = AccessTools.TypeByName("PlayerAvatar");
                if (playerAvatarType == null) return;
                MethodInfo localGetter = AccessTools.Method("SemiFunc:PlayerGetLocal");
                object avatar = localGetter == null ? null : localGetter.Invoke(null, null);
                if (avatar == null) return;
                FieldInfo playerHealthField = AccessTools.Field(playerAvatarType, "playerHealth");
                object playerHealth = playerHealthField == null ? null : playerHealthField.GetValue(avatar);
                if (playerHealth == null) return;
                ReadHealthFrom(playerHealth);
            }
            catch { }
        }

        private static void ReadHealthFrom(object playerHealth)
        {
            if (Instance == null || playerHealth == null) return;
            try
            {
                Type t = playerHealth.GetType();
                FieldInfo health = AccessTools.Field(t, "health");
                FieldInfo maxHealth = AccessTools.Field(t, "maxHealth");
                if (health != null)
                {
                    Instance.state.Health = Convert.ToInt32(health.GetValue(playerHealth));
                    if (maxHealth != null) Instance.state.HealthMax = Math.Max(1, Convert.ToInt32(maxHealth.GetValue(playerHealth)));
                    Instance.state.HealthKnown = true;
                }
            }
            catch { }
        }

        private static Color ParseColor(string csv, Color fallback)
        {
            try
            {
                string[] p = csv.Split(',');
                if (p.Length < 3) return fallback;
                return new Color(Clamp255(p[0]) / 255f, Clamp255(p[1]) / 255f, Clamp255(p[2]) / 255f, 1f);
            }
            catch { return fallback; }
        }

        private static float Clamp255(string s)
        {
            float v;
            if (!float.TryParse(s.Trim(), out v)) return 0f;
            return Mathf.Clamp(v, 0f, 255f);
        }

        private void OnGUI()
        {
            if (!showUi) return;
            uiRect = GUI.Window(6201, uiRect, DrawUi, "Peripheral Lighting");
        }

        private void DrawUi(int id)
        {
            uiScroll = GUILayout.BeginScrollView(uiScroll, GUILayout.Height(510f));
            GUILayout.Label("Toggle: " + uiKey.Value);
            modEnabled.Value = GUILayout.Toggle(modEnabled.Value, "Enabled");
            perKeyKeyboard.Value = GUILayout.Toggle(perKeyKeyboard.Value, "Logitech per-key keyboard");
            mouseZones.Value = GUILayout.Toggle(mouseZones.Value, "Logitech mouse zones");
            healthKeys.Value = GUILayout.Toggle(healthKeys.Value, "Health on number row");
            movementKeys.Value = GUILayout.Toggle(movementKeys.Value, "Highlight WASD + space");
            GUILayout.Label("Effects");
            idleBreathing.Value = GUILayout.Toggle(idleBreathing.Value, "Idle breathing");
            dangerEffects.Value = GUILayout.Toggle(dangerEffects.Value, "Damage / death / low health");
            healEffects.Value = GUILayout.Toggle(healEffects.Value, "Healing");
            treasureEffects.Value = GUILayout.Toggle(treasureEffects.Value, "Valuables / shop / enemy hit");
            GUILayout.Label("Effect intensity: " + effectIntensity.Value.ToString("0.00"));
            effectIntensity.Value = GUILayout.HorizontalSlider(effectIntensity.Value, 0f, 1f);
            GUILayout.Label("Idle breath cycles/sec: " + idleBreathSpeed.Value.ToString("0.00"));
            idleBreathSpeed.Value = GUILayout.HorizontalSlider(idleBreathSpeed.Value, 0.05f, 2f);
            GUILayout.Label("Damage fade (sec): " + dangerDecaySeconds.Value.ToString("0.0"));
            dangerDecaySeconds.Value = GUILayout.HorizontalSlider(dangerDecaySeconds.Value, 0.1f, 10f);
            GUILayout.Label("Heal fade (sec): " + healFlashSeconds.Value.ToString("0.0"));
            healFlashSeconds.Value = GUILayout.HorizontalSlider(healFlashSeconds.Value, 0.1f, 10f);
            GUILayout.Label("Treasure fade (sec): " + treasureFlashSeconds.Value.ToString("0.0"));
            treasureFlashSeconds.Value = GUILayout.HorizontalSlider(treasureFlashSeconds.Value, 0.1f, 10f);
            GUILayout.Label("Brightness: " + brightness.Value.ToString("0.00"));
            brightness.Value = GUILayout.HorizontalSlider(brightness.Value, 0f, 1f);
            GUILayout.Label("Smoothing: " + smoothing.Value.ToString("0.0"));
            smoothing.Value = GUILayout.HorizontalSlider(smoothing.Value, 0f, 40f);
            GUILayout.Label("Low health threshold: " + lowHealthThreshold.Value.ToString("0.00"));
            lowHealthThreshold.Value = GUILayout.HorizontalSlider(lowHealthThreshold.Value, 0.05f, 0.9f);
            GUILayout.Label("Colors are editable in the cfg file:");
            GUILayout.Label("Idle " + idleColor.Value + " | Danger " + dangerColor.Value);
            GUILayout.Label("Heal " + healColor.Value + " | Treasure " + treasureColor.Value);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Test danger")) state.DangerPulse = 1f;
            if (GUILayout.Button("Test heal")) state.HealPulse = 1f;
            if (GUILayout.Button("Test treasure")) state.TreasurePulse = 1f;
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Save config")) Config.Save();
            GUILayout.EndScrollView();
            GUI.DragWindow();
        }

        private void OnDestroy()
        {
            try { if (harmony != null) harmony.UnpatchSelf(); } catch { }
            try { if (logitech != null) logitech.Shutdown(); } catch { }
        }

        internal static void Damage(object playerHealth)
        {
            if (Instance == null) return;
            ReadHealthFrom(playerHealth);
            Instance.state.DangerPulse = 1f;
        }

        internal static void Heal(object playerHealth)
        {
            if (Instance == null) return;
            ReadHealthFrom(playerHealth);
            Instance.state.HealPulse = 1f;
        }

        internal static void Death(object playerHealth)
        {
            if (Instance == null) return;
            ReadHealthFrom(playerHealth);
            Instance.state.DangerPulse = 1f;
            Instance.state.Health = 0;
            Instance.state.HealthKnown = true;
        }

        internal static void Treasure() { if (Instance != null) Instance.state.TreasurePulse = 1f; }
        internal static void EnemyHit() { if (Instance != null) Instance.state.TreasurePulse = Math.Max(Instance.state.TreasurePulse, 0.35f); }

        private sealed class LightingState
        {
            public bool HealthKnown;
            public int Health = 100;
            public int HealthMax = 100;
            public float DangerPulse;
            public float HealPulse;
            public float TreasurePulse;
            public void Tick(float dt, float dangerSeconds, float healSeconds, float treasureSeconds)
            {
                DangerPulse = Mathf.Max(0f, DangerPulse - dt / Mathf.Max(0.01f, dangerSeconds));
                HealPulse = Mathf.Max(0f, HealPulse - dt / Mathf.Max(0.01f, healSeconds));
                TreasurePulse = Mathf.Max(0f, TreasurePulse - dt / Mathf.Max(0.01f, treasureSeconds));
            }
        }

        private static class PlayerHealthHurtPatch { public static void Postfix(object __instance) { Damage(__instance); } }
        private static class PlayerHealthHealPatch { public static void Postfix(object __instance) { Heal(__instance); } }
        private static class PlayerHealthDeathPatch { public static void Postfix(object __instance) { Death(__instance); } }
        private static class EnemyHealthHurtPatch { public static void Postfix() { EnemyHit(); } }
        private static class ValuableDiscoverPatch { public static void Postfix() { Treasure(); } }
        private static class ValuableValuePatch { public static void Postfix() { Treasure(); } }
        private static class ShopPatch { public static void Postfix() { Treasure(); } }
    }

    internal sealed class LogitechLighting
    {
        private readonly BepInEx.Logging.ManualLogSource log;
        private bool initialized;
        private float nextApply;
        private readonly byte[] bitmap = new byte[21 * 6 * 4];
        public bool Available { get { return initialized; } }

        public LogitechLighting(BepInEx.Logging.ManualLogSource log)
        {
            this.log = log;
        }

        public void Initialize()
        {
            try
            {
                // Unity/Mono does not reliably probe nested BepInEx plugin directories
                // for native DLLs. Load our bundled copy by absolute path first.
                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    string path = Path.Combine(Path.GetDirectoryName(typeof(LogitechLighting).Assembly.Location), "LogitechLedEnginesWrapper.dll");
                    if (!File.Exists(path))
                    {
                        log.LogWarning("Bundled Logitech LED SDK DLL missing: " + path);
                        return;
                    }
                    if (Native.LoadLibrary(path) == IntPtr.Zero)
                    {
                        int error = Marshal.GetLastWin32Error();
                        log.LogWarning("Cannot load Logitech LED SDK DLL: " + path + " (Windows error " + error + "). " +
                            (error == 126 ? "DLL exists, but a dependency may be missing: install Microsoft's Visual C++ 2013 Redistributable (x64) for MSVCR120.dll in the game environment. " : "") +
                            "G HUB/LGS must also be running for lighting to initialize.");
                        return;
                    }
                }
                initialized = Native.LogiLedInitWithName("R.E.P.O Peripheral Lighting");
                if (!initialized) initialized = Native.LogiLedInit();
                if (initialized)
                {
                    Try(() => Native.LogiLedSaveCurrentLighting());
                    Try(() => Native.LogiLedStopEffects());
                    log.LogInfo("Logitech LED SDK initialized.");
                }
                else log.LogWarning("Logitech LED SDK returned false. Is G HUB/LGS running in the same Windows/Proton prefix?");
            }
            catch (DllNotFoundException ex)
            {
                log.LogWarning("Logitech LED SDK DLL not found: " + ex.Message + ". Copy LogitechLedEnginesWrapper.dll or LogitechLed.dll beside REPO.exe / into the Proton prefix.");
            }
            catch (Exception ex)
            {
                log.LogWarning("Logitech LED SDK init failed: " + ex.Message);
            }
        }

        public void Apply(Color c, int health, int maxHealth, bool showHealthKeys, bool showMovementKeys, bool perKey, bool zones, float brightness)
        {
            if (!initialized) return;
            if (Time.unscaledTime < nextApply) return;
            nextApply = Time.unscaledTime + 0.05f;
            int r = Percent(c.r), g = Percent(c.g), b = Percent(c.b);
            Try(() => Native.LogiLedSetLighting(r, g, b));
            if (zones)
            {
                Try(() => Native.LogiLedSetLightingForTargetZone(3, 0, r, g, b));
                Try(() => Native.LogiLedSetLightingForTargetZone(3, 1, r, g, b));
                Try(() => Native.LogiLedSetLightingForTargetZone(3, 2, r, g, b));
            }
            if (perKey)
            {
                FillBitmap(r, g, b);
                if (showMovementKeys) PaintWasd(brightness);
                if (showHealthKeys && health >= 0 && maxHealth > 0) PaintHealth(health, maxHealth, brightness);
                Try(() => Native.LogiLedSetLightingFromBitmap(bitmap));
            }
        }

        private void FillBitmap(int r, int g, int b)
        {
            byte red = (byte)Mathf.RoundToInt(r * 255f / 100f);
            byte green = (byte)Mathf.RoundToInt(g * 255f / 100f);
            byte blue = (byte)Mathf.RoundToInt(b * 255f / 100f);
            for (int i = 0; i < bitmap.Length; i += 4)
            {
                bitmap[i] = blue;
                bitmap[i + 1] = green;
                bitmap[i + 2] = red;
                bitmap[i + 3] = 255;
            }
        }

        private void PaintWasd(float brightness)
        {
            int g = Mathf.RoundToInt(204 * brightness), b = Mathf.RoundToInt(255 * brightness);
            SetKey(2, 2, 0, g, b); // W
            SetKey(1, 3, 0, g, b); // A
            SetKey(2, 3, 0, g, b); // S
            SetKey(3, 3, 0, g, b); // D
            SetKey(5, 5, 0, g, b); // Space
        }

        private void PaintHealth(int health, int maxHealth, float brightness)
        {
            float ratio = Mathf.Clamp01((float)health / Mathf.Max(1, maxHealth));
            int lit = Mathf.Clamp(Mathf.CeilToInt(ratio * 10f), 0, 10);
            int value = Mathf.RoundToInt(255 * brightness);
            for (int i = 0; i < 10; i++)
            {
                if (i < lit) SetKey(i + 1, 1, 0, value, 0);
                else SetKey(i + 1, 1, value, 0, 0);
            }
        }

        private void SetKey(int x, int y, int r, int g, int b)
        {
            if (x < 0 || x >= 21 || y < 0 || y >= 6) return;
            int i = ((y * 21) + x) * 4;
            bitmap[i] = (byte)b;
            bitmap[i + 1] = (byte)g;
            bitmap[i + 2] = (byte)r;
            bitmap[i + 3] = 255;
        }

        public void Resume()
        {
            if (initialized) Try(() => Native.LogiLedSaveCurrentLighting());
        }

        public void Suspend()
        {
            if (!initialized) return;
            Try(() => Native.LogiLedRestoreLighting());
        }

        public void Shutdown()
        {
            if (!initialized) return;
            Try(() => Native.LogiLedRestoreLighting());
            Try(() => Native.LogiLedShutdown());
            initialized = false;
        }

        private static int Percent(float v) { return Mathf.Clamp(Mathf.RoundToInt(v * 100f), 0, 100); }
        private static void Try(Action action) { try { action(); } catch { } }
    }

    internal static class Native
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr LoadLibrary(string fileName);

        [DllImport("LogitechLedEnginesWrapper", CallingConvention = CallingConvention.Cdecl, EntryPoint = "LogiLedInit")]
        [return: MarshalAs(UnmanagedType.I1)] public static extern bool LogiLedInit();
        [DllImport("LogitechLedEnginesWrapper", CallingConvention = CallingConvention.Cdecl, EntryPoint = "LogiLedInitWithName")]
        [return: MarshalAs(UnmanagedType.I1)] public static extern bool LogiLedInitWithName([MarshalAs(UnmanagedType.LPWStr)] string name);
        [DllImport("LogitechLedEnginesWrapper", CallingConvention = CallingConvention.Cdecl, EntryPoint = "LogiLedShutdown")]
        public static extern void LogiLedShutdown();
        [DllImport("LogitechLedEnginesWrapper", CallingConvention = CallingConvention.Cdecl, EntryPoint = "LogiLedSaveCurrentLighting")]
        [return: MarshalAs(UnmanagedType.I1)] public static extern bool LogiLedSaveCurrentLighting();
        [DllImport("LogitechLedEnginesWrapper", CallingConvention = CallingConvention.Cdecl, EntryPoint = "LogiLedRestoreLighting")]
        [return: MarshalAs(UnmanagedType.I1)] public static extern bool LogiLedRestoreLighting();
        [DllImport("LogitechLedEnginesWrapper", CallingConvention = CallingConvention.Cdecl, EntryPoint = "LogiLedStopEffects")]
        [return: MarshalAs(UnmanagedType.I1)] public static extern bool LogiLedStopEffects();
        [DllImport("LogitechLedEnginesWrapper", CallingConvention = CallingConvention.Cdecl, EntryPoint = "LogiLedSetLighting")]
        [return: MarshalAs(UnmanagedType.I1)] public static extern bool LogiLedSetLighting(int redPercentage, int greenPercentage, int bluePercentage);
        [DllImport("LogitechLedEnginesWrapper", CallingConvention = CallingConvention.Cdecl, EntryPoint = "LogiLedSetLightingForTargetZone")]
        [return: MarshalAs(UnmanagedType.I1)] public static extern bool LogiLedSetLightingForTargetZone(int deviceType, int zone, int redPercentage, int greenPercentage, int bluePercentage);
        [DllImport("LogitechLedEnginesWrapper", CallingConvention = CallingConvention.Cdecl, EntryPoint = "LogiLedSetLightingFromBitmap")]
        [return: MarshalAs(UnmanagedType.I1)] public static extern bool LogiLedSetLightingFromBitmap(byte[] bitmap);
    }
}
