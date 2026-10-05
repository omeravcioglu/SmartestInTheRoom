"""The trailer's music, synthesised from nothing but numpy, so there is no licence to worry about.

A bright 120 BPM groove in C major (C | Am | F | G): marimba arpeggios, a plucky bass, chord
stabs on the off-beats, four-on-the-floor kick, claps and hats. Laid out bar by bar against the
trailer's edit (one bar is two seconds):

  bars  0-3   intro      soft marimba and hats under the opening line
  bar   4     drop       crash; the full groove from here ("69 MINIGAMES.")
  bar  15     break      drums out, a minor iv, a riser ("FAIL, AND YOU'RE OUT.")
  bar  16     hit        the groove is back (the knock-out)
  bar  23     build      snare roll and riser ("FIRST TO 100...")
  bar  24     hit        the last groove, melody up an octave
  bar  28     end        one big chord, left to ring

render() returns float32 stereo at SR, peak about -3 dBFS. Run this file to write music.wav.
"""
import math
import wave

import numpy as np

SR = 48000
BPM = 120
BEAT = 60.0 / BPM
BAR = 4 * BEAT
EIGHTH = BEAT / 2
BARS = 31  # 62 seconds

INTRO = range(0, 4)
BREAK_BAR = 15
BUILD_BAR = 23
END_BAR = 28
DROPS = (4, 16, 24)

# The progression, one chord a bar; the break bar is a minor iv then the V.
PROGRESSION = ["C", "Am", "F", "G"]
TRIADS = {  # stab voicings
    "C": ("C4", "E4", "G4"), "Am": ("C4", "E4", "A4"), "F": ("C4", "F4", "A4"),
    "G": ("B3", "D4", "G4"), "Fm": ("C4", "F4", "Ab4"),
}
ROOTS = {"C": "C2", "Am": "A1", "F": "F1", "G": "G1", "Fm": "F1"}
# A 4-bar marimba hook over the progression, in eighths (None = rest).
HOOK = {
    "C": ["E5", "G5", "C6", "G5", "E5", "G5", "A5", "G5"],
    "Am": ["E5", "A5", "C6", "A5", "E5", "A5", "G5", "E5"],
    "F": ["F5", "A5", "C6", "A5", "F5", "A5", "G5", "F5"],
    "G": ["D5", "G5", "B5", "G5", "D5", "G5", "A5", "B5"],
}
NAMES = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}


def freq(note):
    """'C4', 'Ab4', 'F#3' -> Hz (A4 = 440)."""
    name, rest = note[0], note[1:]
    shift = 0
    while rest and rest[0] in "#b":
        shift += 1 if rest[0] == "#" else -1
        rest = rest[1:]
    midi = 12 * (int(rest) + 1) + NAMES[name] + shift
    return 440.0 * 2 ** ((midi - 69) / 12)


def chord_of(bar):
    if bar == BREAK_BAR:
        return "Fm"
    return PROGRESSION[bar % 4]


# ---------------------------------------------------------------------------
# Voices: each returns a mono burst starting at t = 0
# ---------------------------------------------------------------------------

def _t(seconds):
    return np.arange(int(seconds * SR)) / SR


def kick():
    t = _t(0.42)
    f = 46 + 80 * np.exp(-t / 0.035)
    phase = 2 * np.pi * np.cumsum(f) / SR
    body = np.sin(phase) * np.exp(-t / 0.26)
    click = np.random.default_rng(1).standard_normal(len(t)) * np.exp(-t / 0.002) * 0.3
    return (body + click) * 0.95


def _band(noise, lo, hi):
    spec = np.fft.rfft(noise)
    f = np.fft.rfftfreq(len(noise), 1 / SR)
    spec[(f < lo) | (f > hi)] = 0
    return np.fft.irfft(spec, len(noise))


def clap(seed=2):
    t = _t(0.3)
    rng = np.random.default_rng(seed)
    noise = _band(rng.standard_normal(len(t)), 900, 5200)
    env = np.zeros(len(t))
    for start in (0.0, 0.011, 0.022):  # the three hands of a clap
        i = int(start * SR)
        env[i:] += np.exp(-(t[: len(t) - i]) / (0.11 if start == 0.022 else 0.012))
    out = noise * env
    return out / (np.abs(out).max() + 1e-9) * 0.5


def hat(open_=False, seed=3):
    t = _t(0.25 if open_ else 0.06)
    noise = _band(np.random.default_rng(seed).standard_normal(len(t)), 7000, 16000)
    out = noise * np.exp(-t / (0.11 if open_ else 0.022))
    return out / (np.abs(out).max() + 1e-9) * (0.16 if open_ else 0.12)


