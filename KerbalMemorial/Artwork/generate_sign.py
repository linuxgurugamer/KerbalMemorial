"""Regenerate embedded sign artwork. Requires Pillow and a bold TrueType font.
Usage: python generate_sign.py /path/to/DejaVuSans-Bold.ttf
The generated PNGs are included; Python/font are not required to build the mod.
"""
from pathlib import Path
import sys
from PIL import Image, ImageDraw, ImageFont

OUT = Path(__file__).resolve().parent
FONT = sys.argv[1] if len(sys.argv) > 1 else 'DejaVuSans-Bold.ttf'
# Logical artwork is 3:2. Sample at 2x, then resample horizontally into a
# square power-of-two GPU texture. The world-space quad restores 3:2.
S = 2
W, H = 1536, 1024
BG, WHITE, INK = '#1c5736', '#f8f9f2', '#18281e'
CARD, EDGE, ROD = '#228550', '#14502d', '#2a3e2f'
img = Image.new('RGBA', (W*S,H*S), BG)
d = ImageDraw.Draw(img)
def rect(x,y,w,h,r,fill):
    d.rounded_rectangle((x*S,y*S,(x+w)*S-1,(y+h)*S-1),radius=r*S,fill=fill)
def circle(x,y,r,fill):
    d.ellipse(((x-r)*S,(y-r)*S,(x+r)*S,(y+r)*S),fill=fill)
def label(draw, text, cx, cy, maxw, maxh, color):
    # Fit the real glyph outline and center its ink, not its baseline.
    size = round(maxh*S*1.4)
    while True:
        f=ImageFont.truetype(FONT,size)
        l,t,r,b=draw.textbbox((0,0),text,font=f)
        if r-l <= maxw*S and b-t <= maxh*S: break
        size -= 2
    draw.text((round(cx*S-(r+l)/2),round(cy*S-(b+t)/2)),text,font=f,fill=color)
rect(24,24,1488,976,30,'#e1e9df')
rect(39,39,1458,946,24,WHITE)
xs=[90,321,552,783]
rect(110,104,862,14,7,ROD)
for x in xs:
    for hx in (x+50,x+160):
        rect(hx-5,112,10,128,5,ROD)
    rect(x+9,231+11,210,308,17,'#c5cec3')
    rect(x,231,210,308,17,EDGE)
    rect(x+5,236,200,298,14,CARD)
    for hx in (x+50,x+160):
        circle(hx,245,10,'#e6eee4'); circle(hx,245,4,ROD)
label(d,'DAYS',1230,390,396,126,INK)
label(d,'SINCE LAST',768,660,1260,152,INK)
label(d,'ACCIDENT',768,855,1260,175,INK)
img.resize((2048,2048),Image.Resampling.LANCZOS).save(OUT/'SignBoard.png',optimize=True)
# All tiles cover only the uniform interior of the card. Blank tile clears a
# previous digit without modifying hooks, corners, shadows or the board.
tw,th=162,204
atlas=Image.new('RGBA',(tw*S*4,th*S*3),CARD)
for i,c in enumerate('0123456789- '):
    if c==' ': continue
    tile=Image.new('RGBA',(tw*S,th*S),CARD)
    label(ImageDraw.Draw(tile),c,tw/2,th/2,tw-12,th-12,WHITE)
    atlas.paste(tile,((i%4)*tw*S,(i//4)*th*S))
atlas.resize((216*4,408*3),Image.Resampling.LANCZOS).save(OUT/'SignDigits.png',optimize=True)
