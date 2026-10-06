"""The trailer's music, synthesised from nothing but numpy, so there is no licence to worry about.

A warm, bouncy game-show groove at 120 BPM in C major (Cmaj9 | Am7 | Fmaj7 | G): an electric
piano (two-operator FM, like a Rhodes) comping the chords, a round synth bass, a plucked guitar
hook (Karplus-Strong) that never climbs above E5, a full kit (kick, snare and clap, hats, a
shaker), and brass for the big moments. Everything sits in the middle of the spectrum: the top
end is rolled off on the master so nothing is shrill.

Laid out bar by bar against the trailer's edit (one bar is two seconds):

  bars  0-3   intro      e-piano, bass and shaker under the opening line, a swell into the drop
  bar   4     drop       slam + brass hit; the full groove ("69 MINIGAMES.")
  bar  15     break      slam, drums out, Fm7, a swell ("FAIL, AND YOU'RE OUT.")
  bar  16     knock-out  half-time groove; a sad trombone "wah-wah" as the OUT! banner lands
  bar  18     groove     back to full time (the second montage)
  bar  23     build      slam, snare roll and swell ("FIRST TO 100...")
  bar  24     last       the groove with everything in; a brass fanfare as the winner appears
  bar  28     end        slam and one big chord, left to ring under the end card

render() returns float32 stereo at SR, peak about -3 dBFS. Run this file to write music.wav.
"""
import math
import wave

import numpy as np

SR = 48000
BPM = 120
BEAT = 60.0 / BPM
BAR = 4 * BEAT
STEP = BEAT / 4          # a sixteenth
BARS = 31                # 62 seconds

INTRO = range(0, 4)
BREAK_BAR = 15
HALFTIME = (16, 17)
BUILD_BAR = 23
END_BAR = 28
DROPS = (4, 16, 24)
SLAMS = (4 * BAR, BREAK_BAR * BAR, BUILD_BAR * BAR, END_BAR * BAR)  # the title cards land
OUT_AT = 33.25           # the OUT! banner in the knock-out clip
WINNER_AT = 51.0         # the winner page

PROGRESSION = ["C", "Am", "F", "G"]
ROOTS = {"C": 36, "Am": 33, "F": 29, "G": 31, "Fm": 29}           # MIDI, bass register
VOICINGS = {                                                       # e-piano, rootless and close
    "C": (52, 55, 59, 62),    # E3 G3 B3 D4   Cmaj9
    "Am": (52, 55, 57, 60),   # E3 G3 A3 C4   Am7
    "F": (53, 57, 60, 64),    # F3 A3 C4 E4   Fmaj7
    "G": (50, 55, 59, 62),    # D3 G3 B3 D4   G
    "Fm": (53, 56, 60, 63),   # F3 Ab3 C4 Eb4 Fm7
}
BRASS = {
    "C": (55, 60, 64, 67), "Am": (57, 60, 64, 69), "F": (57, 60, 65, 69), "G": (55, 59, 62, 67),
}
# The hook, per chord, in sixteenths: (step, MIDI, length in steps). Tops out at E5 (76).
HOOK = {
    "C": [(0, 67, 2), (2, 72, 2), (4, 76, 2), (6, 74, 1), (7, 72, 3), (10, 69, 2), (12, 67, 2), (14, 64, 2)],
    "Am": [(0, 69, 2), (2, 72, 2), (4, 76, 2), (6, 74, 1), (7, 72, 3), (10, 71, 2), (12, 69, 2), (14, 67, 2)],
    "F": [(0, 65, 2), (2, 69, 2), (4, 72, 2), (6, 74, 1), (7, 72, 3), (10, 69, 2), (12, 72, 2), (14, 74, 2)],
    "G": [(0, 71, 2), (2, 74, 1), (3, 76, 1), (4, 74, 2), (6, 71, 2), (8, 67, 2), (10, 69, 1), (11, 71, 1), (12, 74, 4)],
}
# E-piano comping: (step, velocity, length in steps).
COMP = [(0, 0.85, 4), (6, 0.65, 2), (10, 0.75, 3), (14, 0.55, 2)]


def hz(midi):
    return 440.0 * 2 ** ((midi - 69) / 12)


def chord_of(bar):
    return "Fm" if bar == BREAK_BAR else PROGRESSION[bar % 4]


def _t(seconds):
    return np.arange(max(1, int(seconds * SR))) / SR


def _rng(seed):
    return np.random.default_rng(seed)


