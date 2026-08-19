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

- per-ward range, visual settings, access policy, permitted-player management, guild binding and optional password access;
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

- **Direct access**: ward creator, directly permitted player, member of the bound guild, effective `Permit everyone`, or configured admin bypass.
- **Connected/effective access**: access inherited through overlapping active wards according to the selected connected access mode.
- **Ownership exemptions**: narrow exceptions for objects where a foreign ward should not trap a player's own property, such as tombstones, saddles and previously controlled vehicles.
- **Permit everyone**: a global default that each ward may inherit or override independently.

Sensitive actions and console commands are validated by the server. The server checks the requester identity, target ward ZDO, access and requested state before applying changes.

## Per-ward settings

`Ward settings / Ward settings mode` has two values:

- `PerWard` is the default. Authorized players can open the settings window and stored per-ward range, visual and access overrides are applied.
- `ServerControlled` hides the ward settings action, blocks the UI and its server RPCs, and ignores all stored per-ward overrides in favor of global config values. Stored values are preserved and become active again after returning to `PerWard`.

Each ward can store its own range, visual and access overrides in its ZDO while `PerWard` mode is active.

1. Disable the ward.
2. Press `AltPlace + Use` (`Left Shift + E` by default).
3. Change the range, access settings, or glow, sphere and ward circle controls on the **Ward visuals** page.
4. Use the main **Access** section to add an online player by nickname, and open the **Permitted players** page to review or remove stored entries.
5. Apply the settings from the main page.

| Config | Meaning |
|---|---|
| `Ward settings / Ward settings mode` | `PerWard` enables the ward settings UI and stored overrides. `ServerControlled` hides and blocks the UI, ignores stored overrides, and uses global config values. |
| `Ward settings / Use default values for wards without custom settings` | Wards without saved overrides use the global range and visual defaults. Ignored in `ServerControlled` mode. |
| `Ward settings / Only creator can edit ward settings` | Only the creator may edit the ward instead of any player with access. Ignored in `ServerControlled` mode. |
| `Ward settings / Admins can edit ward settings` | Players accepted by `Ward admin access`, or by the ward's effective `Permit everyone` value, may edit any ward. Ignored in `ServerControlled` mode. |

The **Access settings** page contains a per-ward `Permit everyone` value with the same **Use default** behavior as the visual settings. Its inline note explains that enabling the effective value treats every player as permitted. This allows a public ward while the global default is private, or a private ward while the global default is public. When effective for a ward, all players pass its access checks and that ward is excluded from inactive expiration. The global default is now `Ward access / Permit everyone`; values previously enabled under `Ward admin` must be enabled again in the new group. Per-ward access overrides are ignored in `ServerControlled` mode.

The main Access section can add an online player by an exact or uniquely matching nickname. The **Permitted players** page displays the ward's explicit permitted list in pages of ten and lets authorized editors remove stored entries even if those players are offline. The server revalidates the requester identity, target ward ZDO and edit permission before changing the list.

Ward hover text summarizes effective access without allowing long name lists to dominate the hover. Explicit names are shown while their combined text is at most 30 characters; longer lists are replaced with a player count. Active guild and password access are appended to the same line. Effective `Permit everyone` is shown simply as `Permitted: everyone`.

## Guild access

When Guilds is installed on the server and clients and `Ward settings mode` is `PerWard`, the **Access settings** page can bind one guild to the ward.

- **Bind guild** stores the requester's current guild and enables guild access.
- **Enable guild access** can temporarily disable or re-enable the stored binding.
- **Unbind guild** removes the binding completely.
- Current guild members receive normal permitted-equivalent access while they remain members.
- Guild membership is checked against current server-synchronized data and validated by the server; members are not copied into the ward's permitted list.
- Guild access is additive and does not remove creator, explicit permitted, password or admin access.

A ward stores both the guild ID and name. Rebind wards after renaming a guild.

## Ward password protection

