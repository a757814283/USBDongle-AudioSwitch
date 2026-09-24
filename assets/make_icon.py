# -*- coding: utf-8 -*-
"""
Builds the app icons for USBDongle_AudioSwitch.

Three icons ship, and they are deliberately different:

  Resources/app.ico         black line art on an *opaque white* background.
                            Used for the window, its taskbar button, Alt-Tab
                            and Explorer. The white plate is what makes one
                            file work on both Windows themes: against a dark
                            taskbar the plate reads as a white icon, against a
                            light one the plate disappears into the background
                            and only the black drawing is left. No theme
                            detection is needed for this one.

  Resources/tray-white.ico  white line art on transparency. The notification
  Resources/tray-black.ico  area is drawn over the taskbar, so it needs real
                            alpha - an opaque plate there would show up as a
                            white square in the tray. Unlike app.ico the tray
                            has no plate to carry the contrast, so the ink
                            itself has to flip with the taskbar: white on the
                            dark theme, black on the light one. AppIcons picks
                            between the two from SystemUsesLightTheme.

Two art tracks, because one drawing cannot serve every size:

  * >= 40px  the original line art
  * <= 32px  a simplified glyph - the original's double-shell ear cups and
             thin strokes collapse into mush at tray size (16px), so small
             sizes get bold filled shapes with no interior detail.

Art sources live in assets/ as multi-resolution .ico masters (art-black.ico,
art-white.ico). The original 2048px JPEG those masters were traced from is not
carried in the repo; when it is absent - the normal case - the art is reused
verbatim from the masters, so a rebuild is byte-stable. Drop the JPEG back into
assets/ to regenerate the large sizes from the original drawing instead.
"""
import os
import struct
import numpy as np
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)

SOURCE = os.path.join(HERE, "USBDongle_AudioSwitch_icon.jpeg")
ART_BLACK = os.path.join(HERE, "art-black.ico")
ART_WHITE = os.path.join(HERE, "art-white.ico")

# Icon sizes Windows asks for. 16/20/24/32 are tray + small shell views,
# 48/64 taskbar and alt-tab, 128/256 Explorer extra-large.
SIZES = [16, 20, 24, 32, 40, 48, 64, 128, 256]

# Sizes that get the simplified glyph instead of the original artwork.
SIMPLE_MAX = 32

# --- geometry measured off the source image (2048px scale) -------------------
# Crop square was centred on the artwork so the composition keeps its margins.
CROP_SIDE = 1120
CROP_CX, CROP_CY = 1024, 1023


def extract_alpha():
    """Luminance -> alpha, so anti-aliased line edges survive as soft alpha."""
    lum = np.asarray(Image.open(SOURCE).convert("L")).astype(np.float32)
    alpha = 255.0 - lum
    alpha[alpha < 8] = 0.0          # drop JPEG noise in the paper
    x0 = CROP_CX - CROP_SIDE // 2
    y0 = CROP_CY - CROP_SIDE // 2
    return alpha[y0:y0 + CROP_SIDE, x0:x0 + CROP_SIDE]


def render_original(size, colour):
    """Original artwork, downsampled with a proper box filter."""
    sub = extract_alpha()
    px = np.zeros((CROP_SIDE, CROP_SIDE, 4), np.uint8)
    px[..., 0], px[..., 1], px[..., 2] = colour
    px[..., 3] = np.clip(sub, 0, 255).astype(np.uint8)
    return Image.fromarray(px, "RGBA").resize((size, size), Image.LANCZOS)


def render_simple(size, colour, with_power):
    """
    Small-size glyph.

    Deliberately NOT a scaled-down copy of the source: in the original the ear
    cups span only ~22% of the width, which at 16px is under 4px of mush, and
    the resulting blob reads as a face rather than headphones. This is a
    recomposed drawing instead - the phones fill the frame, the cups are large
    solid shapes, and there is no interior line work to lose.
    """
    ss = 8
    S = size * ss
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    fill = colour + (255,)

    # Headband spanning most of the frame.
    cx, cy = 0.5 * S, 0.47 * S
    r = 0.325 * S
    d.arc([cx - r, cy - r, cx + r, cy + r], start=185, end=355,
          fill=fill, width=max(2, int(round(0.115 * S))))

    # Ear cups: solid capsules, big enough to survive 16px.
    for ccx in (0.225, 0.775):
        cw, ch = 0.27 * S, 0.34 * S
        cup = Image.new("RGBA", (int(cw), int(ch)), (0, 0, 0, 0))
        ImageDraw.Draw(cup).rounded_rectangle(
            [0, 0, cup.width - 1, cup.height - 1],
            radius=int(cw * 0.45), fill=fill)
        cup = cup.rotate(-12, resample=Image.BICUBIC, expand=True)
        img.alpha_composite(cup, (int(ccx * S - cup.width / 2),
                                  int(0.645 * S - cup.height / 2)))

    if with_power:
        px, py, pr = 0.5 * S, 0.862 * S, 0.082 * S
        d.arc([px - pr, py - pr, px + pr, py + pr], start=-55, end=235,
              fill=fill, width=max(2, int(round(0.082 * S))))
        bw = max(2, int(round(0.060 * S)))
        d.rounded_rectangle([px - bw / 2, py - pr * 1.45,
                             px + bw / 2, py - pr * 0.25],
                            radius=bw / 2, fill=fill)

    return img.resize((size, size), Image.LANCZOS)