# ---------------------------------------------------------------------------
# Filters (FFT, static) and small helpers
# ---------------------------------------------------------------------------

def _shape(x, gain_of_f):
    n = len(x)
    size = 1 << max(1, (n - 1).bit_length())
    spec = np.fft.rfft(x, size, axis=0)
    f = np.fft.rfftfreq(size, 1 / SR)
    g = gain_of_f(f)
    spec *= g if spec.ndim == 1 else g[:, None]
    return np.fft.irfft(spec, size, axis=0)[:n]


def lowpass(x, fc, order=2):
    return _shape(x, lambda f: 1 / np.sqrt(1 + (f / fc) ** (2 * order)))


def highpass(x, fc, order=2):
    return _shape(x, lambda f: 1 / np.sqrt(1 + (fc / np.maximum(f, 1e-3)) ** (2 * order)))


def bandpass(x, lo, hi, order=2):
    return highpass(lowpass(x, hi, order), lo, order)


def env_ar(t, attack, decay):
    return np.minimum(1, t / max(attack, 1e-4)) * np.exp(-t / decay)


def gate(t, length, release=0.03):
    return np.clip((length - t) / release, 0, 1)


def norm(x, peak=1.0):
    return x / (np.abs(x).max() + 1e-12) * peak


# ---------------------------------------------------------------------------
# Drums
# ---------------------------------------------------------------------------

def kick():
    t = _t(0.5)
    f = 50 + 110 * np.exp(-t / 0.03)
    body = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t / 0.28)
    body = np.tanh(body * 1.8) / np.tanh(1.8)                       # a little weight
    click = lowpass(_rng(1).standard_normal(len(t)) * np.exp(-t / 0.003), 3500)
    return norm(body + 0.25 * norm(click), 0.95)


def snare(seed=2):
    t = _t(0.35)
    tone = (np.sin(2 * np.pi * 185 * t) + 0.5 * np.sin(2 * np.pi * 330 * t)) * np.exp(-t / 0.07)
    noise = bandpass(_rng(seed).standard_normal(len(t)), 1200, 7000) * np.exp(-t / 0.13)
    return norm(0.55 * norm(tone) + norm(noise), 0.7)


def clap(seed=3):
    t = _t(0.35)
    noise = bandpass(_rng(seed).standard_normal(len(t)), 900, 4500)
    env = np.zeros(len(t))
    for start, decay in ((0.0, 0.01), (0.012, 0.01), (0.024, 0.14)):
        i = int(start * SR)
        env[i:] += np.exp(-t[: len(t) - i] / decay)
    return norm(noise * env, 0.45)


def hat(open_=False, seed=4):
    t = _t(0.3 if open_ else 0.08)
    noise = bandpass(_rng(seed).standard_normal(len(t)), 6000, 11000)
    return norm(noise * np.exp(-t / (0.12 if open_ else 0.025)), 0.27 if open_ else 0.2)


def shaker(seed=5):
    t = _t(0.09)
    noise = bandpass(_rng(seed).standard_normal(len(t)), 3500, 9000)
    return norm(noise * env_ar(t, 0.012, 0.03), 0.18)


def crash(seconds=2.6):
    t = _t(seconds)
    noise = bandpass(_rng(6).standard_normal(len(t)), 3000, 10000, 1)
    return norm(noise * np.exp(-t / 0.9), 0.22)


def boom():
    """The slam under a title card: a deep thud with a short burst of air."""
    t = _t(1.4)
    f = 38 + 70 * np.exp(-t / 0.08)
    body = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t / 0.5)
    air = lowpass(_rng(7).standard_normal(len(t)), 1800) * np.exp(-t / 0.09)
    return norm(norm(body) + 0.35 * norm(air), 0.9)


def swell(seconds):
    """A reversed cymbal: air that grows into the next downbeat."""
    t = _t(seconds)
    noise = bandpass(_rng(8).standard_normal(len(t)), 1500, 9000, 1)
    x = t / seconds
    return norm(noise * x ** 3, 0.26)


# ---------------------------------------------------------------------------
# Tonal voices
# ---------------------------------------------------------------------------

def epiano(midi, length, vel=0.8):
    """Two-operator FM: a warm Rhodes-like tine, brighter when played harder."""
    t = _t(length + 0.25)
    f = hz(midi)
    index = (0.4 + 1.6 * vel) * np.exp(-t / 0.4) + 0.15
    mod = np.sin(2 * np.pi * f * t)
    tone = np.sin(2 * np.pi * f * t + index * mod)
    tone += 0.12 * np.sin(2 * np.pi * 2 * f * t) * np.exp(-t / 0.25)    # the bark of a hard hit
    amp = env_ar(t, 0.003, 1.6) * gate(t, length + 0.2, 0.12)
    return tone * amp * vel


