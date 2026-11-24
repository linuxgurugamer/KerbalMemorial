"""Generate UI artwork from the original Kerbal Memorial Wall image.
Usage: python generate_wall.py '/path/to/Kerbal Memorial Wall of Fallen Crew.png'
Only the decorative regions are reused; no fictional memorial entries are included.
"""
from pathlib import Path
from PIL import Image, ImageOps, ImageDraw
import sys
out = Path(__file__).resolve().parent
im = Image.open(sys.argv[1]).convert('RGB')
if abs(im.width / im.height - 4/3) > 0.01:
    raise ValueError('Expected the original 4:3 wall image')
# Coordinates are expressed in the reference display's 1408 x 1056 space.
im = im.resize((1408, 1056), Image.Resampling.LANCZOS)
for name, box in {
    'WallHeader': (272, 12, 1371, 350),
    'WallSide': (33, 33, 272, 1013),
    'WallFooter': (272, 887, 1371, 1013),
    'WallStone': (281, 43, 389, 104),
}.items():
    im.crop(box).save(out / (name + '.png'))
# Build a blank plaque using the original stone texture, bronze colors sampled
# from the wall, and a beveled border. Never reuse a fictitious face or name.
w,h=750,294
base=Image.new('RGB',(w,h),'#201611')
stone=im.crop((281,43,389,104))
stone=ImageOps.colorize(ImageOps.grayscale(stone),'#251711','#a37753')
stone=stone.resize((324,183),Image.Resampling.LANCZOS)
for y in range(0,h,stone.height):
    for x in range(0,w,stone.width):
        base.paste(stone if (x//stone.width+y//stone.height)%2==0 else ImageOps.mirror(stone),(x,y))
d=ImageDraw.Draw(base)
for inset, width, color in [(0,9,'#1b1410'),(8,5,'#8d5c3c'),(13,5,'#d9ab77'),(18,4,'#65432d'),(22,4,'#38271e')]:
    d.rounded_rectangle((inset,inset,w-1-inset,h-1-inset),radius=max(8,24-inset//2),outline=color,width=width)
for x in (35,w-36):
    for y in (34,h-35):
        d.ellipse((x-10,y-10,x+10,y+10),fill='#302016',outline='#dbab75',width=2)
        d.ellipse((x-5,y-6,x+3,y+2),fill='#b98352')
base.save(out/'WallPlaque.png')
