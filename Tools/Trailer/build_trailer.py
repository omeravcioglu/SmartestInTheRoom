"""Builds the trailer: the game's own footage, title cards, original music and the host's voice.

  1. Record the footage (an explicit PlayMode test; needs graphics, so no -nographics):
       Unity.exe -batchmode -projectPath . -runTests -testPlatform PlayMode -testFilter TrailerCapture
     It writes Steam/Trailer/footage/<clip>/f00000.jpg ... and times.txt (when each frame was taken),
     60 frames to each second of game time.
  2. python Tools/Trailer/build_trailer.py [--footage DIR] [--work DIR] [--out FILE]
     needs ffmpeg on the PATH and numpy + Pillow.

Every clip is resampled to exactly 60 fps from its timestamps, the cards are drawn in the game's
fonts and colours, music.py writes the score to the same bars as the edit, and the voice lines
duck the music while they play. Output: H.264 1920 x 1080 at 60 fps with AAC stereo.
"""
import argparse
import bisect
import os
import shutil
import subprocess
import sys
import wave

import numpy as np
from PIL import Image, ImageDraw, ImageFont

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import music  # noqa: E402

FPS = 60
W, H = 1920, 1080
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
FONTS = os.path.join(ROOT, "Assets", "_Smartest", "Fonts")
VOICE = os.path.join(ROOT, "Assets", "_Smartest", "Audio", "Voice")

PAPER = (243, 237, 224)
INK = (26, 25, 22)
GOLD = (255, 194, 26)

# The edit, in order: (kind, what, where in the clip it starts, seconds). One bar is 2 s.
MONTAGE_ONE = ["m1-traffic", "m1-penalty", "m1-pop-lock", "m1-rhythm", "m1-herd",
               "m1-slice", "m1-fishing", "m1-juggle", "m1-flap", "m1-tightrope"]
# Ends on the two-left level, into "FIRST TO 100...".
MONTAGE_TWO = ["m2-statues", "m2-whack", "m2-keepy-uppy", "m2-goalie", "m2-stack"]
TIMELINE = (
    [("clip", "lobby", 0.0, 2.5),
     ("clip", "scores", 0.3, 2.5),
     ("zoom", "front-page", 0.2, 3.0),
     ("card", "69 MINIGAMES.", None, 2.0)]
    + [("clip", c, 0.15, 2.0) for c in MONTAGE_ONE]
    + [("card", "FAIL, AND\nYOU'RE OUT.", None, 2.0),
       ("clip", "knockout", 0.3, 4.0)]
    + [("clip", c, 0.15, 2.0) for c in MONTAGE_TWO]
    + [("card", "FIRST TO 100…", None, 2.0),
       ("clip", "closing", 0.1, 3.0),
       ("clip", "winner", 0.1, 5.0),
       ("end", None, None, 6.0)]
)

# The host, saying what the bubble on screen says: (seconds into the trailer, clip).
VOICE_CUES = [
    (0.25, "menu_welcome_2.mp3"),     # Look who it is. Get your friends in, first to a hundred points...
    (10.3, "minigame_start_2.mp3"),   # Minigame! Actual skill required, sorry.
    (34.2, "you_are_out_4.mp3"),      # Out! You're out. It's fine. It's fine. (after the trombone)
    (44.15, "minigame_last_two_1.mp3"),  # Two left! This is the good part.
    (48.1, "close_to_win_3.mp3"),     # One good round and this is over. Just saying.
    (52.2, "winner_1.mp3"),           # A HUNDRED! That's the game! Smartest in the room, officially... (after the fanfare)
]


# ---------------------------------------------------------------------------
# Pictures
# ---------------------------------------------------------------------------

def font(name, size):
    return ImageFont.truetype(os.path.join(FONTS, name), size)


def rules(draw, y):
    """The masthead's double rule: 6 px, a gap, 2 px."""
    draw.rectangle([96, y, W - 96, y + 6], fill=INK)
    draw.rectangle([96, y + 16, W - 96, y + 18], fill=INK)


