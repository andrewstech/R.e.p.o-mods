using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.Networking;

namespace Andrew.REPO.Soundboard
{
    [BepInPlugin("andrew.repo.soundboard", "R.E.P.O Soundboard", "0.1.0")]
    public sealed class SoundboardPlugin : BaseUnityPlugin
    {
        private const int SlotCount = 8;
        private readonly Dictionary<int, AudioClip> clips = new Dictionary<int, AudioClip>();
        private readonly Dictionary<int, string> clipNames = new Dictionary<int, string>();
        private ConfigEntry<bool> modEnabled;
        private ConfigEntry<float> volume;
        private ConfigEntry<bool> spatial;
        private ConfigEntry<float> spatialBlend;
        private ConfigEntry<float> maxDistance;
        private ConfigEntry<KeyboardShortcut> reloadKey;
        private ConfigEntry<KeyboardShortcut> stopKey;
        private readonly ConfigEntry<KeyboardShortcut>[] slotKeys = new ConfigEntry<KeyboardShortcut>[SlotCount];
        private readonly ConfigEntry<string>[] slotFiles = new ConfigEntry<string>[SlotCount];
        private ConfigEntry<KeyboardShortcut> uiKey;
        private bool showUi;
        private Rect uiRect = new Rect(410f, 30f, 390f, 430f);
        private Vector2 scroll;
        private GameObject audioHost;
        private AudioSource source;
        private string soundDir;

        private void Awake()
        {
            soundDir = Path.Combine(Paths.PluginPath, "REPOSoundboard", "sounds");
            Directory.CreateDirectory(soundDir);

            modEnabled = Config.Bind("General", "Enabled", true, "Enable the soundboard.");
            volume = Config.Bind("General", "Volume", 0.85f, new ConfigDescription("Playback volume.", new AcceptableValueRange<float>(0f, 1f)));
            spatial = Config.Bind("Playback", "Spatial", false, "If true, play from the player/camera position instead of pure local 2D audio.");
            spatialBlend = Config.Bind("Playback", "SpatialBlend", 0.75f, new ConfigDescription("0 = 2D, 1 = fully 3D when Spatial is enabled.", new AcceptableValueRange<float>(0f, 1f)));
            maxDistance = Config.Bind("Playback", "MaxDistance", 18f, new ConfigDescription("3D max audible distance when Spatial is enabled.", new AcceptableValueRange<float>(1f, 100f)));
            reloadKey = Config.Bind("Keys", "ReloadSounds", new KeyboardShortcut(KeyCode.F10), "Reload sound files from disk.");
            stopKey = Config.Bind("Keys", "StopAll", new KeyboardShortcut(KeyCode.F9), "Stop currently playing soundboard audio.");
            uiKey = Config.Bind("UI", "ToggleConfigUi", new KeyboardShortcut(KeyCode.F7), "Toggle the in-game soundboard config UI.");

            for (int i = 0; i < SlotCount; i++)
            {
                slotKeys[i] = Config.Bind("Keys", "Slot" + (i + 1), new KeyboardShortcut(KeyCode.F1 + i), "Play soundboard slot " + (i + 1) + ".");
                slotFiles[i] = Config.Bind("Files", "Slot" + (i + 1), i == 0 ? "sample_beep.wav" : "", "File name in BepInEx/plugins/REPOSoundboard/sounds. Supports wav/ogg/mp3 if Unity can decode it.");
            }

            CreateAudioHost();
            StartCoroutine(LoadAllSounds());
            Logger.LogInfo("R.E.P.O Soundboard loaded. Put sounds in: " + soundDir);
        }

        private void CreateAudioHost()
        {
            audioHost = new GameObject("REPO Soundboard AudioHost");
            DontDestroyOnLoad(audioHost);
            source = audioHost.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.volume = volume.Value;
        }

        private void Update()
        {
            if (uiKey.Value.IsDown()) showUi = !showUi;
            if (!modEnabled.Value) return;

            if (reloadKey.Value.IsDown())
            {
                StopAllCoroutines();
                StartCoroutine(LoadAllSounds());
            }

            if (stopKey.Value.IsDown() && source != null) source.Stop();

            for (int i = 0; i < SlotCount; i++)
            {
                if (slotKeys[i].Value.IsDown()) PlaySlot(i);
            }
        }

