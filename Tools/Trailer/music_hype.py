"""The hype trailer's track: original, synthesised from numpy, nothing to license.

150 BPM, so a sixteenth is exactly 0.1 s and a beat exactly 24 frames at 60 fps: every cut in
build_hype.py lands on the grid. E minor (Em | C | G | D), up a whole tone to F# minor for the
last drop, ending on F# major. Warm on purpose: brass and saws through low filters, melodies that
stay round, hats kept down, the top of the master rolled off. The drums and bass carry the hype.

  bars  0-3   cold open   clock ticks, booms under the title slams ("8 FRIENDS." ...)
  bars  4-7   build       kick in, snare roll, riser; 3, 2, 1 hits; a beat of silence
  bars  8-15  drop        four on the floor, offbeat bass pumping, supersaw pad, the brass hook;
                          a tape stop at the very end
  bars 16-19  knock-out   "FAIL..." / "...YOU'RE OUT." booms, then half-time trap and 808s;
                          a sad trombone as the OUT! banner lands
  bars 20-23  build       back up, a slam for "FIRST TO 100", roll and riser into...
  bars 24-31  drop        F# minor: everything, bigger; a roll into the climax
  bars 32-37  climax      a braam on the winner, the title, and F# major to finish

render() returns float32 stereo at SR (60 s), peak about -3 dBFS; STEMS holds the parts for
checking the balance. Run this file to write music_hype.wav and print each stem's level.
"""
import math

import numpy as np

import music as base
from music import SR, _add, _rng, _t, bandpass, env_ar, gate, highpass, hz, lowpass, norm

BPM = 150
BEAT = 60.0 / BPM        # 0.4 s
BAR = 4 * BEAT           # 1.6 s
STEP = BEAT / 4          # 0.1 s
LENGTH = 60.0

COLD, BUILD1, DROP1, KO, BUILD2, DROP2, CLIMAX = (range(0, 4), range(4, 8), range(8, 16), range(16, 20),
                                                  range(20, 24), range(24, 32), range(32, 38))

# Moments the edit lands on (seconds).
SLAMS = (0.8, 2.4, 4.0)                  # "8 FRIENDS." / "69 MINIGAMES." / "1 WINNER."
COUNT = (11.2, 11.6, 12.0)               # 3, 2, 1
DROP1_AT, KO_FAIL, KO_OUT = 12.8, 25.6, 26.4
OUT_AT = 28.8                            # the OUT! banner lands
FIRST_TO = 35.2                          # "FIRST TO 100"
DROP2_AT, CLIMAX_AT, TITLE_AT, END_AT = 38.4, 51.2, 54.4, 56.0

CHORDS = {
    # name: (808 note, pad voicing) -- MIDI
    "Em": (28, (52, 55, 59, 64)), "C": (36, (48, 52, 55, 60)), "G": (31, (50, 55, 59, 62)),
    "D": (38, (50, 54, 57, 62)), "E": (28, (52, 56, 59, 64)),
    "F#m": (30, (54, 57, 61, 66)), "D2": (38, (50, 54, 57, 62)), "A": (33, (52, 57, 61, 64)),
    "E2": (28, (52, 56, 59, 64)), "F#": (30, (54, 58, 61, 66)),
}
LOOP_E = ["Em", "C", "G", "D"]
LOOP_FS = ["F#m", "D2", "A", "E2"]

# The brass hook, per chord in E minor, in sixteenths: (step, MIDI, length). Tops out at E5.
HOOK = {
    "Em": [(0, 64, 2), (3, 67, 1), (4, 71, 3), (8, 69, 2), (10, 67, 2), (12, 64, 4)],
    "C": [(0, 64, 2), (3, 67, 1), (4, 72, 3), (8, 71, 2), (10, 67, 2), (12, 64, 4)],
    "G": [(0, 62, 2), (3, 67, 1), (4, 74, 3), (8, 71, 2), (10, 67, 2), (12, 71, 4)],
    "D": [(0, 66, 2), (3, 69, 1), (4, 74, 3), (8, 76, 2), (10, 74, 2), (12, 69, 4)],
}
HOOK_FS = {"F#m": "Em", "D2": "C", "A": "G", "E2": "D"}   # the same hook, a tone up


