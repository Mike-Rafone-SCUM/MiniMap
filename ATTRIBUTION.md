# Attributions, Third-Party Data & Licenses

This document acknowledges and attributes all third-party repositories, interactive map providers, open-source projects, and game intellectual property utilized by **SCUM MiniMap**.

---

## 1. Game Intellectual Property & Disclaimer

* **Game:** [SCUM](https://scumgame.com/)
* **Developer:** [Gamepires](https://gamepires.com/)
* **Publisher:** [Jagex](https://www.jagex.com/)
* **Trademarks & Copyright:** All in-game content, textures, terrain artwork, item names, sector designations, character models, and audio are the exclusive copyright and trademarks of Gamepires d.o.o. and Jagex Limited.
* **Disclaimer:** SCUM MiniMap is a non-commercial, third-party community overlay utility developed for the Skynett Gaming community. It is not an official Gamepires or Jagex product, and is neither affiliated with, maintained by, nor endorsed by Gamepires or Jagex.

---

## 2. Interactive Map & POI Data Providers

We gratefully acknowledge the creators and community contributors whose publicly shared map research and coordinate databases made detailed navigation on the island possible:

### Scum-Map.com
* **Website:** [https://scum-map.com](https://scum-map.com)
* **Creator / Maintainer:** Jazi and the Scum-Map.com community
* **Contributions Utilized:**
  * Categorized POI location points (over 8,000 points across 106 official categories).
  * Vector polygon geometry for the four primary island hunting biomes (`hunting_biomes.svg`: Mediterranean, Continental Meadow, Continental Forest, Mountain).
  * Island Wildlife Guide species encounter table and time-of-day behavioral classifications ([Island Wildlife Guide](https://scum-map.com/en/catalog/scum/island)).

### Davo's SCUM Interactive Map
* **Website:** [https://davoonline.com/scummap/](https://davoonline.com/scummap/)
* **Creator / Maintainer:** Davo / DavoOnline
* **Contributions Utilized:**
  * Aquatic life spawning volume extents (`EMBEDDED_AQUATIC_LIFE`, 187 volumes).
  * Fish species preset distributions and encounter weight tables (`FISH_SPECIES_BY_PRESET`, 23 preset groups).

### Scummymap
* **Website:** [https://scummymap.com](https://scummymap.com)
* **Contributions Utilized:**
  * Map legend cross-referencing and schema audit data used to verify POI naming conventions and sector bounds.

---

## 3. Open-Source Libraries & Software

### SCUM Database item catalogue

* **Website:** [SCUM Database](https://scum-db.com/)
* **Data:** Exact item spawn IDs, item names, category metadata and explicit weapon compatibility from its public structured APIs. The executable embeds an offline snapshot of records identified as visible, spawnable items and their source-listed ammunition, magazines, attachments and adapters. Game assets and item names remain the property of their respective rights holders.
* **Provenance and refresh:** See `docs/SPAWN-ITEM-CATALOG.md` in the development project. The item selector also links to SCUM Database directly.


### OpenCV (Open Source Computer Vision Library)
* **Repository / Website:** [https://opencv.org](https://opencv.org) | [GitHub: opencv/opencv](https://github.com/opencv/opencv)
* **License:** [Apache License 2.0](https://www.apache.org/licenses/LICENSE-2.0)
* **Copyright:** © 2000-2026 Intel Corporation, Willow Garage Inc., OpenCV Foundation
* **Usage:** Used in `detect_zones.py` for automated template matching, computer vision contouring, and zone circle detection.

### NumPy
* **Repository / Website:** [https://numpy.org](https://numpy.org) | [GitHub: numpy/numpy](https://github.com/numpy/numpy)
* **License:** [BSD 3-Clause License](https://numpy.org/doc/stable/license.html)
* **Copyright:** © 2005-2026 NumPy Developers
* **Usage:** Used for fast multi-dimensional coordinate transformations, bounding box math, and grid index quantization.

### RE-UE4SS (Unreal Engine 4 Scripting System)
* **Repository:** [https://github.com/UE4SS-RE/RE-UE4SS](https://github.com/UE4SS-RE/RE-UE4SS)
* **License:** [MIT License](https://github.com/UE4SS-RE/RE-UE4SS/blob/main/LICENSE)
* **Copyright:** © 2022-2026 UE4SS Team
* **Usage:** Instrumental during offline engine research, C++ telemetry discovery, and road network topological extraction.

### Tabler Icons
* **Repository / Website:** [https://tabler.io/icons](https://tabler.io/icons) | [GitHub: tabler/tabler-icons](https://github.com/tabler/tabler-icons)
* **License:** [MIT License](https://github.com/tabler/tabler-icons/blob/main/LICENSE)
* **Copyright:** © 2020-2026 Paweł Kuna
* **Usage:** Vector icon glyphs utilized in HTML visualization panels and diagnostic test previews.

---

## 4. Project Repository & Community

### Navigation voice recordings
* **Lyan (Female US)**, **Mira (Russian)**, **Mila (German)** and **Isabel (Spanish Argentine)**: ElevenLabs recordings supplied by the project owner for inclusion in SCUM MiniMap.
* For those four voices, individual navigation prompts were extracted from the supplied recordings.
* **Noora (Suomi)**, **Ona (Lietuvių)** and **Zuri (Kiswahili)** use synthesized Microsoft neural speech prepared with the offline authoring tools. The application bundles the MP3 clips and plays them locally. These packs do not clone a person's voice. Lithuanian and Swahili transcripts and pronunciation still require native-speaker review.

### Steam profile icon
* Steam logo PNG supplied by the project owner, displayed at 22 × 22 pixels for Steam Community profile links. Steam and the Steam logo are trademarks of Valve Corporation.

* **Official Repository:** [Mike-Rafone-SCUM/MiniMap](https://github.com/Mike-Rafone-SCUM/MiniMap)
* **Skynett Gaming Community:** [Discord: discord.gg/MYzcGaFDMn](https://discord.gg/MYzcGaFDMn) | Dedicated server hosting, bot development, and player community.

## Field console typography

Barlow Semi Condensed by Jeremy Tribby, bundled from the Google Fonts repository under the SIL Open Font License 1.1. The complete licence is in `resources/fonts/OFL.txt`.

### Bundled Barlow Semi Condensed license

```text
Copyright 2017 The Barlow Project Authors (https://github.com/jpt/barlow)

This Font Software is licensed under the SIL Open Font License, Version 1.1.
This license is copied below, and is also available with a FAQ at:
http://scripts.sil.org/OFL


-----------------------------------------------------------
SIL OPEN FONT LICENSE Version 1.1 - 26 February 2007
-----------------------------------------------------------

PREAMBLE
The goals of the Open Font License (OFL) are to stimulate worldwide
development of collaborative font projects, to support the font creation
efforts of academic and linguistic communities, and to provide a free and
open framework in which fonts may be shared and improved in partnership
with others.

The OFL allows the licensed fonts to be used, studied, modified and
redistributed freely as long as they are not sold by themselves. The
fonts, including any derivative works, can be bundled, embedded, 
redistributed and/or sold with any software provided that any reserved
names are not used by derivative works. The fonts and derivatives,
however, cannot be released under any other type of license. The
requirement for fonts to remain under this license does not apply
to any document created using the fonts or their derivatives.

DEFINITIONS
"Font Software" refers to the set of files released by the Copyright
Holder(s) under this license and clearly marked as such. This may
include source files, build scripts and documentation.

"Reserved Font Name" refers to any names specified as such after the
copyright statement(s).

"Original Version" refers to the collection of Font Software components as
distributed by the Copyright Holder(s).

"Modified Version" refers to any derivative made by adding to, deleting,
or substituting -- in part or in whole -- any of the components of the
Original Version, by changing formats or by porting the Font Software to a
new environment.

"Author" refers to any designer, engineer, programmer, technical
writer or other person who contributed to the Font Software.

PERMISSION & CONDITIONS
Permission is hereby granted, free of charge, to any person obtaining
a copy of the Font Software, to use, study, copy, merge, embed, modify,
redistribute, and sell modified and unmodified copies of the Font
Software, subject to the following conditions:

1) Neither the Font Software nor any of its individual components,
in Original or Modified Versions, may be sold by itself.

2) Original or Modified Versions of the Font Software may be bundled,
redistributed and/or sold with any software, provided that each copy
contains the above copyright notice and this license. These can be
included either as stand-alone text files, human-readable headers or
in the appropriate machine-readable metadata fields within text or
binary files as long as those fields can be easily viewed by the user.

3) No Modified Version of the Font Software may use the Reserved Font
Name(s) unless explicit written permission is granted by the corresponding
Copyright Holder. This restriction only applies to the primary font name as
presented to the users.

4) The name(s) of the Copyright Holder(s) or the Author(s) of the Font
Software shall not be used to promote, endorse or advertise any
Modified Version, except to acknowledge the contribution(s) of the
Copyright Holder(s) and the Author(s) or with their explicit written
permission.

5) The Font Software, modified or unmodified, in part or in whole,
must be distributed entirely under this license, and must not be
distributed under any other license. The requirement for fonts to
remain under this license does not apply to any document created
using the Font Software.

TERMINATION
This license becomes null and void if any of the above conditions are
not met.

DISCLAIMER
THE FONT SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO ANY WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT
OF COPYRIGHT, PATENT, TRADEMARK, OR OTHER RIGHT. IN NO EVENT SHALL THE
COPYRIGHT HOLDER BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
INCLUDING ANY GENERAL, SPECIAL, INDIRECT, INCIDENTAL, OR CONSEQUENTIAL
DAMAGES, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
FROM, OUT OF THE USE OR INABILITY TO USE THE FONT SOFTWARE OR FROM
OTHER DEALINGS IN THE FONT SOFTWARE.

```