def load_master_frame(path, size):
    """
    One frame out of a multi-resolution master .ico.

    Used for every size whenever the source JPEG is unavailable, which is the
    normal case: the masters already hold the artwork at each size, including
    the hand-composed small glyphs, so reusing them keeps a rebuild stable
    instead of silently degrading the icon.
    """
    if not os.path.exists(path):
        raise SystemExit(
            "no art for %dpx: neither %s nor the master %s exists"
            % (size, os.path.basename(SOURCE), path))

    icon = Image.open(path)
    available = sorted(w for w, _ in icon.ico.sizes())
    if size not in available:
        raise SystemExit("%s has no %dpx frame (has %s)"
                         % (path, size, available))

    icon.size = (size, size)
    return icon.convert("RGBA")


def art_for(size, colour, master):
    """RGBA line art at the given size, drawn in `colour`."""
    if os.path.exists(SOURCE):
        if size <= SIMPLE_MAX:
            # The power ring is only worth drawing once there is room for it.
            return render_simple(size, colour, with_power=(size >= 24))
        return render_original(size, colour)

    return load_master_frame(master, size)


def on_opaque_background(art, background):
    """
    Flattens the line art onto a solid plate.

    This is what makes the app icon background opaque. Without it the icon is
    transparent line art, which vanishes against a dark taskbar; with it the
    plate itself carries the contrast and the same file works on both themes.
    """
    plate = Image.new("RGBA", art.size, tuple(background) + (255,))
    plate.alpha_composite(art)
    return plate


def write_ico(frames, path):
    """
    Hand-rolled .ico writer.

    Pillow's own ICO save cannot be used here: it ignores the images it is
    given and re-thumbnails a single base image into every requested size,
    which would discard the whole point of this script (different artwork for
    small sizes).

    Frames are written as uncompressed 32bpp BMP/DIB rather than PNG. PNG
    frames are legal in an ICO and smaller, but System.Drawing.Icon - which is
    what loads these at runtime - has historically mishandled them. DIB costs
    bytes and buys certainty.
    """
    entries, blobs = [], []
    for im in frames:
        w, h = im.size
        px = im.convert("RGBA")

        # BITMAPINFOHEADER. biHeight is doubled: the DIB also carries an AND
        # mask below the colour data.
        header = struct.pack("<IiiHHIIiiII",
                             40, w, h * 2, 1, 32, 0, w * h * 4, 0, 0, 0, 0)

        # Colour data is BGRA, bottom-up.
        bgra = np.asarray(px)[..., [2, 1, 0, 3]]
        xor = bgra[::-1].tobytes()

        # 1bpp AND mask, rows padded to 4 bytes, bottom-up. All zero means
        # "consult the alpha channel", which is what we want.
        row_bytes = ((w + 31) // 32) * 4
        mask = bytes(row_bytes * h)

        blobs.append(header + xor + mask)
        entries.append((w, h))

    offset = 6 + 16 * len(entries)
    out = bytearray(struct.pack("<HHH", 0, 1, len(entries)))
    for (w, h), blob in zip(entries, blobs):
        # 0 encodes 256 in an ICO directory entry.
        out += struct.pack("<BBBBHHII",
                           w if w < 256 else 0, h if h < 256 else 0,
                           0, 0, 1, 32, len(blob), offset)
        offset += len(blob)
    for blob in blobs:
        out += blob

    with open(path, "wb") as fh:
        fh.write(out)
    return len(out)


def build(colour, out_path, master, sizes=None, background=None,
          preview_pngs=False):
    frames = []
    for s in (sizes or SIZES):
        art = art_for(s, colour, master)
        if background is not None:
            art = on_opaque_background(art, background)
        frames.append(art)

    size = write_ico(frames, out_path)
    print("wrote %s  %d frames %s  %s  %.0f KB"
          % (out_path, len(frames), sizes or SIZES,
             "opaque %s" % (background,) if background else "transparent",
             size / 1024.0))

    if preview_pngs:
        for s in (256, 48, 16):
            f = [x for x in frames if x.width == s][0]
            f.save(os.path.join(HERE, "%s-%d.png"
                                % (os.path.splitext(os.path.basename(out_path))[0], s)))
    return frames


if __name__ == "__main__":
    res = os.path.join(ROOT, "Resources")
    os.makedirs(res, exist_ok=True)

    # Window, taskbar button, Alt-Tab and Explorer. Black line art on an
    # opaque white plate: the plate is what keeps the icon legible on the dark
    # taskbar, and it disappears into a light one. Explorer's "extra large
    # icons" view needs the 256px frame.
    build((0, 0, 0), os.path.join(res, "app.ico"), ART_BLACK,
          sizes=[16, 20, 24, 32, 40, 48, 64, 128, 256],
          background=(255, 255, 255))

    # Notification area. Line art on transparency - an opaque plate here would
    # show as a square sitting in the tray. The notification area never asks for
    # more than 48px, and DIB frames are bulky, so stop there; 48px still covers
    # a 300% DPI tray (16 * 3).
    #
    # Both tray files come from the same drawing at the same sizes, so they
    # differ only in ink colour and a theme switch swaps one silhouette for
    # another rather than a slightly different glyph.
    build((255, 255, 255), os.path.join(res, "tray-white.ico"), ART_WHITE,
          sizes=[16, 20, 24, 32, 40, 48])

    # ... and the black copy for a *light* taskbar, where the white one is
    # invisible. Built from ART_BLACK - the same master app.ico is plated from -
    # so it needs no artwork of its own.
    build((0, 0, 0), os.path.join(res, "tray-black.ico"), ART_BLACK,
          sizes=[16, 20, 24, 32, 40, 48])