def chord_of(bar):
    if bar in COLD or bar in BUILD1 or bar in DROP1 or bar in KO:
        return LOOP_E[bar % 4]
    if bar in BUILD2:
        return ("Em", "C", "D", "E")[bar - 20]
    if bar in DROP2:
        return LOOP_FS[bar % 4]
    return ("F#m", "D2", "E2", "F#", "F#", "F#")[bar - 32]


# ---------------------------------------------------------------------------
# Voices
# ---------------------------------------------------------------------------

def saw(freq, n, phase0=0.0):
    """A band-limited saw (polyBLEP), freq a number or an array of n values."""
    dt = np.broadcast_to(np.asarray(freq, dtype=float) / SR, (n,))
    phase = (phase0 + np.cumsum(dt)) % 1.0
    s = 2 * phase - 1
    m = phase < dt
    x = phase[m] / dt[m]
    s[m] -= x + x - x * x - 1
    m = phase > 1 - dt
    x = (phase[m] - 1) / dt[m]
    s[m] -= x * x + x + x + 1
    return s


def kick():
    t = _t(0.55)
    f = 46 + 150 * np.exp(-t / 0.022) + 30 * np.exp(-t / 0.12)
    body = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t / 0.3)
    body = np.tanh(body * 2.4) / np.tanh(2.4)
    click = lowpass(_rng(11).standard_normal(len(t)) * np.exp(-t / 0.0025), 4000)
    return norm(body + 0.28 * norm(click), 1.0)


def sub808(midi, length, glide_from=None, glide=0.08):
    """A long 808: a sine (with a hint of its octave, for small speakers) that can slide in."""
    t = _t(length + 0.05)
    f1 = hz(midi)
    f = f1 + (hz(glide_from) - f1) * np.exp(-t / glide) if glide_from is not None else np.full(len(t), f1)
    ph = 2 * np.pi * np.cumsum(f) / SR
    s = np.tanh(1.7 * (np.sin(ph) + 0.2 * np.sin(2 * ph)))
    amp = np.minimum(1, t / 0.004) * np.exp(-t / max(0.5, length)) * gate(t, length, 0.06)
    return s * amp * 0.55


def offbass(midi, length=0.18):
    """The pumping offbeat bass: a saw through a low filter, plucked."""
    t = _t(length + 0.02)
    s = lowpass(saw(hz(midi), len(t)) * env_ar(t, 0.003, 0.12) * gate(t, length, 0.02), 900, 2)
    s += 0.6 * np.sin(2 * np.pi * hz(midi) * t) * env_ar(t, 0.003, 0.15)
    return np.tanh(1.5 * s) * 0.45


def pad(notes, length, fc=1600.0, attack=0.12, release=0.35, seed=0):
    """A supersaw pad, five detuned saws a note, spread across the sides, kept dark."""
    t = _t(length + release)
    n = len(t)
    out = np.zeros((n, 2))
    for ni, m in enumerate(notes):
        for v in range(5):
            cents = (v - 2) * 9
            s = saw(hz(m) * 2 ** (cents / 1200), n, _rng(seed + ni * 13 + v).uniform(0, 1))
            out[:, v % 2] += s * (1.0 if v == 2 else 0.7)
    out = lowpass(out, fc, 2)
    amp = np.minimum(1, t / attack) * gate(t, length + release * 0.5, release)
    return out * amp[:, None] * 0.05


def braam(notes, length=2.4, seed=0):
    """A trailer braam: low brass, pushed hard, with the sub under it."""
    x = base.brass(notes, length, 1.0, seed=seed)
    x = np.tanh(norm(x, 1.0) * 3.5)
    x = lowpass(x, 1700, 2)
    t = _t(len(x) / SR)
    sub = np.sin(2 * np.pi * hz(min(notes) - 12) * t) * np.exp(-t / 1.2)
    return norm(x * env_ar(t, 0.02, 1.4)[:, None] + 0.45 * sub[:, None], 0.9)


def tom(f0=110, decay=0.35, seed=0):
    t = _t(0.8)
    f = f0 * (0.62 + 0.38 * np.exp(-t / 0.08))
    body = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t / decay)
    hit = lowpass(_rng(30 + seed).standard_normal(len(t)), 2200) * np.exp(-t / 0.02)
    return norm(norm(body) + 0.3 * norm(hit), 0.9)


def subdrop(seconds=1.4):
    t = _t(seconds)
    f = 30 + 45 * np.exp(-t / 0.35)
    return np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t / 0.7) * 0.8


