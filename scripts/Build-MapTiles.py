"""Build a seekable, PNG-compressed map tile pyramid for the embedded map."""
import gc
import io
import os
import struct
import sys
from PIL import Image

Image.MAX_IMAGE_PIXELS = 256_000_000

SOURCE, OUTPUT = sys.argv[1:3]
TEMP_OUTPUT = OUTPUT + ".tmp"
TILE = 512
GUTTER = 2

with Image.open(SOURCE) as source:
    width, height = source.size

entries = []
while True:
    columns = (width + TILE - 1) // TILE
    rows = (height + TILE - 1) // TILE
    entries.append((width, height, columns, rows, [(0, 0)] * (columns * rows)))
    if width <= 512 or height <= 512:
        break
    width, height = width // 2, height // 2

with open(TEMP_OUTPUT, "wb") as output:
    output.write(b"MTL2")
    output.write(struct.pack("<II", TILE, len(entries)))
    for width, height, columns, rows, tiles in entries:
        output.write(struct.pack("<IIII", width, height, columns, rows))
        output.write(b"\0" * (12 * len(tiles)))
    with Image.open(SOURCE) as source:
        image = source.convert("RGB")
    for index, entry in enumerate(entries):
        width, height, columns, rows, tiles = entry
        for row in range(rows):
            for column in range(columns):
                tile = image.crop((max(0, column * TILE - GUTTER),
                                   max(0, row * TILE - GUTTER),
                                   min((column + 1) * TILE + GUTTER, width),
                                   min((row + 1) * TILE + GUTTER, height)))
                data = io.BytesIO()
                tile.save(data, format="JPEG", quality=92, subsampling=0, optimize=True)
                offset = output.tell()
                payload = data.getvalue()
                output.write(payload)
                tiles[row * columns + column] = (offset, len(payload))
                tile.close()
        if index + 1 < len(entries):
            next_width, next_height = entries[index + 1][:2]
            reduced = image.resize((next_width, next_height), Image.Resampling.BILINEAR)
            image.close()
            image = reduced
            gc.collect()
    image.close()
    output.seek(12)
    for width, height, columns, rows, tiles in entries:
        output.write(struct.pack("<IIII", width, height, columns, rows))
        for offset, length in tiles:
            output.write(struct.pack("<QI", offset, length))

os.replace(TEMP_OUTPUT, OUTPUT)
print(f"Built {sum(len(entry[4]) for entry in entries)} tiles across {len(entries)} levels.")