        private IEnumerator LoadAllSounds()
        {
            clips.Clear();
            clipNames.Clear();
            for (int i = 0; i < SlotCount; i++)
            {
                string file = (slotFiles[i].Value ?? "").Trim();
                if (file.Length == 0) continue;
                string path = Path.Combine(soundDir, file);
                if (!File.Exists(path))
                {
                    Logger.LogWarning("Soundboard slot " + (i + 1) + " missing: " + path);
                    continue;
                }
                yield return StartCoroutine(LoadClip(i, path));
            }
            Logger.LogInfo("Soundboard loaded " + clips.Count + " slot(s).");
        }

        private IEnumerator LoadClip(int slot, string path)
        {
            AudioType type = AudioType.UNKNOWN;
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".wav") type = AudioType.WAV;
            else if (ext == ".ogg") type = AudioType.OGGVORBIS;
            else if (ext == ".mp3") type = AudioType.MPEG;

            string uri = new Uri(path).AbsoluteUri;
            UnityWebRequest req = UnityWebRequestMultimedia.GetAudioClip(uri, type);
            yield return req.SendWebRequest();

            if (req.isNetworkError || req.isHttpError)
            {
                Logger.LogWarning("Failed to load sound slot " + (slot + 1) + ": " + req.error + " | " + path);
                yield break;
            }

            AudioClip clip = DownloadHandlerAudioClip.GetContent(req);
            if (clip == null)
            {
                Logger.LogWarning("Unity returned no clip for sound slot " + (slot + 1) + ": " + path);
                yield break;
            }

            clips[slot] = clip;
            clipNames[slot] = Path.GetFileName(path);
            Logger.LogInfo("Loaded sound slot " + (slot + 1) + ": " + Path.GetFileName(path));
        }

        private void PlaySlot(int slot)
        {
            if (!clips.ContainsKey(slot))
            {
                Logger.LogWarning("Soundboard slot " + (slot + 1) + " has no loaded sound.");
                return;
            }

            EnsureAudioHost();
            PositionAudioHost();
            source.volume = Mathf.Clamp01(volume.Value);
            source.spatialBlend = spatial.Value ? Mathf.Clamp01(spatialBlend.Value) : 0f;
            source.maxDistance = Mathf.Max(1f, maxDistance.Value);
            source.rolloffMode = AudioRolloffMode.Linear;
            source.PlayOneShot(clips[slot], source.volume);
            Logger.LogInfo("Played soundboard slot " + (slot + 1) + ": " + clipNames[slot]);
        }

        private void EnsureAudioHost()
        {
            if (audioHost == null || source == null) CreateAudioHost();
        }

        private void PositionAudioHost()
        {
            if (audioHost == null) return;
            Camera cam = Camera.main;
            if (cam != null)
            {
                audioHost.transform.position = cam.transform.position;
                return;
            }
            audioHost.transform.position = Vector3.zero;
        }

        private void OnGUI()
        {
            if (!showUi) return;
            uiRect = GUI.Window(6202, uiRect, DrawUi, "Soundboard");
        }

        private void DrawUi(int id)
        {
            GUILayout.Label("Toggle: " + uiKey.Value + " | Reload: " + reloadKey.Value + " | Stop: " + stopKey.Value);
            modEnabled.Value = GUILayout.Toggle(modEnabled.Value, "Enabled");
            spatial.Value = GUILayout.Toggle(spatial.Value, "Spatial / positional playback");
            GUILayout.Label("Volume: " + volume.Value.ToString("0.00"));
            volume.Value = GUILayout.HorizontalSlider(volume.Value, 0f, 1f);
            GUILayout.Label("Spatial blend: " + spatialBlend.Value.ToString("0.00"));
            spatialBlend.Value = GUILayout.HorizontalSlider(spatialBlend.Value, 0f, 1f);
            GUILayout.Label("Max distance: " + maxDistance.Value.ToString("0"));
            maxDistance.Value = GUILayout.HorizontalSlider(maxDistance.Value, 1f, 100f);
            GUILayout.Label("Sound folder: " + soundDir);
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(205f));
            for (int i = 0; i < SlotCount; i++)
            {
                string status = clips.ContainsKey(i) ? "loaded" : "missing";
                GUILayout.BeginHorizontal();
                GUILayout.Label("F" + (i + 1) + " " + slotFiles[i].Value + " (" + status + ")");
                if (GUILayout.Button("Play", GUILayout.Width(60f))) PlaySlot(i);
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Reload")) { StopAllCoroutines(); StartCoroutine(LoadAllSounds()); }
            if (GUILayout.Button("Stop")) { if (source != null) source.Stop(); }
            if (GUILayout.Button("Save")) Config.Save();
            GUILayout.EndHorizontal();
            GUI.DragWindow();
        }

        private void OnDestroy()
        {
            if (audioHost != null) Destroy(audioHost);
        }
    }
}
