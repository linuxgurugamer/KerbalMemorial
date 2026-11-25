"""Generate individual plaque templates from supplied classic image and cleaned alternate wall artwork.
Usage: python generate_individual_plaque.py path/to/classic.png
"""
from PIL import Image, ImageDraw, ImageFilter, ImageEnhance
from pathlib import Path
import sys

out = Path(__file__).parent
im = Image.open(sys.argv[1]).convert('RGB').resize((1920,800),Image.Resampling.LANCZOS)
# Replace the example text with nearby blank bronze texture, preserving the
# divider, laurels, frame, and original inscription panel.
def replace(box, sample, radius=12, rounded=0, brightness=1):
    global im
    x,y,X,Y=box
    patch=im.crop(sample).resize((X-x,Y-y),Image.Resampling.LANCZOS)
    patch=ImageEnhance.Brightness(patch).enhance(brightness)
    layer=Image.new('RGB',im.size); layer.paste(patch,(x,y))
    # Extend patch beyond feathered edge with a padded replica of its pixels.
    # Mask is inset to keep the adjacent original gold frame untouched.
    mask=Image.new('L',(X-x,Y-y)); d=ImageDraw.Draw(mask)
    inset=max(2,radius*2)
    d.rounded_rectangle((inset,inset,X-x-inset-1,Y-y-inset-1),radius=rounded,fill=255)
    mask=mask.filter(ImageFilter.GaussianBlur(radius/2))
    im.paste(patch,(x,y),mask)
# Source coordinates in 1920 x 800 image.
replace((722,88,1564,229),(380,475,1400,657),8,0,1.0)
replace((810,266,1480,402),(400,475,1300,660),9,0,1.0)
# The portrait extends down to the frame's inner lip. Rebuild the empty
# portrait recess with a dark brown texture; the actual portrait overlays it.
box=(296,77,582,411)
x,y,X,Y=box
patch=im.crop((470,490,790,670)).resize((X-x,Y-y),Image.Resampling.LANCZOS)
patch=ImageEnhance.Brightness(patch).enhance(.82)
mask=Image.new('L',patch.size); d=ImageDraw.Draw(mask); d.rounded_rectangle((1,1,patch.width-2,patch.height-2),radius=39,fill=255)
im.paste(patch,(x,y),mask)
classic=im.crop((172,16,1748,782))
classic.save(out/'IndividualClassic.png',optimize=True)
# Fallback industrial style, based on the cleaned Alternate wall plaque.
# No sample portraits or lettering are present in that resource.
alt=Image.open(out/'AltWallPlaque.png').convert('RGB')
w,h=1576,766
canvas=Image.new('RGB',(w,h),(47,49,53))
# Preserve the portrait and bolts' aspect ratio. Expand only the blank
# central metal area to match the wide individual-plaque layout.
upper_h=464
scale=upper_h/alt.height
left=round(176*scale); right=round(47*scale)
canvas.paste(alt.crop((0,0,176,alt.height)).resize((left,upper_h),Image.Resampling.LANCZOS),(0,0))
canvas.paste(alt.crop((176,0,350,alt.height)).resize((w-left-right,upper_h),Image.Resampling.LANCZOS),(left,0))
canvas.paste(alt.crop((350,0,397,alt.height)).resize((right,upper_h),Image.Resampling.LANCZOS),(w-right,0))
# Draw a second inset metallic panel with the same surface texture and trim.
# Stretch a clean metal section rather than tile the background.
metal=alt.crop((185,38,345,145)).resize((w-80,250),Image.Resampling.LANCZOS)
canvas.paste(metal,(40,489))
d=ImageDraw.Draw(canvas)
d.rounded_rectangle((30,478,w-31,h-20),radius=25,outline=(16,17,18),width=9)
d.rounded_rectangle((36,484,w-37,h-26),radius=21,outline=(169,171,175),width=5)
d.rounded_rectangle((42,490,w-43,h-32),radius=18,outline=(81,83,87),width=4)
canvas.save(out/'IndividualAlternate.png',optimize=True)
