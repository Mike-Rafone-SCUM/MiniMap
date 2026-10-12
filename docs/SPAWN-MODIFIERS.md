# Spawn cart item modifiers

Modifier names and command argument structure were checked against SCUM Database on 7 October 2026 using its public [SpawnItem command metadata](https://scum-db.com/api/admin-commands/SpawnItem) and [item property configuration](https://scum-db.com/api/admin-commands/config/item-properties). The command permits repeated property/value pairs after the item ID and quantity. Each cart entry stores its own overrides; blank fields retain the game's defaults. SCUM Database lists Location but leaves its value hint unresolved, so its formats were verified against the [official SCUM 0.6.6.38576 patch notes](https://store.steampowered.com/news/posts/?appgroupname=scum&appids=513710&enddate=1633617799&feed=steam_community_announcements) and [SCUM developer clarification](https://steamcommunity.com/app/513710/discussions/0/3043859512572689748/).

| Property | Value accepted by MiniMap | Source scope |
| --- | --- | --- |
| Health | Decimal between 0 and 100 | Item condition expressed as a percentage |
| Dirtiness | Decimal between 0 and 100 | Dirtiness expressed as a percentage |
| Uses | Nonnegative whole number | Remaining uses, up to the individual item's maximum |
| Location | Quoted `X=… Y=… Z=…`, quoted copied transform, three unquoted coordinates or a 17-digit Steam ID | Four formats documented in official SCUM patch notes |
| StackCount | Positive whole number | Stack size, up to the individual item's maximum |
| KeyCardSector | A1–A4, B1–B4, C1–C4 or D1–D4 | These 16 values are explicitly listed in the source configuration |
| Radiation | Invariant decimal number | The source provides no confirmed units or bounds |
| Weight | Invariant decimal number | The source labels this override in kilograms but provides no confirmed bounds |

`SpawnModifiers` validates these eight properties, rejects duplicate names, and generates canonical names and a stable property order. Numeric input uses a period for the decimal separator; exponents, grouping separators, control characters, additional commands and arbitrary property names are rejected. Whole numbers use the application's 32-bit integer representation; decimal numbers use its decimal representation. These representation limits are application limits, not claims about SCUM's maximum values.

The popup lists the eight properties as optional checkboxes with dropdown values. Selecting a preset enables its property; deselecting it clears the value and omits that override. Numeric presets are convenience choices, not claims that every item supports those values; the dropdowns also accept custom values. KeyCardSector lists the sixteen documented sectors. Location accepts a custom target and lists a previously saved target when reopening the editor. Selecting a modifier without choosing a value keeps the editor open with a validation error.

Multiple cart rows can share an edit session. Common values appear selected; differing values, including a property present on only some items, appear as **Mixed** with an indeterminate checkbox. Choosing a preset or custom value updates that property across the selection. Unchecking a modifier removes it across the selection, and Reset to defaults removes all eight properties. Untouched properties preserve each item's original value. Choosing the Mixed entry again preserves the original values. Cancellation makes no cart change. All replacements are validated before committing any selected entry, including each full command's 512-character limit. Quantities, order, unselected rows and the selection are preserved after applying and saving the edit.

The catalogue currently does not store each item's maximum uses or stack size, so MiniMap checks the documented lower bounds without inventing per-item upper limits. The game may reject or clamp incompatible overrides; successful input submission does not prove that the server applied a modifier. The full generated command must fit within 512 characters.

The Location field accepts a keyed vector such as `X=100 Y=200 Z=300`, copied SCUM location text such as `{X=100 Y=200 Z=300|P=0 Y=90 R=0}`, three numbers such as `100 200 300`, or a 17-digit Steam ID. The popup adds quotes to keyed vectors and copied transforms; it leaves Steam IDs and numeric triples unquoted. When typing the complete modifier string in the cart, quote keyed vectors and copied transforms explicitly. Coordinates and rotation values are validated as signed invariant decimals and normalized without introducing a map boundary limit. Labels are normalized to uppercase; embedded quotes, extra axes and incomplete vectors are rejected. A Steam ID's shape is validated, but MiniMap does not confirm whether that player is online. Blank Location retains SCUM's default spawn position in front of the invoking player.

Example:

```text
#SpawnItem Weapon_AK47 1 Health 100 Weight 3.5
#SpawnItem Weapon_AK47 1 Location "X=100 Y=200 Z=300" Weight 3.5
```

Named kits and the working cart are stored in `spawn-kits.db` in the active AppData profile, retaining each item's canonical ID, quantity, modifier string and position. Saving a kit and replacing the working cart use database transactions. Legacy TSV imports support version 2's third modifier column and older two-column kits with default modifiers. Loading validates the entire kit before replacing the current cart. Editing quantities or adding more of an existing item preserves its modifiers; clearing the modifier fields removes the overrides.
