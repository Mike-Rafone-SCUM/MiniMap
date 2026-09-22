"""Build the routing exclusion mask from the bundled map's blue water artwork.

This is map-derived coverage, not authoritative terrain collision data. It is
used for unverified straight-line connectors, never to erase surveyed bridges.
Regenerate and inspect the preview when the bundled map changes.
"""
from pathlib import Path
import gzip, hashlib, struct
import numpy as np
import cv2
from PIL import Image

root=Path(__file__).resolve().parent.parent
source=root/'resources/map.png'
Image.MAX_IMAGE_PIXELS=250_000_000
image=Image.open(source).convert('RGB')
mask=Image.new('L',image.size)
for top in range(0,image.height,256):
    box=(0,top,image.width,min(top+256,image.height))
    rgb=np.asarray(image.crop(box),dtype=np.int16)
    r,g,b=rgb[:,:,0],rgb[:,:,1],rgb[:,:,2]
    water=(b-r>=20)&(g-r>=12)&(b-g>=5)
    mask.paste(Image.fromarray(np.uint8(water)*255),box)
# Preserve thin blue streams rather than averaging them out at navigation scale.
small=np.asarray(mask.resize((4096,4096),Image.Resampling.BOX))>=24
# Discard isolated blue JPEG/artwork specks; retain connected streams and lakes.
count,labels,stats,_=cv2.connectedComponentsWithStats(np.uint8(small),8)
keep=stats[:,cv2.CC_STAT_AREA]>=12
keep[0]=False
small=keep[labels]
packed=np.packbits(small.reshape(-1),bitorder='little').tobytes()
raw=b'WATR'+struct.pack('<II',4096,4096)+packed
(root/'resources/water-mask.bin').write_bytes(gzip.compress(raw,mtime=0))
preview=Image.fromarray(np.uint8(small)*255)
preview.resize((1024,1024)).save(root/'release/water-mask-preview.png')
print('Source SHA256:',hashlib.sha256(source.read_bytes()).hexdigest())
print('Water cells:',int(small.sum()),'Compressed bytes:',(root/'resources/water-mask.bin').stat().st_size)
