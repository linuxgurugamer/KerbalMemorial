"""Regenerate the editor sign board and per-card digit atlas.
Requires Pillow, NumPy, OpenCV and a bold TrueType font. The original preview
is SignPreview.png; generated PNGs are embedded, with no runtime dependencies.
Usage: python generate_sign.py /path/to/LiberationSans-Bold.ttf
"""
from pathlib import Path
import sys
import cv2
import numpy as np
from PIL import Image, ImageDraw, ImageFont

OUT = Path(__file__).resolve().parent
FONT = sys.argv[1] if len(sys.argv) > 1 else 'LiberationSans-Bold.ttf'
WIDTH = 2048
HEIGHT = 2048
TILE_WIDTH = 192
TILE_HEIGHT = 440
TILE_TOP = 1150
TILE_X = (883, 1149, 1416, 1683)

# Rectify the photographed sign, retaining the 3:2 physical aspect.
source = np.asarray(Image.open(OUT / 'SignPreview.png').convert('RGB'))
quad = np.float32([[55, 78], [1427, 128], [1435, 918], [47, 942]])
flat_quad = np.float32([[0, 0], [1535, 0], [1535, 1023], [0, 1023]])
flat = cv2.warpPerspective(source, cv2.getPerspectiveTransform(quad, flat_quad), (1536, 1024))
board = Image.fromarray(flat).resize((WIDTH, HEIGHT), Image.Resampling.LANCZOS)
# Remove the example digits while retaining each card's own aged paper texture.
for x in TILE_X:
    box = (x, TILE_TOP, x + TILE_WIDTH, TILE_TOP + TILE_HEIGHT)
    patch = np.asarray(board.crop(box)).copy()
    gray = cv2.cvtColor(patch, cv2.COLOR_RGB2GRAY)
    mask = (gray < 155).astype(np.uint8) * 255
    mask = cv2.dilate(mask, np.ones((19, 13), np.uint8))
    alpha = cv2.GaussianBlur(mask, (15, 15), 0).astype(np.float32)[:, :, None] / 255
    # Interpolate from the clean strips at each side of the number.
    left = np.median(patch[:, :10], axis=1).astype(np.float32)
    right = np.median(patch[:, -10:], axis=1).astype(np.float32)
    left = cv2.GaussianBlur(left.reshape(-1, 1, 3), (1, 41), 0)
    right = cv2.GaussianBlur(right.reshape(-1, 1, 3), (1, 41), 0)
    weight = np.linspace(0, 1, TILE_WIDTH, dtype=np.float32)[None, :, None]
    fill = left * (1 - weight) + right * weight
    clean = np.uint8(np.clip(patch * (1 - alpha) + fill * alpha, 0, 255))
    board.paste(Image.fromarray(clean), (x, TILE_TOP))
board.save(OUT / 'SignBoard.png', optimize=True)

# Twelve columns (0-9, dash, blank) and four rows, one row per card.
# The four cards have different backgrounds, so each gets its own tiles.
atlas = Image.new('RGB', (TILE_WIDTH * 12, TILE_HEIGHT * 4))
font = ImageFont.truetype(FONT, 440)
for slot, x in enumerate(TILE_X):
    blank = board.crop((x, TILE_TOP, x + TILE_WIDTH, TILE_TOP + TILE_HEIGHT))
    for index, char in enumerate('0123456789- '):
        tile = blank.copy()
        if char != ' ':
            bbox = font.getbbox(char)
            mask = Image.new('L', (bbox[2] - bbox[0], bbox[3] - bbox[1]))
            ImageDraw.Draw(mask).text((-bbox[0], -bbox[1]), char, font=font, fill=255)
            # Fit a consistent physical numeral width/height. The square GPU
            # texture is stretched back to 3:2 in the editor.
            digit_height = 370 if char != '-' else 62
            digit_width = 151 if char != '-' else 126
            if char == '1':
                digit_width = 120
            mask = mask.resize((digit_width, digit_height), Image.Resampling.LANCZOS)
            tile.paste((30, 29, 28), ((TILE_WIDTH - digit_width) // 2, (TILE_HEIGHT - digit_height) // 2), mask)
        atlas.paste(tile, (index * TILE_WIDTH, slot * TILE_HEIGHT))
atlas.save(OUT / 'SignDigits.png', optimize=True)