def epiano_chord(notes, length, vel):
    """Stereo, spread a touch, with a slow tremolo across the sides."""
    mono = sum(epiano(n, length, vel) for n in notes)
    t = _t(len(mono) / SR)
    trem = 0.5 + 0.5 * np.sin(2 * np.pi * 4.2 * t)
    left = mono * (0.75 + 0.25 * trem)
    right = mono * (0.75 + 0.25 * (1 - trem))
    return np.stack([left, right], axis=1) * 0.11


def bass(midi, length, vel=1.0):
    """A round synth bass: a saw through a lowpass that opens on the pluck, and a sine sub."""
    t = _t(length + 0.03)
    f = hz(midi)
    fc = 220 + 900 * vel * np.exp(-t / 0.09)
    s = np.zeros(len(t))
    k = 1
    while k * f < 3000:
        s += np.sin(2 * np.pi * k * f * t) / k / np.sqrt(1 + (k * f / fc) ** 4)
        k += 1
    s = 0.8 * s + 0.7 * np.sin(2 * np.pi * f * t)
    s = np.tanh(1.4 * s)
    return s * np.minimum(1, t / 0.004) * gate(t, length, 0.025) * 0.5 * vel


def pluck(midi, length, vel=1.0, seed=0, bright=2600):
    """Karplus-Strong: a nylon-ish guitar pluck, mellow on top."""
    f = hz(midi)
    total = int((length + 0.6) * SR)
    n = int(math.ceil(SR / f))
    rng = _rng(100 + seed)
    y = np.zeros(total + n + 1)
    burst = lowpass(rng.uniform(-1, 1, n), bright, 1)
    y[1:n + 1] = burst - burst.mean()      # no DC: a string doesn't hold an offset
    decay = 0.996
    i = n + 1
    while i < len(y):
        j = min(i + n, len(y))
        y[i:j] = decay * 0.5 * (y[i - n:j - n] + y[i - n - 1:j - n - 1])
        i = j
    y = y[1:]
    # The loop sounds at SR / (n + 0.5); stretch it onto the right pitch.
    ratio = f / (SR / (n + 0.5))
    src = np.arange(total) * ratio
    out = np.interp(src, np.arange(len(y)), y)
    t = _t(total / SR)
    out *= gate(t, length + 0.5, 0.15)
    return lowpass(out, 4500) * vel * 0.5


def brass(notes, length, vel=1.0, bend=0.0, vibrato=0.0, seed=0):
    """A small brass section: detuned saws with a filter that blooms on the attack."""
    t = _t(length + 0.15)
    fc = 500 + 2300 * vel * (np.minimum(1, t / 0.07) * (0.55 + 0.45 * np.exp(-t / 0.25)))
    out = np.zeros((len(t), 2))
    for ni, midi in enumerate(notes):
        for side, cents in ((0, -7), (1, 6), (0, 2), (1, -3)):
            f0 = hz(midi) * 2 ** (cents / 1200)
            fmod = f0 * (1 + bend * np.exp(-t / 0.06)) * (1 + vibrato * np.sin(2 * np.pi * 5.5 * t) * np.minimum(1, t / 0.3))
            phase = 2 * np.pi * np.cumsum(fmod) / SR + _rng(seed + ni * 7 + side).uniform(0, 6.28)
            k = 1
            while k * f0 < 6000:
                out[:, side] += np.sin(k * phase) / k / np.sqrt(1 + (k * f0 / fc) ** 4)
                k += 1
    amp = np.minimum(1, t / 0.025) * gate(t, length, 0.12)
    return out * amp[:, None] * 0.05 * vel


def sad_trombone(at_midi=58):
    """Wah, wah, wah, waaah: the knock-out, said with a trombone."""
    pieces = []
    for i, (st, dur) in enumerate(((0, 0.15), (-1, 0.15), (-2, 0.15), (-3, 0.5))):
        last = i == 3
        x = brass((at_midi + st,), dur, vel=0.85, bend=-0.03, vibrato=0.012 if last else 0.0, seed=40 + i)
        # A "wah" mute: the top end opens and closes once per note.
        tt = _t(len(x) / SR)
        wah = 0.55 + 0.45 * np.sin(np.pi * np.clip(tt / max(dur, 1e-3), 0, 1))
        pieces.append((sum(p[1] for p in pieces) if pieces else 0.0, x * wah[:, None]))
        pieces[-1] = (pieces[-1][0], pieces[-1][1], dur)
    total = sum(p[2] for p in pieces) + 0.3
    out = np.zeros((int(total * SR), 2))
    at = 0.0
    for _, x, dur in pieces:
        i = int(at * SR)
        n = min(len(x), len(out) - i)
        out[i:i + n] += x[:n]
        at += dur
    return lowpass(out, 3000)