A ward can optionally let a non-permitted player join its permitted list by entering a per-ward password. Password access is disabled for each ward until it is explicitly enabled in that ward's settings, and it is ignored while `Ward settings mode` is `ServerControlled`.

1. Disable the ward and open its settings with `AltPlace + Use`.
2. Use the **Password protection** group on the **Access settings** page.
3. Set a password and enable password access.
4. A non-permitted player can approach either an active or inactive ward, press `Use`, enter the password, and be added directly to that ward's permitted list after server validation.

Password entry does not grant connected-network ownership or admin rights. It adds the player only to the selected ward's normal permitted list.

| Config/value | Default | Meaning |
|---|---:|---|
| `Ward password protection / Password field mode = SetNewPasswordOnly` | Yes | The current password is never displayed. A new password is changed only with **Set new password**. The ZDO stores a random salt and PBKDF2 hash, not the readable password. |
| `Ward password protection / Password field mode = EditablePassword` | No | Shows a normal password field and saves it with the rest of the ward settings. To make that possible, the readable password is also stored in the ward ZDO. A password previously created in hash-only mode cannot be recovered or displayed; it remains valid until replaced. |
| `Ward password protection / Password change access = CreatorOnly` | Yes | Only the ward creator may change or remove the password. Configured ward admins and players covered by the ward's effective `Permit everyone` value may still edit it. |
| `Ward password protection / Password change access = CreatorAndPermitted` | No | The creator and players with direct permitted-equivalent access, including explicit permitted players and bound guild members, may change or remove the password. |

Password checks and permitted-list changes are performed on the server against the target ward ZDO. Passwords are case-sensitive and limited to 64 characters. Hash-only storage prevents the password from being read directly, but weak passwords may still be guessed by anyone who can inspect and copy ward ZDO data.

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

Supported vanilla categories include containers, doors, plants, food and feasts, dropped items, ships, carts, tames, production and crafting stations, item and armor stands, portals, map tables, fireplaces, shield generators, obliterators, turrets, beds, catapults, archery targets, barber stations, traps and inactive wards. When EpicLoot is installed, its enchanting table is handled as a crafting station by the same crafting-station access setting.

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

The `Ward without permitted players nearby` group protects inactive public PvE bases while no permitted/effective-access player is present. It uses the server-tracked ward ZDO collection rather than requiring loaded ward scene instances.

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
| `DirectPermitted` | Creator, directly permitted player, bound guild member, effective `Permit everyone`, or admin bypass may refresh the ward. |
| `EffectiveAccess` | Default. Connected ward access may also refresh it according to `Expiration connected access mode`. |

### Expiration reactivation mode

| Mode | Meaning |
|---|---|
| `ManualInteraction` | Default. An access player must interact with the expired ward. |
| `AutomaticOnLogin` | The server may reactivate the ward when an access player is nearby during a periodic check. |

Old wards receive a current timestamp when expiration is enabled, so existing worlds do not immediately lose every ward. Expiration is skipped independently for every ward whose effective `Permit everyone` value is enabled.

## Ward administration

### Ward admin access

| Mode | Meaning |
|---|---|
| `Off` | Administrators do not bypass ward checks. |
| `Admins` | Server administrators and the host bypass ward checks. |
| `AdminsInGodMode` | Default. Administrators bypass ward checks only while god mode is active. |

`Ward access / Permit everyone` supplies the default for wards without a local override. An effective value of `true` bypasses that ward's ownership restrictions for every player while preserving its stored permitted list. This config moved from `Ward admin`; existing enabled values must be enabled again in the new group.

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

All commands are revalidated on the server. Permit commands require ward access; expiration commands require `Ward admin access` or the selected ward's effective `Permit everyone` value.

`Ward admin / Ward build limit per player` limits wards per owner. `0` disables the limit. Existing wards are never removed; only the newly placed ward is destroyed when it would exceed the limit.

## Full and passive protection

`Ward protects` options can protect boars and hens, structures from rain, ships from selected damage, plants, fireplaces from step damage, players from their own traps, and sitting players near an active fire from raids.

