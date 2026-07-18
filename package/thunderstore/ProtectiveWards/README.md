# Protective Wards

![logo](https://staticdelivery.nexusmods.com/mods/3667/images/2450/2450-1689565569-1699140464.png)

Configurable ward protection, access control, passive base support, server-side privacy tools, multipliers and active offerings for Valheim.

Protective Wards is designed primarily for public PvE servers. It helps prevent non-permitted players from casually using, taking, moving or changing objects inside another player's warded base. It is not intended to be a PvP raid system.

## Requirements

- BepInExPack Valheim
- Jotunn 2.29.1 or a newer compatible 2.x version
- YamlDotNet

The mod uses Jotunn `EveryoneMustHaveMod` network compatibility. In multiplayer it must be installed on the server and every client. Gameplay settings are server-synchronized; client-only visual settings are marked `[Not Synced with Server]`.

## Features

- per-ward range and visual settings;
- configurable protection for containers, doors, portals, stations, vehicles, tames and other interactables;
- connected ward networks with several access-sharing modes;
- cylindrical or spherical ward coverage;
- optional protection of dungeon interiors through a warded outside entrance;
- offline/background protection while no permitted player is nearby;
- inactive ward expiration and abandonment rules;
- ward build limits and server-validated management commands;
- passive repair and automatic door closing;
- damage, drain and production-speed multipliers;
- active offerings, including Valkyrie passage travel.

## Access model

- **Direct access**: ward creator, directly permitted player, or configured admin bypass.
- **Connected/effective access**: access inherited through overlapping active wards according to the selected connected access mode.
- **Ownership exemptions**: narrow exceptions for objects where a foreign ward should not trap a player's own property, such as tombstones, saddles and previously controlled vehicles.
- **Permit everyone**: a global bypass that treats every player as having ward admin access.

Sensitive actions and console commands are validated by the server. The server checks the target ward, distance, access and requested state before applying changes.

## Per-ward settings

Each ward can store its own range and visual overrides in its ZDO.

1. Disable the ward.
2. Press `AltPlace + Use` (`Left Shift + E` by default).
3. Change the range, emission, bubble or area-circle settings.
4. Apply the settings.

| Config | Meaning |
|---|---|
| `Ward settings / Use default values for wards without custom settings` | Wards without saved overrides use the global range and visual defaults. |
| `Ward settings / Only creator can edit ward settings` | Only the creator may edit the ward instead of any player with access. |
| `Ward settings / Admins can edit ward settings` | Players accepted by `Ward admin access` may edit any ward. |

## Ward coverage

| Config/value | Meaning |
|---|---|
| `Ward settings / Protected area shape = Cylinder` | Default. Uses horizontal XZ distance and ignores height. |
| `Ward settings / Protected area shape = Sphere` | Uses full 3D distance from the ward. |
| `Ward settings / Protect dungeon interiors through warded entrances` | When enabled, an interior inherits protection if its external `Teleport` entrance is inside an active ward. |

Interior objects are never matched against wards at their high-altitude dungeon position. The mod resolves the outside `Location`, ignores its `m_interiorTransform` hierarchy, finds the external entrance and checks that position instead.

Background protection intentionally keeps horizontal checks for movable boats, carts and tames so waves and physics do not move them in and out of protection because of vertical displacement.

## Connected ward access

Connected access is configured separately for normal interactions, background protection and expiration refresh.

| Mode | Meaning |
|---|---|
| `Off` | Only direct access to the ward protecting the object is accepted. |
| `SameCreatorOnly` | Access is shared only through overlapping wards with the same creator. |
| `MutualTrust` | Access is shared only when the creators of overlapping wards mutually permit each other. |
| `AnyConnected` | Access to any ward grants access through the connected network; intended for shared-base or single-party servers. |

## Access protection

The `Ward access from non-permitted players` group controls interaction blocking inside active wards.

Supported vanilla categories include containers, doors, plants, food and feasts, dropped items, ships, carts, tames, production and crafting stations, item and armor stands, portals, map tables, fireplaces, shield generators, obliterators, turrets, beds, catapults, archery targets, barber stations, traps and inactive wards.

`Generic interactables` is an optional broad compatibility layer for vanilla or modded `Interactable` implementations without dedicated handling. It is disabled by default because ownership-sensitive objects are safer with dedicated patches.

### Portal access mode

| Mode | Meaning |
|---|---|
| `AllowAll` | Non-permitted players may use and rename portals. |
| `AllowTeleportOnly` | Default. Teleporting is allowed, but changing portal tags is blocked. |
| `BlockAll` | Teleporting and renaming are blocked; both source and destination are validated server-side. |

### Item pickup mode

| Mode | Meaning |
|---|---|
| `AllowAll` | All non-consumable drops may be picked up. |
| `AllowNonPlayerDropped` | Default. Normal loot/world drops are allowed, but player-dropped items are protected. |
| `BlockAll` | All non-consumable item pickup is blocked in protected areas. |

Vehicles use last-controller tracking: a player who drove a ship or dragged a cart into another ward can regain control or detach it without granting the same exemption to the creator or other players.

## Background protection

The `Ward without permitted players nearby` group protects inactive public PvE bases while no permitted/effective-access player is present.

### Permitted player presence mode

| Mode | Meaning |
|---|---|
| `PermittedNearProtectedArea` | A permitted player must be within the configured horizontal radius of the protected object. |
| `PermittedInsideConnectedArea` | A permitted player anywhere inside the connected ward area disables background protection. |
| `PermittedOnline` | A permitted player being online is enough to disable background protection. |

### Background protection mode

| Mode | Meaning |
|---|---|
| `Off` | No broad structure background protection. |
| `BlockNonPermittedPlayerDamage` | Blocks direct structure damage caused by non-permitted players. |
| `BlockAllDamageWhenNoPermittedNearby` | Blocks structure damage from all sources while no permitted/effective player is present. |

Other settings can require a minimum number of player-built pieces, prevent structure fire damage, protect tames/boats/carts, pacify tames, stop tames damaging structures, and block non-permitted building or demolition. Players may always demolish their own pieces.

## Inactive ward expiration

Expiration is disabled by default, works server-side in multiplayer and is ignored in singleplayer.

`Ward expiration / Expiration minutes` sets the inactivity period; `0` disables the feature. Expired wards are not deleted. Their permitted lists remain stored, but they intentionally behave like disabled wards so abandoned areas can be reclaimed through normal ward behavior.

### Expiration refresh mode

| Mode | Meaning |
|---|---|
| `DirectPermitted` | Creator, directly permitted player or admin/global bypass may refresh the ward. |
| `EffectiveAccess` | Default. Connected ward access may also refresh it according to `Expiration connected access mode`. |

### Expiration reactivation mode

| Mode | Meaning |
|---|---|
| `ManualInteraction` | Default. An access player must interact with the expired ward. |
| `AutomaticOnLogin` | The server may reactivate the ward when an access player is nearby during a check or when the loaded ward wakes up. |

Old wards receive a current timestamp when expiration is enabled, so existing worlds do not immediately lose every ward. `Ward admin / Permit everyone` disables expiration enforcement.

## Ward administration

### Ward admin access

| Mode | Meaning |
|---|---|
| `Off` | Administrators do not bypass ward checks. |
| `Admins` | Server administrators and the host bypass ward checks. |
| `AdminsInGodMode` | Default. Administrators bypass ward checks only while god mode is active. |

`Ward admin / Permit everyone` is stronger than the admin mode. It bypasses ward ownership restrictions for every player while preserving stored permitted lists.

### External ward commands

Commands are controlled by `Ward admin / Enable external ward control commands` and `Ward admin / External ward control command range`.

| Command | Alias | Description |
|---|---|---|
| `pw_permit <player name>` | `ward_permit <player name>` | Adds a uniquely matched online player to the nearest ward. |
| `pw_unpermit <player name>` | `ward_unpermit <player name>` | Removes a matching player from the nearest ward's stored list; the player may be offline. |
| `pw_enable` | `ward_enable` | Enables the nearest ward. Creator or configured ward admin. |
| `pw_disable` | `ward_disable` | Disables the nearest ward. Creator or configured ward admin. |
| `pw_set_expired` | `ward_set_expired` | Marks the nearest ward as expired. Admin-only. |
| `pw_set_unexpired` | `ward_set_unexpired` | Clears the expired state. Admin-only. |

All commands are revalidated on the server. Permit commands require ward access; expiration commands require `Ward admin access` or `Permit everyone`.

`Ward admin / Ward build limit per player` limits wards per owner. `0` disables the limit. Existing wards are never removed; only the newly placed ward is destroyed when it would exceed the limit.

## Full and passive protection

`Ward protects` options can protect boars and hens, structures from rain, ships from selected damage, plants, fireplaces from step damage, players from their own traps, and sitting players near an active fire from raids.

Passive options include repair of one piece every 10 seconds across connected areas, optional repair of non-player location structures, optional crafting-station requirements, and automatic door closing.

## Multipliers

Ward-area multipliers cover damage dealt/taken, structure and ship damage, fall damage, turret fire rate, food/stamina/skill/fuel/durability drain, and smelting/cooking/fermenting/sap-collecting speed.

`1` keeps vanilla behavior. Values below or above `1` reduce/increase the relevant effect or slow/speed the process according to the config description.

## Active offerings

Enabled offerings are shown in the Valheim Compendium under `Ward and offerings`. If the vanilla ward text is unavailable, the topic is still added once the player knows the ward recipe. Ward hover keeps only a short hint.

Available effects include instant repair, structure augmentation, passive healing, shared mead effects, Thor's wrath, trophy-based creature killing, plant growth, Moder power and Valkyrie passage travel.

`Offerings / Protect from non-permitted players` can restrict offerings to players with direct or connected access. It is disabled by default so visitors may make offerings.

## Valkyrie passage

Travel destinations include the Sacrificial Stones, Haldor, Hildir, the Bog Witch and optional Eikthyr, Elder, Bonemass, Moder, Yagluth, Queen and Fader altars. Boss altar routes are disabled by default and have separate item, amount and consumption settings. Hildir chests are never consumed.

Configured item names may use a prefab name such as `Coins`, a localization token such as `$item_coins`, or a localized item name when ObjectDB and localization data are available.

| Config | Meaning |
|---|---|
| `Offerings - Taxi / Seconds to fly back` | Delay before the return flight; `0` makes the trip one-way. |
| `Offerings - Taxi / Seconds to wait for return flight` | Maximum time to wait until the player becomes ready to return; `0` disables the timeout. |
| `Offerings - Taxi / Active passage handling = RejectNewPassage` | Keeps the current passage and rejects another offering. |
| `Offerings - Taxi / Active passage handling = StopActivePassage` | Stops the current passage without starting another; offer again to start a new one. |
| `Misc / Maximum taxi speed` | Client-side speed cap; `60` by default. |

During flight, `AltPlace + Use` (`Left Shift + E` by default) requests an immediate drop and cancels the return pickup.

The player cannot be encumbered and must be teleportable, except that carried Hildir chests are ignored for this check. The destination must exist and be at least 200 meters away before an item can be consumed.

## Installation and configuration

Install with a mod manager, or place `ProtectiveWards.dll` in `BepInEx/Plugins`. For multiplayer, install it on the server and every client.

Recommended configuration managers:

- [Configuration Manager](https://thunderstore.io/c/valheim/p/shudnal/ConfigurationManager/)
- [Official BepInEx Configuration Manager](https://valheim.thunderstore.io/package/Azumatt/Official_BepInEx_ConfigurationManager/)

Server-synchronized settings are admin-only in configuration managers.

## Localization

Create `Protective Wards.LanguageName.yml` or `Protective Wards.LanguageName.json` anywhere under the BepInEx directory. Files are loaded on game launch and language changes. The English example is included next to the DLL as `Protective Wards.English.json`.

- [Jotunn language list](https://valheim-modding.github.io/Jotunn/data/localization/language-list.html)

## Compatibility

Dedicated patches are used where objects have special ownership or interaction logic. The optional generic-interactable protection is intentionally broad and should be enabled carefully on heavily modded servers.

## Links

- [GitHub](https://github.com/shudnal/ProtectiveWards)
- [Nexus](https://www.nexusmods.com/valheim/mods/2450)
- [Discord](https://discord.gg/e3UtQB8GFK)
- [Buy Me a Coffee](https://buymeacoffee.com/shudnal)
