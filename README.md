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

### Per-ward settings

`Ward settings / Ward settings mode` controls whether individual ward state is available:

- `PerWard` is the default. Authorized players can open the settings window and stored per-ward range, visual and access overrides are applied.
- `ServerControlled` removes the settings action from the ward hover, blocks opening and applying the window on both client and server, and ignores all stored per-ward overrides. Existing values remain in the ward ZDO and become active again after switching back to `PerWard`. Global config values are used while the server-controlled mode is active.

Each ward can store its own range, visual and access settings in the ward ZDO while `PerWard` mode is active.

To edit a ward:

1. Disable the ward.
2. Press `AltPlace + Use` (`Left Shift + E` by default) on the ward.
3. Change values in the settings window. Glow, sphere and ward circle controls are grouped on the `Ward visuals` page.
4. Use the main Access section to add an online player by nickname, and open the `Permitted players` page to review or remove stored entries.
5. Apply settings from the main settings page.

You can customize:

- ward range;
- emission color and multiplier;
- ward sphere visibility and color;
- detailed ward sphere shader properties;
- ward circle colors, width, line amount and animation speed;
- the ward-specific `Permit everyone` access policy;
- the explicit permitted-player list, including server-validated add and remove actions;
- optional access for one bound guild when Guilds is installed.

Most per-ward values can inherit their corresponding global config. `Permit everyone` follows the same override model: an unchanged ward uses `Ward access / Permit everyone`, while a ward-specific value can make one public ward in an otherwise private world or keep one private ward when the global default is public. The Access settings page explains that enabling the effective value treats every player as permitted. Per-ward `Permit everyone`, guild access and password access are ignored in `ServerControlled` mode.

The main Access section can add an online player by an exact or uniquely matching nickname. The **Permitted players** page shows the ward's current explicit permitted list in pages of ten and lets authorized editors remove stored players even while they are offline. Every change is revalidated by the server against the tracked ward ZDO, requester identity and edit permission.

Disabled wards owned by another player cannot be edited. Admin bypass is controlled by `Ward admin / Ward admin access`:

- `Off` - admins do not bypass ward access checks;
- `Admins` - server admins and host bypass ward access checks;
- `AdminsInGodMode` - server admins and host bypass ward access checks only while god mode is enabled.

The default is `AdminsInGodMode`, so admins can play normally without accidentally bypassing protections.

`Ward access / Permit everyone` is the default value for wards without an override. When effective for a ward, its access checks are bypassed for every player while its permitted list remains stored. Wards whose effective value is enabled are also excluded from inactive ward expiration. The config was moved from the `Ward admin` group; existing enabled values must be enabled again under the new group.

### Guild access

When Guilds is installed on the server and clients and `Ward settings mode` is `PerWard`, one guild can be bound to each ward from the ward's **Access settings** page. Binding the current guild enables guild access immediately; it can later be disabled without removing the binding, or unbound completely.

Current members of the bound guild receive normal permitted-equivalent access while they remain in that guild. Membership is resolved from the current server-synchronized guild data and is validated by the server. The member list is not copied into the ward ZDO. If a guild is renamed, rebind affected wards so the stored identity matches the renamed guild.

Guild access is additive: the creator, directly permitted players, password-enrolled players and configured admin access continue to work normally.

Ward hover text presents a concise effective access summary. It lists short explicit permitted names, switches to a player count when the combined names exceed 30 characters, and appends active guild and password access. When effective `Permit everyone` is enabled, the summary is simply `Permitted: everyone`.

### Access protection from non-permitted players

The `Ward access from non-permitted players` config group controls what non-permitted players are blocked from using inside another player's active ward.

`Ward settings / Protected area shape` controls normal ward coverage and connected ward overlap. `Cylinder` is the default and uses horizontal XZ distance, matching vanilla behavior. `Sphere` uses full 3D distance from the ward. Background protection keeps horizontal coverage for movable objects, but interior targets are still resolved through their dungeon entrance.

`Ward settings / Protect dungeon interiors through warded entrances` controls interior inheritance. When enabled, an interior is protected only when its outside dungeon entrance is covered by an active ward; all individually enabled protection rules then apply throughout that interior. When disabled, outside wards do not protect dungeon interiors.

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
- crafting stations and station discovery, including the EpicLoot enchanting table when EpicLoot is installed;
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

Several systems can share access across overlapping ward networks.

Available modes:

- `Off` - only direct access to the ward covering the object is accepted.
- `SameCreatorOnly` - access is shared only between overlapping wards created by the same player.
- `MutualTrust` - access is shared only between overlapping wards whose creators mutually permit/trust each other.
- `AnyConnected` - access to any ward in the overlapping network can grant access to the whole network. Intended for single-party/shared-base servers.

Access protection, background protection and expiration can use separate connected access settings.

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
They are admin-only: the requester must be allowed by `Ward admin / Ward admin access`, or by the effective `Permit everyone` value of the selected ward.

#### Ward build limit

The server can limit how many wards each player may have in the world.

Existing wards are never removed. If a player already exceeds the configured limit, only newly built wards are blocked: after a new ward is placed, the server checks the tracked ward ZDO collection for that creator and destroys only the newly placed ward if the limit is exceeded.

### Background/passive protection

The `Ward without permitted players nearby` config group controls background protection for inactive public PvE bases when no permitted/effective-access player is nearby. Background protection resolves ward coverage, connected access and ownership from the server-tracked ward ZDO collection, so the ward centre does not need a loaded scene instance.

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

Inactive ward expiration is disabled by default. Wards whose effective per-ward `Permit everyone` value is enabled are excluded from expiration enforcement.

This is a multiplayer/server-side mechanic and is ignored in singleplayer. When enabled, the server periodically checks the tracked ward ZDO collection. Wards expire after the configured number of real-time minutes without nearby activity from players who can refresh them.

Important details:

- expired wards are disabled, not deleted;
- permitted lists are preserved;
- old wards are initialized with the current server time and do not expire immediately after enabling the feature;
- expiration can be refreshed by nearby direct permitted players or by nearby effective connected access, depending on configuration;
- reactivation can be manual by interacting with the ward, or automatic when an access player is nearby or an expired ward wakes up near an access player;
- optional expiration hover debug details are shown only to players allowed by `Ward admin / Ward admin access`.

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
- selected travel items: open a Valkyrie passage to distant locations.

By default, offerings are still available to non-permitted players. A separate opt-in config can restrict offerings to permitted/effective-access players.

When the player knows the vanilla ward text, the Valheim Compendium also shows a `Ward and offerings` topic under the raven icon. It lists the currently recognizable offerings and their effects. The ward hover only keeps a short reminder instead of showing the full offering list.

### Valkyrie passage

A Valkyrie passage offering can carry the player to selected distant locations and optionally bring them back.

Supported destinations include:

- Sacrificial Stones with a boss trophy;
- Haldor with the configured Haldor passage item, coins by default. The amount depends on whether Haldor is already discovered;
- Hildir with Hildir chests or the configured Hildir travel item. Hildir chests are never consumed; the configured Hildir item defaults to Linen thread x50;
- Bog Witch with the configured Bog Witch travel item and amount;
- optional boss altar destinations for Eikthyr, Elder, Bonemass, Moder, Yagluth, Queen, and Fader. These routes are disabled by default and can be configured with their own offering item, item amount, and consume setting.

Each main destination can be enabled or disabled separately. Most Valkyrie passage offerings also have a separate setting controlling whether the offered item is consumed. Hildir chest passage is always free and does not consume the chest. Boss altar locations are fixed to vanilla world locations; if the target location is missing in the world, no item is consumed.

Valkyrie passage item configs accept several item name forms:

- item prefab name, for example `Coins`;
- localization token, for example `$item_coins`;
- localized item name from the current game language, if ObjectDB and localization data are already available.

The mod resolves configured item names to the internal localization token before comparing them with inventory items.

The passage starts through the `Valkyrie passage` status effect with the Celestial feather icon. If return flight is enabled, the same status effect shows the return timer. During the flight, `AltPlace + Use` (`Left Shift + E` by default) makes the Valkyrie drop you immediately. No Valkyrie will return to pick you up after that. Set `Offerings - Taxi / Seconds to fly back` to `0` to disable the return flight.

Restrictions:

- the player cannot be encumbered;
- the player must be teleportable, except Hildir chests are ignored for this check;
- the target point must be at least 300 meters away;
- another passage cannot start while a return trip is pending unless the active-passage handling config is set to stop the current passage first.

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

Optional compatibility includes server-validated one-guild-per-ward access for Guilds and dedicated crafting-station access protection for the EpicLoot enchanting table.

## Mirrors

[Nexus](https://www.nexusmods.com/valheim/mods/2450)

## Donation

[Buy Me a Coffee](https://buymeacoffee.com/shudnal)

## Discord

[Join server](https://discord.gg/e3UtQB8GFK)