Passive options include repair of one piece every 10 seconds across connected areas, optional repair of non-player location structures, optional crafting-station requirements, and automatic door closing.

## Multipliers

Ward-area multipliers cover damage dealt/taken, structure and ship damage, fall damage, turret fire rate, food/stamina/skill/fuel/durability drain, and smelting/cooking/fermenting/sap-collecting speed.

`1` keeps vanilla behavior. Values below or above `1` reduce/increase the relevant effect or slow/speed the process according to the config description.

## Active offerings

Enabled offerings known to the player are shown in the Valheim Compendium under `Ward and offerings`. If the vanilla ward text is unavailable, the topic is still added once the player knows the ward recipe. `Misc / Show offerings in hover` is enabled by default and controls both the short ward-hover hint and this Compendium topic; it is client-only.

All primary offering types are enabled by default. Offerings 1-9 use fixed tribute items, amounts and effects; their individual `Offerings` booleans enable or disable them. Valkyrie passage destinations, prices, amounts and consumption rules have additional settings.

| Offering config | Default tribute | Default state | Effect and configuration |
|---|---|---:|---|
| `1 - Repair all pieces by surtling core offering` | Surtling core x1 | Enabled | Instantly repairs damaged pieces in all connected ward areas. The core is not consumed when nothing can be repaired. |
| `2 - Augment all pieces by black core offering` | Black core x1 | Enabled | Augments structural pieces in all connected areas by increasing their maximum health. The core is not consumed when no piece can be repaired or augmented. |
| `3 - Heal all allies for 3 min by food offering` | Any food item x1 | Enabled | Consumes one food item and starts three minutes of passive healing for players and tames in connected areas. Better food produces stronger healing. Each ward has its own active healing period; food is not consumed while that ward is already healing. |
| `4 - Share mead effect to all players by mead offering` | Any usable mead x1 | Enabled | Shares the mead effect with eligible players in connected areas. The mead is not consumed when no player can receive the effect. |
| `5 - Call the wrath of the Thor upon your enemies by thunderstone offering` | Thunder stone x1 | Enabled | Strikes enemies in connected areas. The thunder stone is consumed even when no target is damaged. |
| `6 - Kill all enemies of the same type by trophy offering` | Any creature trophy x1 | Enabled | Kills creatures matching the offered trophy in connected areas. The trophy is not consumed when no matching creature is affected. |
| `7 - Grow all plants by Ymir flesh offering` | Ymir flesh x1 | Enabled | Instantly advances healthy plants in connected areas. The item is not consumed when no plant can grow. |
| `8 - Grow all plants regardless the requirements by Eitr x5 offering` | Refined Eitr x5 | Enabled | Grows plants even where their normal biome or placement requirements are not met. Eitr is not consumed when no plant can grow. |
| `9 - Activate Moder power by dragon egg offering` | Dragon egg x1 | Enabled | Consumes the egg and applies Moder's forsaken power to players in connected areas after Moder has been defeated. |
| `10 - Fly back and forth to distant point by different items offering` | Destination-specific | Enabled | Enables the Valkyrie passage system. Every destination, price, amount and consume rule is configured in the taxi groups below. |

`Offerings / Protect from non-permitted players` is **disabled by default**. While disabled, visitors may use enabled offerings. When enabled, the server requires direct or configured connected ward access.

### Valkyrie passage offerings

The master switch is `Offerings / 10 - Fly back and forth to distant point by different items offering`, enabled by default.