# ---------------------------------------------------------------------------
# Mixing
# ---------------------------------------------------------------------------

def _add(buf, sound, at, gain=1.0, pan=0.0):
    i = int(round(at * SR))
    if i >= len(buf) or i < 0:
        return
    if sound.ndim == 1:
        a = (pan + 1) * math.pi / 4
        sound = np.stack([sound * math.cos(a), sound * math.sin(a)], axis=1) * math.sqrt(2)
    n = min(len(sound), len(buf) - i)
    buf[i:i + n] += sound[:n] * gain


def reverb(dry, seconds=2.2, decay=0.55, tone=5000, predelay=0.02, seed=9):
    t = _t(seconds)
    out = np.zeros_like(dry)
    for ch in range(2):
        ir = _rng(seed + ch).standard_normal(len(t)) * np.exp(-t / decay)
        ir = lowpass(ir, tone, 1)
        ir = np.concatenate([np.zeros(int(predelay * SR)), ir])
        ir /= np.sqrt((ir ** 2).sum())
        n = len(dry) + len(ir) - 1
        size = 1 << (n - 1).bit_length()
        wet = np.fft.irfft(np.fft.rfft(dry[:, ch], size) * np.fft.rfft(ir, size), size)[: len(dry)]
        out[:, ch] = wet
    return out


def compress(x, threshold=0.35, ratio=3.0, attack=0.005, release=0.12):
    """A gentle bus compressor: RMS follower, smoothed gain."""
    level = np.sqrt(np.maximum(1e-12, (x ** 2).mean(axis=1)))
    win = int(0.01 * SR)
    level = np.sqrt(np.convolve(level ** 2, np.ones(win) / win, mode="same"))
    over = np.maximum(level / threshold, 1.0)
    gain = over ** (1 / ratio - 1)
    # Attack fast, release slow: a one-pole on the gain, done in blocks to stay quick.
    smoothed = np.empty_like(gain)
    g = 1.0
    a_att = math.exp(-1 / (attack * SR / 64))
    a_rel = math.exp(-1 / (release * SR / 64))
    for b in range(0, len(gain), 64):
        target = gain[b:b + 64].min()
        g = a_att * g + (1 - a_att) * target if target < g else a_rel * g + (1 - a_rel) * target
        smoothed[b:b + 64] = g
    return x * smoothed[:, None]


# ---------------------------------------------------------------------------
# Arrangement
# ---------------------------------------------------------------------------

STEMS = {}