def tick(tock=False, seed=0):
    """A stopwatch tick, kept under 4 kHz."""
    t = _t(0.07)
    f = 1350 if tock else 1800
    s = np.sin(2 * np.pi * f * t) * np.exp(-t / 0.006)
    s += 0.6 * bandpass(_rng(40 + seed).standard_normal(len(t)), 1200, 3800) * np.exp(-t / 0.004)
    return norm(s, 0.3)


def heartbeat():
    t = _t(0.6)
    one = np.sin(2 * np.pi * 58 * t) * np.exp(-t / 0.09)
    out = one.copy()
    i = int(0.19 * SR)
    out[i:] += 0.75 * one[: len(out) - i]
    return norm(lowpass(out, 300), 0.8)


def riser(seconds, seed=0):
    """Air and a rising saw growing into the next downbeat."""
    t = _t(seconds)
    n = len(t)
    x = t / seconds
    noise = _rng(50 + seed).standard_normal(n)
    out = np.zeros(n)
    chunks = 24
    for c in range(chunks):
        a, b = c * n // chunks, (c + 1) * n // chunks
        lo = 400 + 3200 * (c / chunks) ** 1.5
        seg = bandpass(noise[max(0, a - 2000):b], lo, lo * 2.2)[-(b - a):]
        out[a:b] = seg
    out *= x ** 2.2
    tone = lowpass(saw(110 * 2 ** (3 * x), n), 2400, 2) * x ** 2.5
    return norm(0.8 * norm(out) + 0.5 * norm(tone), 0.5)


def tape_stop(buf, start, length):
    """The record slows to a stop: read the next `length` seconds ever slower, falling in pitch."""
    i0, n = int(start * SR), int(length * SR)
    src = buf[i0:i0 + n].copy()
    t = np.arange(n) / SR
    pos = (t - t ** 2 / (2 * length)) * SR
    for ch in range(2):
        buf[i0:i0 + n, ch] = np.interp(pos, np.arange(n), src[:, ch])
    buf[i0:i0 + n] *= (np.linspace(1, 0, n) ** 0.6)[:, None]


# ---------------------------------------------------------------------------
# Arrangement
# ---------------------------------------------------------------------------

STEMS = {}