def crash():
    t = _t(2.2)
    noise = _band(np.random.default_rng(4).standard_normal(len(t)), 4500, 16000)
    out = noise * np.exp(-t / 0.8)
    return out / (np.abs(out).max() + 1e-9) * 0.22


def marimba(note, length=0.6):
    t = _t(length)
    f = freq(note)
    s = np.sin(2 * np.pi * f * t) * np.exp(-t / 0.38)
    s += 0.42 * np.sin(2 * np.pi * 3.93 * f * t) * np.exp(-t / 0.055)  # the bar's bright overtone
    s += 0.10 * np.sin(2 * np.pi * 9.5 * f * t) * np.exp(-t / 0.012)   # the mallet
    attack = np.minimum(1, t / 0.002)
    return s * attack * 0.32


def bass(note, length=0.26):
    t = _t(length)
    f = freq(note)
    s = np.zeros(len(t))
    for k in range(1, 9):  # a saw whose top end dies first: a pluck without a filter
        s += np.sin(2 * np.pi * k * f * t) / k * np.exp(-t * (2.5 + 7 * k))
    s *= np.minimum(1, t / 0.003) * np.minimum(1, (length - t) / 0.02)
    return s * 0.42


def stab(notes, length=0.2, detune_cents=7):
    t = _t(length)
    left = np.zeros(len(t))
    right = np.zeros(len(t))
    for n in notes:
        f = freq(n)
        for side, cents in ((left, -detune_cents), (right, detune_cents)):
            ff = f * 2 ** (cents / 1200)
            for k in range(1, 6):
                side += np.sin(2 * np.pi * k * ff * t) / k * np.exp(-t * (9 + 5 * k))
    env = np.minimum(1, t / 0.004)
    return np.stack([left * env, right * env], axis=1) * 0.09


def riser(seconds):
    t = _t(seconds)
    x = t / seconds
    f = 220 * 2 ** (2.5 * x)  # up two and a half octaves
    tone = np.sin(2 * np.pi * np.cumsum(f) / SR) * 0.10 * x ** 2
    noise = _band(np.random.default_rng(5).standard_normal(len(t)), 2000, 12000) * 0.06 * x ** 3
    return tone + noise


# ---------------------------------------------------------------------------
# Arrangement
# ---------------------------------------------------------------------------

def _add(buf, sound, at, gain=1.0, pan=0.0):
    """Mix a mono or stereo burst into a stereo buffer at time `at` (seconds)."""
    i = int(round(at * SR))
    if i >= len(buf):
        return
    if sound.ndim == 1:
        left = sound * gain * math.cos((pan + 1) * math.pi / 4) * math.sqrt(2)
        right = sound * gain * math.sin((pan + 1) * math.pi / 4) * math.sqrt(2)
        sound = np.stack([left, right], axis=1)
    else:
        sound = sound * gain
    n = min(len(sound), len(buf) - i)
    buf[i:i + n] += sound[:n]


def _reverb(dry, seconds=1.8, decay=0.42, seed=6):
    """FFT convolution with a decaying-noise impulse, a little different per side."""
    t = _t(seconds)
    out = np.zeros_like(dry)
    for ch in range(2):
        ir = np.random.default_rng(seed + ch).standard_normal(len(t)) * np.exp(-t / decay)
        ir = _band(ir, 200, 9000)
        ir /= np.sqrt((ir ** 2).sum())
        n = len(dry[:, ch]) + len(ir) - 1
        size = 1 << (n - 1).bit_length()
        wet = np.fft.irfft(np.fft.rfft(dry[:, ch], size) * np.fft.rfft(ir, size), size)[: len(dry)]
        out[:, ch] = wet
    return out


