# Protective Wards

![logo](https://staticdelivery.nexusmods.com/mods/3667/images/2450/2450-1689565569-1699140464.png)

Configurable ward protection, access control, passive base support, server-side privacy tools, multipliers and active offerings for Valheim.

Protective Wards is aimed at public PvE servers: it helps keep non-permitted players from casually using, taking, moving or changing objects inside another player's warded base. It is not designed as a PvP raid system.

## Requirements

- BepInExPack Valheim
- Jotunn 2.29.1 or newer compatible 2.x version
- YamlDotNet

The mod uses Jotunn network compatibility with `EveryoneMustHaveMod` and server-synced configuration. In client-server mode, the mod is required on both the server and all clients.

## Main features

Most features work inside an active player ward area. Some background protections can be configured to use connected/overlapping ward networks.

### How access is evaluated

Protective Wards separates several kinds of access instead of using only the vanilla permitted list everywhere.

- **Direct access** means the player is the ward creator, is directly permitted on the ward, or is allowed by the configured admin bypass.
- **Connected/effective access** can extend access through overlapping wards according to the selected connected access mode. This is useful for shared bases made from multiple wards.
- **Object ownership exemptions** are used only for specific objects where trapping a player would be worse than allowing a limited action. Tombstones, saddles, vehicles and similar cases are handled separately from normal base access.
- **Admin access** is controlled by `Ward admin / Ward admin access`. By default it requires admin plus god mode, so admins can still play normally without bypassing protections accidentally.
- **Permit everyone** is a global bypass. When enabled, access restrictions are not enforced and all players are treated as ward admins.

For client-server games, sensitive operations are validated on the server. Client UI and console commands are only requests; the server re-checks access, distance and the target object before changing ward data.

### Per-ward visual settings

Each ward can have its own visual settings stored in the ward ZDO.

To edit a ward:

1. Disable the ward.
2. Press `AltPlace + Use` (`Left Shift + E` by default) on the ward.
3. Change values in the settings window.
4. Apply settings from the main settings page.

You can customize:

- ward range;
- emission color and multiplier;
- ward sphere visibility and color;
- detailed ward sphere shader properties;
- ward circle colors, width, line amount and animation speed.

Wards without custom settings can either use global default config values or keep vanilla/current behavior, depending on the config option `Use default values for wards without custom settings`.

Disabled wards owned by another player cannot be edited. Admin bypass is controlled by `Ward admin / Ward admin access`:

- `Off` - admins do not bypass ward access checks;
- `Admins` - server admins and host bypass ward access checks;
- `AdminsInGodMode` - server admins and host bypass ward access checks only while god mode is enabled.

The default is `AdminsInGodMode`, so admins can play normally without accidentally bypassing protections.

`Ward admin / Permit everyone` is a stronger global bypass mode. When it is enabled, ward access checks are not enforced and every player is treated as having ward admin access. Permitted lists are still stored, but they do not restrict access. This can also be used on multiplayer servers that do not need ward ownership restrictions or inactive ward expiration enforcement.

### Access protection from non-permitted players

The `Ward access from non-permitted players` config group controls what non-permitted players are blocked from using inside another player's active ward. These protections run on interaction points such as container opening, portal use, switch callbacks, pickup paths, saddle use, vehicle controls and station interactions.

The goal is to block unwanted use of another player's base without turning every object into an unconditional hard lock. Several categories have their own rules:

- **Food and feasts** are separate from item pickup. The food setting covers feast interaction and placed consumable item pieces.
- **Item pickup mode** controls non-consumable item drops. It can allow all pickups, block only player-dropped items, or block every non-food item pickup in protected areas.
- **Vehicles** use last-controller tracking. A player who drove a ship or dragged a cart into a protected area can still regain control or detach it, but the vehicle creator does not get automatic access inside someone else's ward.
- **Saddles and tames** use separate saddle-user tracking, so legitimate riding/mounting edge cases can be handled without broadly allowing tame access.
- **Portals** can allow teleporting while still blocking renaming, or block teleporting completely. Full portal blocking checks both the source and the destination side server-side.
- **Generic interactables** are an optional compatibility layer for interactables not covered by a dedicated patch. Dedicated protections are preferred when the object has special game logic.

Supported vanilla access protection includes:

- chests and containers;
- doors;
- plants and pickables;
- feast eating and placed consumable item pieces;
- configurable non-consumable item pickup modes, including allowing all pickups, blocking only player-dropped items, or blocking all non-food pickups;
- ships and ship containers;
- carts, wagons and battering rams;
- tames, saddles and pet interactions;
- production stations;
- crafting stations and station discovery;
- item stands and armor stands;
- portals, with separate modes for teleporting and renaming;
- map tables;
- fireplaces;
- shield generator fuel switches;
- obliterator/incinerator levers;
- turrets and ballistas;
- beds;
- catapults;
- archery targets;
- barber stations;
- traps;
- inactive wards inside another active ward;
- generic interactables as an optional broad compatibility layer.

Ownership-sensitive objects are handled carefully. A foreign ward should not trap a player's own movable/owned objects such as portals, tombstones, saddles or tames. Ships and carts use a last-controller exemption instead of creator-only access, so the same player who drove or dragged the vehicle into another ward can still regain control or detach it without granting extra access to the vehicle creator.

### Connected ward access modes

Several systems can share access across overlapping ward networks. Connected access is used only by systems whose own config points to a connected access mode; it does not automatically make every ward permission global.

Connected access is evaluated from the ward that protects the object being used. The selected mode decides whether other overlapping active player wards can grant access to that protected/root ward. Expired wards are not treated as active connected access sources for expiration refresh checks.

Available modes:

- `Off` - only direct access to the ward covering the object is accepted.
- `SameCreatorOnly` - access is shared only between overlapping wards created by the same player.
- `MutualTrust` - access is shared only between overlapping wards whose creators mutually permit/trust each other.
- `AnyConnected` - access to any ward in the overlapping network can grant access to the whole network. Intended for single-party/shared-base servers.

Access protection, background protection and expiration can use separate connected access settings. This allows strict interaction protection but looser background protection, or direct-only expiration with connected access for normal base use.

### Admin/server tools

#### Ward permitted-list commands

`pw_permit <player name>` / `ward_permit <player name>` adds an online player to the nearest ward's permitted list.

`pw_unpermit <player name>` / `ward_unpermit <player name>` removes a player from the nearest ward's permitted list. It matches the existing permitted list, so the player does not need to be online.

Both commands use `Ward admin / Enable external ward control commands` and `Ward admin / External ward control command range`.
They validate on the server that:

- the ward exists and is close enough;
- the requester has ward access;
- the target can be uniquely resolved;
- the requested permitted-list change is still valid.

#### Ward toggle commands

`pw_enable` / `ward_enable` enables the nearest ward within the configured command range.

`pw_disable` / `ward_disable` disables the nearest ward within the configured command range.

The commands use the same external ward control enable/range configs as the permitted-list commands.
They are creator/admin controlled: the ward creator may toggle their own ward, and players allowed by `Ward admin / Ward admin access` may toggle any nearby ward.

#### Ward expiration admin commands

`pw_set_expired` / `ward_set_expired` marks the nearest ward as expired.

`pw_set_unexpired` / `ward_set_unexpired` clears the expired state from the nearest ward.

The commands use the same external ward control enable/range configs as the permitted-list and toggle commands.
They are admin-only: the requester must be allowed by `Ward admin / Ward admin access`, or by `Ward admin / Permit everyone`.

#### Ward build limit

The server can limit how many wards each player may have in the world.

Existing wards are never removed. If a player already exceeds the configured limit, only newly built wards are blocked: after a new ward is placed, the server checks the tracked ward ZDO collection for that creator and destroys only the newly placed ward if the limit is exceeded.

### Background/passive protection

The `Ward without permitted players nearby` config group controls background protection for inactive public PvE bases when no permitted/effective-access player is nearby. This is separate from ordinary interaction blocking: it is meant to reduce offline grief and environmental/base damage while still allowing normal gameplay when an access player is present.

The background system can require a qualified base before broad protection is applied. Qualification can include a minimum number of player-built pieces inside the connected ward network. Presence detection is configurable: a permitted/effective player can be required near the protected object, anywhere inside the connected area, or simply online.

Configurable behavior includes:

- requiring a minimum number of player-built pieces in a connected ward network before broad background protection activates;
- detecting permitted/effective player presence by radius, by connected area, or by online status;
- blocking direct non-permitted player damage to structures;
- blocking all structure damage while no permitted/effective player is nearby;
- preventing fire/burning damage to structures while no permitted/effective player is nearby;
- protecting tames, boats and carts while no permitted/effective player is nearby;
- pacifying tamed creatures so they drop combat/static targets and do not acquire new targets while the base is protected;
- blocking non-permitted players from placing new pieces or demolishing other players' pieces while the base is protected.

Trap protection still lets permitted players move through their own traps safely. If a non-permitted player enters a qualified background-protected base, traps can still trigger against that player. Players can always demolish their own pieces even when background build/demolish protection is active.

### Inactive ward expiration

Inactive ward expiration is disabled by default.

This is a multiplayer/server-side mechanic and is ignored in singleplayer. When enabled, the server periodically checks its tracked ward ZDO collection. Wards expire after the configured number of real-time minutes without nearby activity from players who are allowed to refresh them. The check is skipped when `Ward admin / Permit everyone` is enabled. The check is also skipped while the server has no active character ZDOs, so an empty dedicated server does not age wards just because no one is online.

An expired ward is not deleted and its permitted list is preserved. The mod makes it behave like a disabled ward, which means another player can claim or reuse an abandoned area through normal disabled-ward behavior. This is intentional: expiration is an abandonment/takeover mechanic, not a hidden deletion system.

Activity and refresh rules:

- activity must come from a player character near the ward, using the ward's current radius as the horizontal activation range;
- `DirectPermitted` refresh mode accepts only the ward creator, directly permitted players and admin bypass;
- `EffectiveAccess` refresh mode can also accept access through connected/overlapping wards according to `Expiration connected access mode`;
- old wards are initialized with the current server time and do not expire immediately after enabling the feature;
- `Permit everyone` disables expiration enforcement because every player is treated as having access;
- singleplayer worlds ignore this system entirely.

Reactivation rules:

- `ManualInteraction` keeps expired wards inactive until an access player interacts with the ward;
- `AutomaticOnLogin` reactivates an expired ward when an access player is nearby during a periodic server check, or when an expired loaded ward wakes up near an access player;
- when a ward is reactivated, connected loaded wards that the same player can directly/admin access can also be activated so a linked base can recover together.

Admin tools:

- `pw_set_expired` / `ward_set_expired` marks the nearest ward as expired;
- `pw_set_unexpired` / `ward_set_unexpired` clears the expired state;
- optional expiration hover details show raw Unix timestamps and the last refreshing player only to players allowed by `Ward admin / Ward admin access`.

### Full protection

Classic protection options include:

- protect boars and hens from enemies and fire;
- protect structures from rain damage;
- protect ships from water damage or from all damage;
- protect plants from damage;
- protect fireplaces from players stepping on them;
- protect players from raids while sitting near an active fire;
- protect players from their own traps.

### Passive repair

Activate a ward to start passive repair of pieces in all connected ward areas. The ward repairs one piece every 10 seconds until all pieces are healthy, then stops.

### Passive door auto-closing

Doors inside a ward can be automatically closed after a configured delay after the last interaction.

### Multipliers

Inside ward areas, configurable multipliers can affect:

- player damage dealt/taken;
- tamed damage taken;
- structure and ship damage taken;
- fall damage taken;
- turret fire rate;
- food drain;
- stamina drain;
- skill drain on death;
- fireplace fuel drain;
- hammer durability drain;
- smelting, cooking, fermenting and sap collecting speed.

### Active offerings

Offer specific items to a ward to trigger useful effects:

- surtling core: instantly repair pieces;
- black core: augment structures by increasing health;
- food: start passive healing for players and tames;
- mead: share mead effects with players in connected areas;
- thunderstone: call Thor's wrath on enemies;
- trophy: kill enemies of the offered trophy type;
- Ymir flesh: grow healthy plants;
- Eitr x5: grow plants regardless of normal requirements;
- dragon egg: activate Moder power for players;
- selected travel items: call a taxi to distant locations.

By default, offerings are still available to non-permitted players. A separate opt-in config can restrict offerings to permitted/effective-access players.

### Taxi

The taxi offering can move the player to selected distant locations and then bring them back.

Supported destinations include:

- Sacrificial Stones with a boss trophy;
- Haldor with coins;
- Hildir with Hildir chests or a Fuling totem;
- Bog Witch with pukeberries.

The taxi waits if the player is sleeping, in a dungeon, sitting, attached to a ship, riding, teleporting, or using a hammer. The return flight can be ended early with `AltPlace + Use` (`Left Shift + E` by default).

Restrictions:

- the player cannot be encumbered;
- the player must be teleportable;
- the target point must be at least 300 meters away;
- another taxi trip cannot start while a return trip is pending.

## Localization

Some messages and captions use suitable vanilla localization lines. The rest is localized by the mod.

To add your own localization, create a file named `Protective Wards.LanguageName.yml` or `Protective Wards.LanguageName.json` anywhere inside the BepInEx folder. For example, to add French translations you can create `Protective Wards.French.yml` inside the config folder.

Localization files are loaded on game launch or language change.

You can send localization files through [GitHub](https://github.com/shudnal/ProtectiveWards/issues) or [Nexus](https://www.nexusmods.com/valheim/mods/2450?tab=posts).

[Language list](https://valheim-modding.github.io/Jotunn/data/localization/language-list.html)

English localization example is located in `Protective Wards.English.json` next to the plugin DLL.

## Installation

Extract `ProtectiveWards.dll` to your `BepInEx/Plugins` folder.

For servers, install the mod on the dedicated server and on all clients.

## Configuration

The recommended way to edit configs is with a configuration manager:

- [Configuration Manager](https://thunderstore.io/c/valheim/p/shudnal/ConfigurationManager/)
- [Official BepInEx Configuration Manager](https://valheim.thunderstore.io/package/Azumatt/Official_BepInEx_ConfigurationManager/)

Server-synced settings are admin-only. Client-only display settings are marked as not synced.

## Compatibility

The mod tries to keep patches focused and non-invasive. Broad generic interaction protection is optional and should be enabled carefully on heavily modded servers.

## Mirrors

[Nexus](https://www.nexusmods.com/valheim/mods/2450)

## Donation

[Buy Me a Coffee](https://buymeacoffee.com/shudnal)

## Discord

[Join server](https://discord.gg/e3UtQB8GFK)