def headline_layer(lines, max_width=1640, max_size=250):
    """Big caps in the game's display face, ink with a gold drop, centred on a clear layer."""
    size = max_size
    while size > 60:
        f = font("Archivo_ExtraCondensed-Black.ttf", size)
        widest = max(f.getlength(line) for line in lines)
        if widest <= max_width:
            break
        size -= 6
    f = font("Archivo_ExtraCondensed-Black.ttf", size)
    lead = int(size * 0.92)
    layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    top = H // 2 - (lead * len(lines)) // 2
    drop = max(6, int(size * 0.045))
    for i, line in enumerate(lines):
        width = f.getlength(line)
        x = (W - width) / 2
        y = top + i * lead - int(size * 0.12)
        d.text((x + drop, y + drop), line, font=f, fill=GOLD)
        d.text((x, y), line, font=f, fill=INK)
    return layer


def slam(base, layer, frames, path_for):
    """The layer lands on the page: it starts a little big and settles in a fifth of a second."""
    for i in range(frames):
        e = min(1.0, i / (0.2 * FPS))
        scale = 1.0 + 0.16 * (1 - e) ** 3
        frame = base.copy()
        if scale != 1.0:
            w, h = int(W * scale), int(H * scale)
            big = layer.resize((w, h), Image.LANCZOS)
            big = big.crop(((w - W) // 2, (h - H) // 2, (w - W) // 2 + W, (h - H) // 2 + H))
            frame.alpha_composite(big)
        else:
            frame.alpha_composite(layer)
        frame.convert("RGB").save(path_for(i), quality=95)


def card(text, frames, path_for):
    base = Image.new("RGBA", (W, H), PAPER + (255,))
    d = ImageDraw.Draw(base)
    rules(d, 150)
    rules(d, H - 168)
    slam(base, headline_layer(text.split("\n")), frames, path_for)


def end_card(frames, path_for):
    base = Image.new("RGBA", (W, H), PAPER + (255,))
    d = ImageDraw.Draw(base)
    rules(d, 150)
    rules(d, H - 168)
    layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ld = ImageDraw.Draw(layer)
    # "...IS THE" over the title, picking up the card before the race.
    small = font("Archivo_ExtraCondensed-Black.ttf", 96)
    lead_in = "…IS THE"
    ld.text(((W - small.getlength(lead_in)) / 2, 236), lead_in, font=small, fill=INK)
    title = headline_layer(["SMARTEST IN THE ROOM"], max_width=1700, max_size=240)
    layer.alpha_composite(title)
    # A tilted gold-on-ink sticker, the way the game labels things.
    label = "WISHLIST ON STEAM"
    lf = font("Archivo-Black.ttf", 58)
    pad_x, pad_y = 34, 18
    tw = int(lf.getlength(label))
    chip = Image.new("RGBA", (tw + pad_x * 2, 58 + pad_y * 2 + 8), INK + (255,))
    ImageDraw.Draw(chip).text((pad_x, pad_y - 4), label, font=lf, fill=GOLD)
    chip = chip.rotate(2, expand=True, resample=Image.BICUBIC)
    layer.alpha_composite(chip, ((W - chip.width) // 2, 760))
    slam(base, layer, frames, path_for)


def read_times(clip_dir):
    times = []
    with open(os.path.join(clip_dir, "times.txt")) as f:
        for line in f:
            parts = line.split()
            if len(parts) == 2:
                times.append(float(parts[1]))
    return times


def footage_frames(clip_dir, start, seconds):
    """The frame nearest each 1/60 s from `start`."""
    times = read_times(clip_dir)
    out = []
    for k in range(int(round(seconds * FPS))):
        t = start + k / FPS
        i = bisect.bisect_left(times, t)
        if i >= len(times) or (i > 0 and t - times[i - 1] <= times[i] - t):
            i -= 1
        i = max(0, min(len(times) - 1, i))
        out.append(os.path.join(clip_dir, f"f{i:05d}.jpg"))
    return out


def place(src, dst):
    try:
        os.link(src, dst)
    except OSError:
        shutil.copyfile(src, dst)


# ---------------------------------------------------------------------------
# Sound
# ---------------------------------------------------------------------------

def decode(path):
    raw = subprocess.run(["ffmpeg", "-v", "error", "-i", path, "-f", "f32le", "-ac", "2", "-ar", str(music.SR), "-"],
                         check=True, capture_output=True).stdout
    return np.frombuffer(raw, dtype="<f4").reshape(-1, 2).copy()


def mix_audio(seconds):
    n = int(seconds * music.SR)
    score = music.render()
    if len(score) < n:
        score = np.vstack([score, np.zeros((n - len(score), 2), np.float32)])
    score = score[:n]
    voice = np.zeros((n, 2), np.float32)
    duck = np.ones(n, np.float32)
    for at, name in VOICE_CUES:
        clip = decode(os.path.join(VOICE, name))
        clip *= 0.92 / (np.abs(clip).max() + 1e-9)
        i = int(at * music.SR)
        m = min(len(clip), n - i)
        voice[i:i + m] += clip[:m]
        a, b = max(0, i - int(0.06 * music.SR)), min(n, i + m + int(0.25 * music.SR))
        duck[a:b] = 0.38
    # Smooth the ducking so the music dips and comes back rather than switching.
    k = int(0.12 * music.SR)
    duck = np.convolve(duck, np.ones(k) / k, mode="same").astype(np.float32)
    mix = score * duck[:, None] + voice
    mix = np.tanh(mix * 1.1) / np.tanh(1.1)
    mix *= 0.89 / (np.abs(mix).max() + 1e-9)   # -1 dB: room for the AAC encode
    return mix


def write_wav(path, stereo):
    data = (np.clip(stereo, -1, 1) * 32767).astype("<i2")
    with wave.open(path, "wb") as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(music.SR)
        w.writeframes(data.tobytes())


# ---------------------------------------------------------------------------

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--footage", default=os.path.join(ROOT, "Steam", "Trailer", "footage"))
    ap.add_argument("--work", default=None)
    ap.add_argument("--out", default=os.path.join(ROOT, "Steam", "Trailer", "SmartestInTheRoom-Trailer.mp4"))
    args = ap.parse_args()
    work = args.work or os.path.join(os.path.dirname(args.footage), "build")
    seq = os.path.join(work, "seq")
    shutil.rmtree(seq, ignore_errors=True)
    os.makedirs(seq)

    n = 0
    total = 0.0
    for kind, what, start, seconds in TIMELINE:
        frames = int(round(seconds * FPS))

        def path_for(i, base=n):
            return os.path.join(seq, f"{base + i:05d}.jpg")

        if kind == "clip":
            for i, src in enumerate(footage_frames(os.path.join(args.footage, what), start, seconds)):
                place(src, path_for(i))
        elif kind == "zoom":
            # A still screen gets a slow push in, so it doesn't sit dead.
            for i, src in enumerate(footage_frames(os.path.join(args.footage, what), start, seconds)):
                s = 1.0 + 0.04 * i / max(1, frames - 1)
                im = Image.open(src)
                w, h = int(W * s), int(H * s)
                im = im.resize((w, h), Image.LANCZOS).crop(((w - W) // 2, (h - H) // 2, (w - W) // 2 + W, (h - H) // 2 + H))
                im.save(path_for(i), quality=95)
        elif kind == "card":
            card(what, frames, path_for)
        elif kind == "end":
            end_card(frames, path_for)
        print(f"{total:6.2f}s  {kind:5} {what or ''}")
        n += frames
        total += seconds

    video = os.path.join(work, "video.mp4")
    subprocess.run(["ffmpeg", "-y", "-v", "error", "-framerate", str(FPS), "-i", os.path.join(seq, "%05d.jpg"),
                    # Near lossless: Steam re-encodes uploads, so give it plenty to work from.
                    "-c:v", "libx264", "-preset", "slow", "-crf", "14", "-pix_fmt", "yuv420p", "-r", str(FPS), video],
                   check=True)
    audio = os.path.join(work, "mix.wav")
    write_wav(audio, mix_audio(total))
    os.makedirs(os.path.dirname(os.path.abspath(args.out)), exist_ok=True)
    subprocess.run(["ffmpeg", "-y", "-v", "error", "-i", video, "-i", audio, "-c:v", "copy", "-c:a", "aac", "-b:a", "192k",
                    "-shortest", "-movflags", "+faststart", args.out], check=True)
    print(f"{args.out}: {n} frames, {total:.1f} s")


if __name__ == "__main__":
    main()