def render():
    total = BARS * BAR + 3.0
    drums = np.zeros((int(total * SR), 2))
    tonal = np.zeros_like(drums)
    K, CL, HC, HO, CR = kick(), clap(), hat(), hat(True), crash()

    for bar in range(BARS):
        t0 = bar * BAR
        chord = chord_of(bar)
        intro = bar in INTRO
        final = bar >= END_BAR

        if final:
            if bar == END_BAR:
                # One big chord, left to ring.
                big = ["C3", "G3", "C4", "E4", "G4", "C5", "E5"]
                _add(drums, K, t0, 1.0)
                _add(drums, CR, t0, 1.6)
                _add(tonal, bass("C2", 2.5), t0, 1.0)
                _add(tonal, stab(TRIADS["C"] + ("C5",), 1.4, 9), t0, 2.2)
                for k, n in enumerate(big):
                    _add(tonal, marimba(n, 2.8), t0 + 0.012 * k, 0.9, pan=-0.5 + k / len(big))
            elif bar == END_BAR + 1:
                # Under the end card: the hook once more, quietly, dying away.
                for step, n in enumerate(HOOK["C"]):
                    _add(tonal, marimba(n), t0 + step * EIGHTH, 0.5 - 0.04 * step, pan=0.3 if step % 2 else -0.3)
                _add(tonal, bass("C2", 1.2), t0, 0.35)
            elif bar == END_BAR + 2:
                # And a last soft chord to finish on.
                for k, n in enumerate(["C4", "E4", "G4", "C5"]):
                    _add(tonal, marimba(n, 2.5), t0 + 0.02 * k, 0.42, pan=-0.3 + 0.2 * k)
            continue

        if bar in DROPS:
            _add(drums, CR, t0, 1.0)

        if bar == BREAK_BAR:
            # Drums out: the minor iv held, then the V with a riser into the hit.
            for k, n in enumerate(TRIADS["Fm"]):
                _add(tonal, marimba(n, 1.0), t0 + 0.01 * k, 0.8)
            _add(tonal, stab(TRIADS["Fm"], 0.9), t0, 1.4)
            _add(tonal, bass("F1", 0.9), t0, 0.9)
            for k, n in enumerate(TRIADS["G"]):
                _add(tonal, marimba(n, 1.0), t0 + 2 * BEAT + 0.01 * k, 0.8)
            _add(tonal, bass("G1", 0.9), t0 + 2 * BEAT, 0.9)
            _add(tonal, riser(2 * BEAT), t0 + 2 * BEAT, 1.0)
            continue

        # Drums.
        for step in range(8):
            at = t0 + step * EIGHTH
            if intro:
                _add(drums, HC, at, 0.55 if step % 2 else 0.35, pan=0.25)
                continue
            if step % 2 == 0:
                _add(drums, K, at, 0.95)
            if step in (2, 6):
                _add(drums, CL, at, 1.0, pan=-0.05)
            open_hat = step % 2 == 1 and bar >= 16
            _add(drums, HO if open_hat else HC, at, 0.8 if step % 2 else 0.55, pan=0.25)
        if bar == BUILD_BAR:
            # A roll that speeds up and swells into the last section.
            hits = [t0 + i * BEAT / 4 for i in range(8)] + [t0 + 2 * BEAT + i * BEAT / 8 for i in range(16)]
            for i, at in enumerate(hits):
                _add(drums, clap(10 + i), at, 0.25 + 0.75 * i / len(hits), pan=0.1)
            _add(tonal, riser(BAR), t0, 1.2)

        # Bass: bouncing between the root, its octave and the fifth.
        root = ROOTS[chord]
        rf = freq(root)
        octave = rf * 2
        fifth = rf * 1.5
        pattern = [rf, octave, rf, fifth, rf, octave, fifth, octave]
        for step, f in enumerate(pattern):
            if intro and step % 2:
                continue
            note = _note_from_freq(f)
            _add(tonal, bass(note), t0 + step * EIGHTH, 0.55 if intro else 1.0)

        # Stabs on the off-beats; on every beat in the last section.
        steps = range(8) if bar >= 24 else (1, 3, 5, 7)
        for step in steps:
            _add(tonal, stab(TRIADS[chord]), t0 + step * EIGHTH, 0.5 if intro else 1.0)

        # The marimba hook, an octave up for the last section.
        hook = HOOK[chord]
        for step, n in enumerate(hook):
            if n is None:
                continue
            if bar >= 24:
                n = n[:-1] + str(int(n[-1]) + 1) if int(n[-1]) < 6 else n
            _add(tonal, marimba(n), t0 + step * EIGHTH, 0.6 if intro else 1.0, pan=0.3 if step % 2 else -0.3)

    mix = drums + tonal + _reverb(tonal) * 0.28 + _reverb(drums, 0.9, 0.18) * 0.06
    mix = mix[: int((BARS * BAR) * SR)]
    # The last chord's tail fades out over the final second and a half.
    fade = int(1.5 * SR)
    mix[-fade:] *= np.linspace(1, 0, fade)[:, None] ** 2
    # About -3 dBFS of headroom for the voice and the limiter.
    mix /= np.abs(mix).max() + 1e-9
    return (mix * 0.7).astype(np.float32)


def _note_from_freq(f):
    midi = int(round(69 + 12 * math.log2(f / 440.0)))
    names = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"]
    return f"{names[midi % 12]}{midi // 12 - 1}"


def write_wav(path, stereo):
    data = (np.clip(stereo, -1, 1) * 32767).astype("<i2")
    with wave.open(path, "wb") as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(data.tobytes())


if __name__ == "__main__":
    import sys

    out = sys.argv[1] if len(sys.argv) > 1 else "music.wav"
    write_wav(out, render())
    print("wrote", out)
