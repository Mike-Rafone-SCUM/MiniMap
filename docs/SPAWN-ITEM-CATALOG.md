# Spawn item catalogue

The admin item selector uses an offline snapshot of [SCUM Database](https://scum-db.com/). `resources/spawn-items.tsv` stores the site's game item ID, display name, category, individual item source URL and subcategory. IDs are copied from the source, including individual clothing, ammunition and weapon variants. The application does not generate IDs by appending guessed suffixes and does not request website data at runtime.

SCUM Database describes its metadata as derived from SCUM game assets. Its site can also retain records that are not obtainable during normal gameplay. This catalogue takes only records explicitly marked `is_spawnable=true` and `asset_purpose=item`, excluding records marked `is_buildable`, `admin_spawn_hidden` or `removed_from_game`. Physical items such as base expansion kits can have the site's `buildable` category while retaining `asset_purpose=item`; they are included. Missing categories are stored as `other`.

This is a source metadata snapshot, not confirmation that a particular live server will accept every item or that an item is available in a player's game version or DLC. The source also exposes uncertainty and client/server presence metadata. The bundled catalogue preserves source spawnability eligibility rather than claiming a live game result.

## Refresh

From this project directory, use Windows PowerShell 5.1 or PowerShell 7:

```powershell
.\scripts\Update-SpawnItemCatalog.ps1
```

The script reads the public `GET https://scum-db.com/api/search` API with `splitVariants=true`, traversing every reported page. Pages can contain fewer records than the requested limit because the source merges some aliases; pagination advances by the reported limit. A changed page total, conflicting IDs, malformed response, unsafe ID or an unexpectedly small catalogue aborts without replacing the previous snapshot. The resulting UTF-8 TSV is written atomically. Review changed records and rebuild the application to update the embedded resource.

Comment headers record retrieval time, endpoint, filters and counts. Version 2 data rows have five tab-separated fields, without escapes: `ID`, `Name`, `Category`, `SourceUrl`, `Subcategory`. The first four fields retain their original order. The loader also accepts legacy four-column rows, treating their subcategory as empty. IDs accept only ASCII letters, digits, underscores and hyphens, with a maximum of 128 characters; a leading hyphen is rejected. Names/categories cannot contain control characters, and a subcategory is at most 120 characters. IDs are unique without regard to case. `SpawnItemCatalog.Load` rejects malformed rows, duplicates and source URLs that do not match the item's canonical `https://scum-db.com/items/<ID>` URL.

## Categories

`SpawnItemCategories` creates six navigation groups from the source category: weapons; clothing and armour; food, health and survival; equipment and crafting; vehicle parts; other items. These groups are application presentation labels. Their category/subcategory descendants preserve the source classification, including assault rifles, pistols, food, drinks, medicine, backpacks, tools and vehicle components. They do not classify items by guessing from an ID or display name. The source's `melee_weapon` and `weapon_melee` aliases share a category node. Unknown categories remain visible in Other items, and missing source subcategories appear under General.

The existing 2,328-ID snapshot was enriched with source subcategory metadata while preserving every original ID/name/category/source URL. Its headers retain the original retrieval time and separately record subcategory retrieval time. Future refreshes retrieve the complete five-column source data together.

## Weapon compatibility and search

`resources/spawn-compatibility.tsv` stores explicit SCUM Database compatibility links alongside the item catalogue. Each of its four-column rows contains `WeaponID`, `RelatedID`, `Relation` and `SourceUrl`. Relations distinguish magazines, ammunition, direct attachments, attachments requiring an adapter and the required adapters. Every endpoint is queried for its exact weapon ID, including variants; compatibility is not inferred from a weapon name or copied between skins. Links to items outside the eligible spawn catalogue are omitted and listed in the snapshot headers.

Refresh the item catalogue first, then refresh compatibility:

```powershell
.\scripts\Update-SpawnCompatibility.ps1
```

The script queries the public `GET https://scum-db.com/api/items/<WeaponID>/compatibility` endpoint for every ranged weapon in the local catalogue. It validates the response schema, exact IDs and attachment match methods before atomically replacing the snapshot. The bundled snapshot contains 3,882 links from 97 weapon queries; 96 weapons have eligible related items. The loader validates both endpoints against the item catalogue, canonical source URLs, supported relation types and duplicate rows. Rebuild after changing either resource. Searching remains offline.

Search ignores case, spaces and punctuation within item IDs, so `AK47`, `AK-47`, `AK_47` and `AK 47` find the same gun family. A weapon search also includes its source-listed magazines, ammunition, attachments and adapters. Additional terms filter the related items: `AK47 scope` shows compatible scope results. The selected category determines which weapons can seed related results; their compatible parts can appear from other categories. Browsing a category without a search remains confined to that category. The **Related to** column names the matching weapons and marks attachments that may need an adapter. A shared part appears once. The database's compatibility metadata describes the item relationship; live SCUM remains authoritative about attachment and spawning behavior.

The result list groups matches by their source category/subcategory and provides collapsible section headings with counts. Ammunition uses its source calibre, while attachment subtypes distinguish scopes, sights, rails and suppressors. A record remains in its source subtype even if its display name suggests another classification. Records without a subtype remain in their source category's general section. This grouping does not change the compatibility graph or generate IDs. Native list selection remains available across expanded sections; collapsing a section deselects its rows, and the add action also excludes rows in collapsed groups. Collapse state is retained by category/subcategory key for the current window session.

## Command source

[SCUM Database's admin commands](https://scum-db.com/tools?tab=admin-commands&verb=SpawnItem) and its public `GET /api/admin-commands/SpawnItem` endpoint describe `#SpawnItem <Item> [Count] [ItemProperty PropertyValue ...]` as spawning one or more items in front of the invoking administrator. Its item argument uses item names or IDs; this selector uses source IDs. The source does not document a SteamID target argument or a maximum count for this command. Any application batch limits are application limits.

## Attribution

Item identifiers, names, category metadata and explicit weapon compatibility are sourced from SCUM Database, retrieved through its public website API. Each bundled item row contains its individual [SCUM Database item page](https://scum-db.com/items/Weapon_AK47), and each compatibility row contains the weapon's source API URL. No website descriptions or image assets are bundled. SCUM game content belongs to its respective rights holders, including Gamepires and other licensors. SCUM Database is a fan resource and is not affiliated with the game developers; SCUM MiniMap is likewise independent of SCUM Database.