def render():
    n = int((LENGTH + 2.0) * SR)
    drums, bass, pads, horns, fx = (np.zeros((n, 2)) for _ in range(5))
    kick_env = np.zeros(n)
    K, SN, CL = kick(), base.snare(), base.clap()
    HO, SH, CR = base.hat(True), base.shaker(), base.crash(3.0)
    pump = 1 - np.exp(-_t(0.3) / 0.11)   # the duck a kick leaves, back up in a third of a second

    def kick_at(at, g=1.0):
        _add(drums, K, at, g)
        i = int(at * SR)
        m = min(len(pump), n - i)
        kick_env[i:i + m] = np.maximum(kick_env[i:i + m], (1 - pump[:m]) * g)

    def impact(at, size=1.0, crash=True):
        _add(fx, base.boom(), at, 1.0 * size)
        _add(fx, subdrop(), at, 0.8 * size)
        if crash:
            _add(drums, CR, at, 1.1 * size)

    def roll(t0, t1, start_gap, end_gap, g0=0.25, g1=0.9, seed=0):
        """A snare roll speeding up from one hit every start_gap to every end_gap."""
        at, k = t0, 0
        while at < t1 - 1e-6:
            x = (at - t0) / (t1 - t0)
            _add(drums, base.snare(60 + seed + k), at, g0 + (g1 - g0) * x, pan=0.05)
            at += start_gap + (end_gap - start_gap) * x
            k += 1

    def four_on_floor(bar, t0, big=False):
        chord = chord_of(bar)
        for s in (0, 4, 8, 12):
            kick_at(t0 + s * STEP)
        if bar % 2 == 1:
            kick_at(t0 + 14 * STEP, 0.6)
        for s in (4, 12):
            _add(drums, SN, t0 + s * STEP, 0.9)
            _add(drums, CL, t0 + s * STEP, 0.75 if big else 0.55, pan=-0.1)
        for s in (2, 6, 10, 14):
            _add(drums, HO, t0 + s * STEP, 0.55, pan=0.3)
        for s in range(16):
            _add(drums, SH, t0 + s * STEP, (0.45, 0.2, 0.3, 0.2)[s % 4], pan=-0.3)
        root, voicing = CHORDS[chord]
        _add(bass, sub808(root, BAR * 0.95, glide_from=root + 5 if bar % 4 == 0 else None), t0, 0.9)
        for s in (2, 6, 10, 14):
            _add(bass, offbass(root + 12), t0 + s * STEP, 1.0)
        _add(pads, pad(voicing, BAR, seed=bar), t0, 1.0)
        hook = HOOK[HOOK_FS.get(chord, chord)]
        shift = 2 if chord in HOOK_FS else 0
        for k, (s, m, ln) in enumerate(hook):
            _add(horns, base.brass((m + shift,), ln * STEP * 0.9, 0.95, seed=bar * 10 + k), t0 + s * STEP, 1.0, pan=0.12)
            if big:  # fuller, not higher: the hook doubled an octave down
                _add(horns, base.brass((m + shift - 12,), ln * STEP * 0.9, 0.8, seed=500 + bar * 10 + k),
                     t0 + s * STEP, 0.75, pan=-0.15)
        if bar % 4 == 3:   # a fill into every fourth bar
            roll(t0 + 12 * STEP, t0 + BAR, STEP, STEP / 2, 0.3, 0.7, seed=bar)

    def half_time(bar, t0):
        chord = chord_of(bar)
        root, voicing = CHORDS[chord]
        for s in (0, 3, 10):
            kick_at(t0 + s * STEP, 1.0 if s == 0 else 0.8)
        _add(drums, SN, t0 + 8 * STEP, 1.0)
        _add(drums, CL, t0 + 8 * STEP, 0.8, pan=-0.1)
        for s in range(0, 16, 2):
            _add(drums, base.hat(False, 70 + s), t0 + s * STEP, 0.5 if s % 4 else 0.35, pan=0.3)
        for k in range(3):                                   # a triplet roll on the hats
            _add(drums, base.hat(False, 90 + k), t0 + 14 * STEP + k * STEP * 2 / 3, 0.4, pan=0.3)
        _add(bass, sub808(root, 0.7), t0, 1.0)
        _add(bass, sub808(root + 3, 0.5, glide_from=root), t0 + 10 * STEP, 0.9)
        _add(pads, pad(voicing, BAR, fc=1100, seed=300 + bar), t0, 0.7)

    for bar in range(38):
        t0 = bar * BAR
        chord = chord_of(bar)
        root, voicing = CHORDS[chord]

        if bar in COLD:
            # Ticking, a low pad holding its breath, booms under the slams.
            for s in range(0, 16, 2):
                _add(fx, tick(s % 4 == 2, s), t0 + s * STEP, 0.7 if s % 4 == 0 else 0.5, pan=0.2)
            _add(pads, pad((40, 47, 52), BAR, fc=700, attack=0.6, seed=bar), t0, 0.6 + 0.15 * bar)
            if bar == 3:
                for s in (0, 4, 8, 12):
                    _add(drums, heartbeat(), t0 + s * STEP, 0.9)
                _add(fx, riser(BAR, 1), t0, 0.7)
        elif bar in BUILD1:
            k = bar - 4
            if bar < 7:
                for s in (0, 4, 8, 12):
                    kick_at(t0 + s * STEP, 0.55 + 0.12 * k)
                if k >= 1:
                    for s in (4, 12):
                        _add(drums, CL, t0 + s * STEP, 0.5, pan=-0.1)
                    for s in (2, 6, 10, 14):
                        _add(bass, offbass(root + 12), t0 + s * STEP, 0.5 + 0.2 * k)
                _add(pads, pad(voicing, BAR, fc=900 + 350 * k, seed=bar), t0, 0.8)
                if bar == 6:
                    roll(t0, t0 + BAR, 2 * STEP, STEP, 0.25, 0.6, seed=6)
                    _add(fx, riser(2 * BAR - BEAT, 2), t0, 1.0)
            else:
                # 3, 2, 1: three big toms, then a beat of nothing before the drop.
                for i, at in enumerate(COUNT):
                    _add(drums, tom(95 - 12 * i, seed=i), at, 0.9 + 0.1 * i)
                    _add(fx, base.boom(), at, 0.5 + 0.15 * i)
                _add(pads, pad(voicing, 3 * BEAT, fc=1800, seed=bar), t0, 0.8)
                _add(fx, base.swell(BEAT), t0 + 3 * BEAT, 1.0)
        elif bar in DROP1:
            four_on_floor(bar, t0)
        elif bar in KO:
            if bar == 16:
                _add(bass, sub808(28, 2 * BAR, glide_from=40, glide=0.4), t0, 0.7)
                for s in (8, 12):
                    _add(drums, heartbeat(), t0 + s * STEP, 0.8)
            elif bar == 17:
                half_time(bar, t0)
            elif bar == 18:
                # The banner lands: one hit, the trombone says it, the beat comes back under the host.
                kick_at(t0)
                _add(bass, sub808(root, 1.2), t0, 0.9)
                _add(pads, pad(voicing, BAR, fc=900, seed=318), t0, 0.5)
                _add(drums, SN, t0 + 12 * STEP, 0.8)
                for s in (12, 14):
                    _add(drums, base.hat(False, 120 + s), t0 + s * STEP, 0.4, pan=0.3)
            else:
                half_time(bar, t0)
                _add(fx, riser(BAR, 3), t0, 0.6)
        elif bar in BUILD2:
            k = bar - 20
            if bar < 23:
                for s in (0, 4, 8, 12):
                    kick_at(t0 + s * STEP, 0.65 + 0.1 * k)
                for s in (4, 12):
                    _add(drums, CL, t0 + s * STEP, 0.5 + 0.1 * k, pan=-0.1)
                for s in (2, 6, 10, 14):
                    _add(bass, offbass(root + 12), t0 + s * STEP, 0.7 + 0.1 * k)
                _add(bass, sub808(root, BAR * 0.9), t0, 0.6)
                _add(pads, pad(voicing, BAR, fc=1000 + 300 * k, seed=bar), t0, 0.85)
                if bar == 22:
                    roll(t0, t0 + BAR, 2 * STEP, STEP, 0.25, 0.6, seed=22)
                    _add(fx, riser(2 * BAR - BEAT, 4), t0, 1.0)
            else:
                roll(t0, t0 + 3 * BEAT, STEP, STEP / 2, 0.5, 1.0, seed=23)
                _add(pads, pad(voicing, 3 * BEAT, fc=2000, seed=bar), t0, 0.9)
                _add(bass, sub808(root, 3 * BEAT), t0, 0.7)
                _add(fx, base.swell(BEAT), t0 + 3 * BEAT, 1.0)
        elif bar in DROP2:
            four_on_floor(bar, t0, big=True)
            if bar == 31:
                roll(t0, t0 + BAR, STEP, STEP / 2, 0.4, 1.0, seed=31)
                _add(fx, riser(BAR, 5), t0, 0.9)
        else:
            # The climax: a braam on the winner, held chords under the host, the title, F# major.
            if bar == 32:
                _add(horns, braam((42, 49, 54), 3.0, seed=1), t0, 1.0)
                _add(bass, sub808(30, 2 * BAR), t0, 0.9)
                for s in (0, 8):
                    _add(drums, tom(70, 0.5, seed=10 + s), t0 + s * STEP, 0.8)
            if bar in (32, 33):
                _add(pads, pad(voicing, BAR, fc=1300, seed=400 + bar), t0, 0.8)
                _add(horns, base.brass(voicing[1:], BAR * 0.9, 0.7, seed=600 + bar), t0, 0.7)
            if bar == 33:
                for s in (0, 8, 12):
                    _add(drums, tom(80, 0.4, seed=20 + s), t0 + s * STEP, 0.7)
            if bar == 34:   # the title
                _add(horns, braam((40, 47, 52), 2.0, seed=2), t0, 0.9)
                _add(pads, pad(voicing, BAR, fc=1500, seed=434), t0, 0.9)
                _add(bass, sub808(28, BAR), t0, 0.9)
                roll(t0 + 2 * BEAT, t0 + BAR, STEP, STEP / 2, 0.3, 0.9, seed=34)
            if bar == 35:   # WISHLIST, and F# major
                _add(horns, braam((42, 49, 54, 58), 3.5, seed=3), t0, 1.0)
                _add(horns, base.brass((54, 58, 61, 66), 3.2, 1.0, vibrato=0.004, seed=650), t0, 1.0)
                _add(pads, pad(CHORDS["F#"][1], 3.6, fc=1500, attack=0.05, release=1.2, seed=435), t0, 1.0)
                _add(bass, sub808(30, 3.0), t0, 1.0)
                kick_at(t0)

    # ---- Moments ----
    for at in SLAMS:
        impact(at, 0.8, crash=False)
    impact(DROP1_AT, 1.0)
    _add(horns, braam((40, 47, 52), 1.6, seed=4), DROP1_AT, 0.7)
    tape_stop(drums, DROP2_AT - BAR * 8 - 0.4 - 12.8 + 25.2 - 25.2 + 25.2, 0.4) if False else None
    for buf in (drums, bass, pads, horns):
        tape_stop(buf, KO_FAIL - 0.4, 0.4)
    impact(KO_FAIL, 1.0, crash=False)
    impact(KO_OUT, 0.9, crash=False)
    impact(OUT_AT, 1.0)
    _add(horns, base.sad_trombone(58), OUT_AT + 0.05, 3.0)
    impact(FIRST_TO, 0.9)
    impact(DROP2_AT, 1.1)
    _add(horns, braam((42, 49, 54), 1.6, seed=5), DROP2_AT, 0.7)
    impact(CLIMAX_AT, 1.2)
    impact(TITLE_AT, 1.0)
    impact(END_AT, 1.2)

    # ---- The mix ----
    side = np.clip(kick_env, 0, 1)
    bass *= (1 - 0.55 * side)[:, None]
    pads *= (1 - 0.6 * side)[:, None]
    horns *= (1 - 0.15 * side)[:, None]
    bass = lowpass(highpass(bass, 28), 2200)
    pads = highpass(pads, 140)
    horns = highpass(horns, 110)
    G = {"drums": 0.75, "bass": 0.95, "pads": 1.6, "horns": 1.7, "fx": 0.85}
    stems = {"drums": drums * G["drums"], "bass": bass * G["bass"], "pads": pads * G["pads"],
             "horns": horns * G["horns"], "fx": fx * G["fx"]}
    tonal = stems["pads"] + stems["horns"]
    mix = (sum(stems.values())
           + base.reverb(tonal, 2.4, 0.6, 4500) * 0.2
           + base.reverb(stems["drums"], 1.2, 0.3, 4000, 0.01, seed=12) * 0.07
           + base.reverb(stems["fx"], 3.0, 0.9, 3000) * 0.25)
    global STEMS
    STEMS = {k: v[: int(LENGTH * SR)] for k, v in stems.items()}
    mix = mix[: int(LENGTH * SR)]
    # Master: no rumble, a soft top so nothing is shrill, glue, then a soft clip for weight.
    mix = highpass(mix, 26)
    mix = lowpass(mix, 11000, 1)
    mix = base.compress(norm(mix, 1.0), threshold=0.38, ratio=3.0, attack=0.004, release=0.15)
    mix = np.tanh(norm(mix, 1.0) * 1.5) / np.tanh(1.5)
    fade = int(2.0 * SR)
    mix[-fade:] *= np.linspace(1, 0, fade)[:, None] ** 2
    return norm(mix, 0.7).astype(np.float32)


