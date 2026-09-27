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

* **Official Repository:** [Mike-Rafone-SCUM/MiniMap](https://github.com/Mike-Rafone-SCUM/MiniMap)
* **Skynett Gaming Community:** [Discord: discord.gg/MYzcGaFDMn](https://discord.gg/MYzcGaFDMn) | Dedicated server hosting, bot development, and player community.