def render():
    total = BARS * BAR + 3.0
    n = int(total * SR)
    drums = np.zeros((n, 2))
    bassbus = np.zeros((n, 2))
    keys = np.zeros((n, 2))
    lead = np.zeros((n, 2))
    horns = np.zeros((n, 2))
    fx = np.zeros((n, 2))
    kick_env = np.zeros(n)
    K, SN, CL, HC, HO, SH, CR = kick(), snare(), clap(), hat(), hat(True), shaker(), crash()
    decay_curve = np.exp(-_t(0.25) / 0.09)

    def kick_at(at, g=1.0):
        _add(drums, K, at, g)
        i = int(at * SR)
        m = min(len(decay_curve), n - i)
        kick_env[i:i + m] = np.maximum(kick_env[i:i + m], decay_curve[:m] * g)

    for bar in range(BARS):
        t0 = bar * BAR
        chord = chord_of(bar)
        intro = bar in INTRO
        half = bar in HALFTIME
        last = bar >= 24

        if bar >= END_BAR:
            if bar == END_BAR:
                kick_at(t0)
                _add(drums, CR, t0, 1.4)
                _add(bassbus, bass(36, 3.5, 0.9), t0, 1.0)
                _add(keys, epiano_chord((48, 52, 55, 59, 62, 67), 3.6, 0.9), t0, 1.1)
                _add(horns, brass((48, 55, 60, 64, 67), 2.2, 1.0, seed=90), t0, 1.2)
                for k, m in enumerate((48, 55, 60, 64, 67, 72)):                 # a strum
                    _add(lead, pluck(m, 2.5, 0.7, seed=k), t0 + 0.018 * k, 0.8, pan=-0.4 + 0.16 * k)
            elif bar == END_BAR + 1:
                for step, m, ln in HOOK["C"]:
                    _add(keys, epiano_chord((m - 12,), ln * STEP + 0.3, 0.45), t0 + step * STEP, 0.7)
                _add(bassbus, bass(36, 1.6, 0.5), t0, 0.6)
            elif bar == END_BAR + 2:
                _add(keys, epiano_chord((48, 52, 55, 59, 64), 2.8, 0.5), t0, 0.8)
            continue

        if bar == BREAK_BAR:
            _add(keys, epiano_chord(VOICINGS["Fm"], 1.9, 0.75), t0, 1.0)
            _add(bassbus, bass(29, 1.0, 0.7), t0, 1.0)
            _add(keys, epiano_chord(VOICINGS["G"], 0.9, 0.7), t0 + 2 * BEAT, 0.9)
            _add(bassbus, bass(31, 0.9, 0.7), t0 + 2 * BEAT, 1.0)
            _add(fx, swell(2 * BEAT), t0 + 2 * BEAT, 1.0)
            continue

        if bar in DROPS:
            _add(drums, CR, t0, 1.0)
            _add(horns, brass(BRASS[chord], 0.55, 1.0, seed=bar), t0, 1.0)

        # ---- Drums ----
        if intro:
            for s in range(16):
                _add(drums, SH, t0 + s * STEP, (0.6, 0.3, 0.45, 0.3)[s % 4] * (0.5 + 0.5 * bar / 3), pan=0.35)
            if bar >= 2:
                kick_at(t0, 0.7)
                kick_at(t0 + 8 * STEP, 0.6)
            if bar == 3:
                for s in range(8, 16, 2):                       # a little fill into the drop
                    _add(drums, snare(20 + s), t0 + s * STEP, 0.25 + 0.05 * s)
                _add(fx, swell(BAR / 2), t0 + BAR / 2, 0.8)
        elif half:
            kick_at(t0)
            kick_at(t0 + 10 * STEP, 0.8)
            _add(drums, SN, t0 + 8 * STEP, 1.0)
            _add(drums, CL, t0 + 8 * STEP, 0.6, pan=-0.1)
            for s in range(0, 16, 2):
                _add(drums, HC, t0 + s * STEP, 0.6 if s % 4 == 2 else 0.4, pan=0.3)
        else:
            for s in (0, 6, 8) + ((11,) if bar % 2 else (14,)):
                kick_at(t0 + s * STEP, 1.0 if s in (0, 8) else 0.75)
            for s in (4, 12):
                _add(drums, SN, t0 + s * STEP, 1.0)
                _add(drums, CL, t0 + s * STEP, 0.55, pan=-0.1)
            if bar % 2:
                _add(drums, snare(30 + bar), t0 + 15 * STEP, 0.22)  # a ghost note
            for s in range(0, 16, 2):
                open_ = s == 14 and bar % 4 == 3
                _add(drums, HO if open_ else HC, t0 + s * STEP, 0.75 if s % 4 == 2 else 0.5, pan=0.3)
            for s in range(16):
                _add(drums, SH, t0 + s * STEP, (0.5, 0.25, 0.35, 0.25)[s % 4] * (1.2 if last else 1.0), pan=-0.35)
        if bar == BUILD_BAR:
            hits = [t0 + i * STEP * 2 for i in range(4)] + [t0 + BAR / 2 + i * STEP for i in range(8)] \
                + [t0 + 3 * BAR / 4 + i * STEP / 2 for i in range(8)]
            for i, at in enumerate(hits):
                _add(drums, snare(50 + i), at, 0.25 + 0.6 * i / len(hits), pan=0.05)
            _add(fx, swell(BAR), t0, 1.1)

        # ---- Bass ----
        root = ROOTS[chord]
        nxt = ROOTS[chord_of(bar + 1)] if bar + 1 < END_BAR else 36
        if intro:
            _add(bassbus, bass(root, BAR * 0.9, 0.45 + 0.1 * bar), t0, 0.8)
        elif half:
            for s, m, ln in ((0, root, 6), (10, root + 12, 2), (12, root, 4)):
                _add(bassbus, bass(m, ln * STEP, 0.9), t0 + s * STEP, 1.0)
        else:
            line = [(0, root, 3), (3, root, 1), (4, root + 12, 1), (6, root, 2), (8, root + 7, 2),
                    (10, root + 12, 1), (11, root, 1), (12, root, 2), (14, nxt - 1, 2)]
            for s, m, ln in line:
                _add(bassbus, bass(m, ln * STEP * 0.92, 1.0 if s % 4 == 0 else 0.8), t0 + s * STEP, 1.0)

        # ---- E-piano ----
        voicing = VOICINGS[chord]
        if intro:
            _add(keys, epiano_chord(voicing, BAR * 0.95, 0.55 + 0.08 * bar), t0, 1.0)
        else:
            for s, vel, ln in COMP:
                _add(keys, epiano_chord(voicing, ln * STEP, vel), t0 + s * STEP, 1.0)

        # ---- The hook ----
        if not intro and not half:
            for k, (s, m, ln) in enumerate(HOOK[chord]):
                _add(lead, pluck(m, ln * STEP, 0.85, seed=bar * 16 + k), t0 + s * STEP, 1.0, pan=0.2)
                if last:  # doubled an octave down for the last stretch: fuller, not higher
                    _add(lead, pluck(m - 12, ln * STEP, 0.6, seed=500 + bar * 16 + k), t0 + s * STEP, 0.8, pan=-0.25)
        elif half:
            # Just the answer of the hook, so the knock-out has room.
            for k, (s, m, ln) in enumerate(HOOK[chord][4:]):
                _add(lead, pluck(m, ln * STEP, 0.6, seed=900 + bar * 8 + k), t0 + s * STEP, 0.8, pan=0.2)

        # ---- Brass pushes in the last stretch ----
        if last and bar not in DROPS:
            _add(horns, brass(BRASS[chord], 0.22, 0.8, seed=200 + bar), t0 + 14 * STEP, 0.7)

    # ---- Moments ----
    for at in SLAMS:
        _add(fx, boom(), at, 1.0)
    _add(horns, sad_trombone(), OUT_AT, 3.0)
    for k, m in enumerate((60, 64, 67)):                       # da-da-da...
        _add(horns, brass((m, m - 12), 0.16, 1.0, seed=300 + k), WINNER_AT + k * 0.15, 1.1)
    _add(horns, brass((60, 64, 67, 72), 1.2, 1.0, vibrato=0.006, seed=310), WINNER_AT + 0.45, 1.2)  # ...DAAA
    _add(drums, CR, WINNER_AT + 0.45, 1.1)

    # ---- The mix ----
    duck = 1 - 0.45 * np.clip(kick_env, 0, 1)                  # the kick pushes the bass and keys aside
    bassbus *= duck[:, None]
    keys *= (1 - 0.25 * np.clip(kick_env, 0, 1))[:, None]
    bassbus = lowpass(highpass(bassbus, 35), 2500)
    keys = highpass(keys, 120)
    lead = highpass(lead, 150)
    # Balance: the hook and the e-piano carry the tune; kick and bass sit under them.
    G_DRUMS, G_BASS, G_KEYS, G_LEAD, G_HORNS = 0.6, 0.67, 2.0, 2.6, 1.4
    tonal = keys * G_KEYS + lead * G_LEAD + horns * G_HORNS
    mix = (drums * G_DRUMS + bassbus * G_BASS + tonal
           + reverb(keys * G_KEYS + lead * G_LEAD * 0.8 + horns * G_HORNS * 0.6) * 0.22
           + reverb(drums * G_DRUMS, 1.0, 0.25, 4000, 0.01, seed=11) * 0.05
           + fx * 0.9 + reverb(fx, 2.6, 0.8, 3500) * 0.25)
    global STEMS
    STEMS = {"drums": drums * G_DRUMS, "bass": bassbus * G_BASS, "keys": keys * G_KEYS, "lead": lead * G_LEAD,
             "horns": horns * G_HORNS, "fx": fx * 0.9}
    mix = mix[: int(BARS * BAR * SR)]
    # Master: no rumble, a soft top (nothing above ~12 kHz to be shrill), glue, soft clip.
    mix = highpass(mix, 28)
    mix = lowpass(mix, 12000, 1)
    mix = compress(norm(mix, 1.0), threshold=0.4, ratio=2.5)
    mix = np.tanh(norm(mix, 1.0) * 1.3) / np.tanh(1.3)
    fade = int(1.5 * SR)
    mix[-fade:] *= np.linspace(1, 0, fade)[:, None] ** 2
    return norm(mix, 0.7).astype(np.float32)


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