def report(mix):
    """Each stem's level by section, and the master's energy by band: the balance, without ears."""
    bands = ((20, 120), (120, 500), (500, 2000), (2000, 6000), (6000, 16000))
    spec = np.abs(np.fft.rfft(mix.mean(axis=1))) ** 2
    f = np.fft.rfftfreq(len(mix), 1 / SR)
    total = spec.sum()
    print("master band energy:", "  ".join(f"{lo}-{hi} Hz {100 * spec[(f >= lo) & (f < hi)].sum() / total:4.1f}%"
                                           for lo, hi in bands))
    sections = (("cold", 0, 6.4), ("build", 6.4, 12.8), ("drop", 12.8, 25.6), ("ko", 25.6, 32), ("build2", 32, 38.4),
                ("drop2", 38.4, 51.2), ("climax", 51.2, 60))
    print("rms dB    " + " ".join(f"{s:>7}" for s, _, _ in sections))
    for name, x in list(STEMS.items()) + [("MASTER", mix)]:
        row = []
        for _, a, b in sections:
            seg = x[int(a * SR):int(b * SR)]
            row.append(20 * np.log10(np.sqrt((seg ** 2).mean()) + 1e-9))
        print(f"{name:8}  " + " ".join(f"{v:7.1f}" for v in row))


if __name__ == "__main__":
    import sys

    out = sys.argv[1] if len(sys.argv) > 1 else "music_hype.wav"
    mix = render()
    base.write_wav(out, mix)
    report(mix)
    print("wrote", out)