| Destination/config | Default tribute | Enabled by default | Consumed by default | Configurable values |
|---|---|---:|---:|---|
| Sacrificial Stones | One boss trophy | Yes | No | `Sacrificial Stones taxi`; `Sacrificial Stones taxi consumes boss trophy` |
| Haldor, undiscovered | `Coins` x2000 | Yes | Yes | Enabled state, item prefab/token/localized name, undiscovered price, discovered price and consume flag |
| Haldor, discovered | `Coins` x500 | Yes | Yes | Same Haldor settings |
| Hildir chest route | Any Hildir quest chest | Yes | Never | `Hildir taxi` and separate `Hildir chest taxi` switches; the chest is always retained |
| Hildir item route | `LinenThread` x50 | Yes | Yes | Enabled state, item, amount and consume flag |
| Bog Witch | `Pukeberries` x20 | Yes | Yes | Enabled state, item, amount and consume flag |
| Eikthyr altar | `TrophyDeer` x10 | No | Yes | Per-altar enabled state, item, amount and consume flag |
| Elder altar | `AncientSeed` x6 | No | Yes | Per-altar enabled state, item, amount and consume flag |
| Bonemass altar | `WitheredBone` x30 | No | Yes | Per-altar enabled state, item, amount and consume flag |
| Moder altar | `FreezeGland` x50 | No | Yes | Per-altar enabled state, item, amount and consume flag |
| Yagluth altar | `GoblinTotem` x10 | No | Yes | Per-altar enabled state, item, amount and consume flag |
| Queen altar | `DvergrKey` x1 | No | Yes | Per-altar enabled state, item, amount and consume flag |
| Fader altar | `Bell` x1 | No | Yes | Per-altar enabled state, item, amount and consume flag |

Taxi item settings accept an item prefab name such as `Coins`, a localization token such as `$item_coins`, or a localized item name when ObjectDB and localization data are available.

| Other taxi config | Default | Meaning |
|---|---:|---|
| `Offerings - Taxi / Seconds to fly back` | `120` | Delay before the return flight; `0` makes the trip one-way. |
| `Offerings - Taxi / Seconds to wait for return flight` | `600` | Maximum time to wait until the player becomes ready to return; `0` disables this timeout. |
| `Offerings - Taxi / Active passage handling` | `RejectNewPassage` | Either rejects a new offering while a passage is active or stops the old passage first. |
| `Misc / Maximum taxi speed` | `60` | Client-side maximum flight speed. |

During flight, `AltPlace + Use` (`Left Shift + E` by default) requests an immediate drop and cancels the return pickup.

The player cannot be encumbered and must be teleportable, except that carried Hildir chests are ignored for this check. The destination must exist and be at least 200 meters away before an item can be consumed.

## Installation and configuration

Install with a mod manager, or place `ProtectiveWards.dll` in `BepInEx/Plugins`. For multiplayer, install it on the server and every client.

Recommended configuration managers:

- [Configuration Manager](https://thunderstore.io/c/valheim/p/shudnal/ConfigurationManager/)
- [Official BepInEx Configuration Manager](https://valheim.thunderstore.io/package/Azumatt/Official_BepInEx_ConfigurationManager/)

Server-synchronized settings are admin-only in configuration managers.

## Localization

Create `Protective Wards.LanguageName.yml` or `Protective Wards.LanguageName.json` inside `BepInEx/config`. Files from the config folder are loaded on game launch and language changes; otherwise the embedded localization is used. The packaged `Protective Wards.English.json` is an example that can be copied to the config folder for use as an external override.

- [Jotunn language list](https://valheim-modding.github.io/Jotunn/data/localization/language-list.html)

## Compatibility

Dedicated patches are used where objects have special ownership or interaction logic. The optional generic-interactable protection is intentionally broad and should be enabled carefully on heavily modded servers.

Optional integrations are detected at runtime without bundling their API assemblies:

- **Guilds**: server-validated current membership can grant permitted-equivalent access to one guild bound per ward.
- **EpicLoot**: the enchanting table follows `Ward access from non-permitted players / Crafting stations`.

## Links

- [GitHub](https://github.com/shudnal/ProtectiveWards)
- [Nexus](https://www.nexusmods.com/valheim/mods/2450)
- [Discord](https://discord.gg/e3UtQB8GFK)
- [Buy Me a Coffee](https://buymeacoffee.com/shudnal)
