#!/usr/bin/env python3
import math, pathlib, struct, sys, wave
path = pathlib.Path(sys.argv[1])
path.parent.mkdir(parents=True, exist_ok=True)
rate = 44100
dur = 0.25
freq = 880
with wave.open(str(path), 'wb') as w:
    w.setnchannels(1)
    w.setsampwidth(2)
    w.setframerate(rate)
    for n in range(int(rate * dur)):
        env = min(1, n / (rate * 0.02), (rate * dur - n) / (rate * 0.04))
        val = int(32767 * 0.25 * env * math.sin(2 * math.pi * freq * n / rate))
        w.writeframes(struct.pack('<h', val))
