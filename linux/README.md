# SCUM MiniMap unter Arch Linux / CachyOS

SCUM läuft unter Linux über Steam/Proton. MiniMap ist ein Windows-Programm
(.NET Framework 4.8 mit WPF) und arbeitet direkt mit SCUMs Fenster zusammen:
Es erkennt, ob SCUM im Vordergrund ist, drückt SCUMs „Copy Location“-Taste
und liest die Koordinaten aus der Zwischenablage.

Ein nativer Linux-Port wäre eine Neuentwicklung (rund 53.000 Zeilen WPF/Win32)
und würde unter Wayland trotzdem nicht funktionieren, weil dort normale
Programme weder fremde Fenster beobachten noch Tasten an sie senden dürfen.
Deshalb läuft MiniMap unter Linux **im selben Proton-Prefix und auf demselben
wineserver wie SCUM**. Dort sieht MiniMap das Spiel genau wie unter Windows.

## Installation

```bash
git clone https://github.com/h3adbang3r/SkynettMiniMap_ArchLinux.git
cd SkynettMiniMap_ArchLinux/linux/arch
makepkg -si
```

Abhängigkeiten zum Bauen: `dotnet-sdk`, `git`, `libarchive` (werden von
`makepkg -s` installiert). Zur Laufzeit wird `protontricks` empfohlen:

```bash
sudo pacman -S protontricks     # falls nicht in den Repos: yay -S protontricks (AUR)
```

## Einrichtung (einmalig)

1. SCUM einmal über Steam mit Proton starten und wieder beenden, damit der
   Prefix existiert. Mit CachyOS funktioniert auch `proton-cachyos`.
2. Prefix vorbereiten (SCUM muss beendet sein):

   ```bash
   scum-minimap setup
   ```

   Das installiert .NET Framework 4.8 in SCUMs Prefix (dauert einige Minuten),
   stellt WPF auf Software-Rendering (unter Wine/DXVK stabiler) und kopiert
   MiniMap nach `compatdata/513710/pfx/drive_c/SCUM-MiniMap`.
3. In Steam: **SCUM → Eigenschaften → Allgemein → Startoptionen**:

   ```
   scum-minimap-steam %command%
   ```

   Weitere Optionen wie `gamemoderun` bleiben möglich, z. B.
   `gamemoderun scum-minimap-steam %command%`.

Ab jetzt startet MiniMap mit SCUM und wird mit dem Spiel beendet. Nach einem
Paket-Update wird die neue Version beim nächsten Start automatisch in den
Prefix kopiert.

## Im Spiel

- SCUM im Modus **Fenster** oder **Randloses Fenster** spielen. Exklusives
  Vollbild verdeckt das Overlay, genau wie unter Windows.
- In SCUM unter Steuerung **Copy Location** auf **NumPad /** legen (oder
  dieselbe Taste in SCUM und MiniMap einstellen).
- Den Wayland-Modus von Proton (`PROTON_ENABLE_WAYLAND=1`) **nicht**
  verwenden. Standardmäßig läuft Proton über XWayland, dort funktionieren
  transparente, immer oben liegende Overlay-Fenster. Das gilt für KDE Plasma
  (CachyOS-Standard) wie für GNOME.

## Befehle

| Befehl | Zweck |
|---|---|
| `scum-minimap setup` | Prefix einmalig vorbereiten (`--no-dotnet` testet Wine-Mono statt .NET 4.8, `--hw-render` schaltet WPF-Hardwarebeschleunigung wieder ein) |
| `scum-minimap-steam %command%` | Steam-Startoption (empfohlen) |
| `scum-minimap run` | MiniMap neben einem bereits laufenden SCUM starten (über `protontricks-launch`, nicht garantiert) |
| `scum-minimap doctor` | Zeigt erkannte Steam-Bibliotheken, Prefix, Proton-Version und .NET-Status |
| `scum-minimap sync` | MiniMap manuell in den Prefix kopieren |

Variablen zum Überschreiben der Erkennung: `SCUM_COMPATDATA`, `SCUM_PROTON`,
`SCUM_MINIMAP_HOME`.

## Unterschiede zu Windows

- **Automatisch mit SCUM starten** (Windows-Aufgabenplanung) ist unter Linux
  deaktiviert. Die Steam-Startoption übernimmt das.
- **Updates** kommen über das Paket (`makepkg`/AUR-Helper), nicht über den
  eingebauten Updater.
- Einstellungen, Zonen und Kits liegen im Prefix unter
  `drive_c/users/steamuser/AppData/Local/ScumMiniMap`.
- Private Gruppen über das Relay funktionieren normal (reines TCP).

## Wie funktioniert die Steam-Startoption?

`scum-minimap-steam` kopiert MiniMap in den Prefix und setzt
`PROTON_REMOTE_DEBUG_CMD`. Proton startet dieses Programm mit demselben
wine-Binary, derselben Umgebung und im selben Steam-Linux-Runtime-Container
direkt vor dem Spiel. Deshalb liegt MiniMap im Prefix und nicht unter `/usr`:
Der Container sieht `/usr` des Hosts nicht. Die Proton-Log-Datei
(`PROTON_LOG=1`) enthält auch die Ausgaben von MiniMap.

## Selbst bauen ohne Paket

```bash
linux/build.sh Release out/        # braucht dotnet-sdk, lädt die .NET-4.8-Referenzassemblies von NuGet
SCUM_MINIMAP_HOME=$PWD/out linux/scum-minimap setup
```

## Fehlersuche

- `scum-minimap doctor` ausführen.
- In den Startoptionen `PROTON_LOG=1 scum-minimap-steam %command%` setzen und
  `~/steam-513710.log` ansehen.
- Fenster bleibt schwarz oder flackert: `scum-minimap setup` erneut ausführen
  (setzt Software-Rendering).
- Nach einem Wechsel der Proton-Version kann .NET im Prefix fehlen:
  `scum-minimap setup` erneut ausführen.
