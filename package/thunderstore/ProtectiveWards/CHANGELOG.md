# 2.0.3
* wards offerings will still be put in the compendium if player knows about ward but default texts are disabled

# 2.0.2
* major taxi system rework
* added a `Valkyrie passage` status effect with the Celestial feather icon for active Valkyrie flights and return timers
* added separate Valkyrie passage destination enable configs and consume-item configs for Sacrificial Stones, Haldor, Hildir, Bog Witch, and optional boss altar routes
* added disabled-by-default Valkyrie passage routes to vanilla boss altars using boss offering items
* changed the default configured Hildir passage tribute to Linen thread x50
* Valkyrie passage item configs can now use item prefab names, localization tokens, or localized item names
* Valkyrie return flight can now be disabled by setting Seconds to fly back to 0
* moved the detailed offerings list from the ward hover into the Valheim Compendium and replaced the hover list with an immediate raven-icon hint
* expanded the ward Compendium text with Protective Wards behavior notes and added a dedicated Ward and offerings topic
* fixed connected ward expiration refresh not counting players standing inside an accessible connected ward area
* fixed plant growth offerings failing on some plant instances
* ward permit and unpermit commands no longer match players from an empty name query
* fixed consumable offering items being eaten when used on a ward without matching an enabled offering

# 2.0.1
* tightened full portal blocking by validating the source portal on the server before approving destination teleport access
* fixed permitted non-creator players being unable to open or apply ward settings on disabled wards when creator-only editing is disabled
* fixed per-ward circle colors saved from the settings UI not applying because stored HTML colors were read without a leading #
* improved cart last-controller tracking so player-attached carts use the attached player first and fall back to the nearest player only for non-player cart pullers
* reset ward administration and ward settings RPC registration when changing worlds or switching between client and server roles in the same game session

# 2.0.0
* migrated configuration sync to Jotunn and added Jotunn as a required dependency
* removed the global Enabled config; use individual feature configs instead
* added per-ward settings UI for range, emission color, sphere visuals, and ward circle visuals
* ward range customization is now bound to the vanilla guard_stone ward prefab
* added access protection from non-permitted players for containers, doors, plants, feasts and placed consumable food, configurable non-food item pickup modes, ships, tames, saddles, carts, portals, production stations, item stands, armor stands, map tables, shield generators, incinerators, turrets, crafting stations, beds, catapults, archery targets, barber stations, traps, fireplaces, and generic interactables
* added connected ward access modes: Off, SameCreatorOnly, MutualTrust, and AnyConnected
* enabling or reactivating a ward can also activate connected wards the requester has access to
* added ownership-aware exemptions for portals, tombstones, saddles, and similar owned objects, and last-controller exemptions for ships and carts so the same driver can leave another warded area
* added portal access modes, including teleport-only access and full teleport blocking
* added server-side pw_ and ward_ commands for nearby ward administration: permit, unpermit, enable, disable, set expired, and set unexpired
* added optional per-player ward build limit
* added background protection for qualified warded bases when no permitted/effective-access player is nearby
* added background protection options for player-built structures, tames, ships, carts, fire damage, tame pacify, building, and demolishing
* added multiplayer/server-side inactive ward expiration with manual and automatic reactivation modes, refreshed by nearby access players
* added Ward admin access mode, including an option that requires admin god mode
* moved Permit everyone to the Ward admin group; when enabled, ward access checks are bypassed and every player is treated as having ward admin access

# 1.2.11
* fixed localization not initializing in certain cases

# 1.2.10
* config to ignore certain doors when auto closing

# 1.2.9
* patch 0.221.10
* more translations

# 1.2.8
* fixed Hildir's chest preventing player from fly to Hildir

# 1.2.7
* new config for passive repair to be able to repair structures not built by players (disabled by default)
* new config for passive repair to check if corresponding crafting station is near the ward (disabled by default)
* fixed ward not repairing and doing offerings in area

# 1.2.6
* patch 0.220.3
* ServerSync updated

# 1.2.5
* fixed valkyrie sometimes didn't bring you back in multiplayer when used within seconds with other player
* time before valkyrie bring you back from a trader made configurable
* areas protected by wards will be considered connected if they overlap at least a bit instead of one ward being physically in the radius of the other
* Bukeperries will no longer be used on player while trying to offer

# 1.2.4
* fixed shared mead offering not applying to other players
* several valkyries now can be summoned as taxi at once

# 1.2.3
* bog witch

# 1.2.2
* taxi error fixed

# 1.2.1
* hover text spamming error fixed

# 1.2.0
* apply ward settings by pressing LeftShift + E on disabled ward (if you are ward creator). Toggling ward doesn't change its settings.
* change ward emission color (ward specific)
* change various area marker values (ward specific)
* code refinements and refactoring (more room for errors btw)
* localization support for custom captions
* more detailed description of item offerings will appear after certain amount of time spent looking at ward

# 1.1.19
* added fader trophy to trophy list to travel to sacrificial stones
* ward range can be set to distinct ward and persist
* fixed water surface rendering inside ward bubble effect
* more bubble effect variables exposed to configure visuals further

# 1.1.18
* fixed taxi offering for multiplayer

# 1.1.17
* added offering item hotkey in hover menu

# 1.1.16
* PTB 0.218.17 compatibility
* consumable item and coins amount to travel to Traders made configurable
* hover text will not be visible if player has no ward access
* offerings and repair effects will not be available if player has no ward access
* ServerSync updated to 1.17

# 1.1.14
* Ashlands

# 1.1.13
* patch 0.217.46

# 1.1.12
* option to show the bubble like trader's one
* option to enable demister in ward range

# 1.1.10
* patch 0.217.22, server sync fix

# 1.1.9
* patch 0.217.22

# 1.1.8
* option to grant permittance to everyone

# 1.1.7
* taxi flight won't start until you are encumbered or not teleportable
* egg offering restricted to Moder kill

# 1.1.6
* teleportable chest check fix

# 1.1.5
* taxi offerings

# 1.1.4
* option to enable spawn in ward area

# 1.1.3
* bosses excluded from multipliers effects
* dragon egg offering
* hideable offering list

# 1.1.2
* auto closing doors
* instant plant growth offerings

# 1.1.1
* passive effect fix
* options enabled by default

# 1.1.0
* activatable passive repair
* active offering effects
* more multipliers
* more protections

# 1.0.4
* fixed fireplace protection bug

# 1.0.3
* boars and hens also protected from fire and smoke

# 1.0.2
* fixed implementation of raid protection to support both PTR and stable game version

# 1.0.1
* fixed exception in log when interacting with a ward

# 1.0.0
* Initial release
