# Protective Wards
![logo](https://staticdelivery.nexusmods.com/mods/3667/images/2450/2450-1689565569-1699140464.png)

Configurable protection and modifiers in active ward area. Creatures, rain, raids, plants, fall damage, ship, resources drains, smelting speed and active offerings. Protect what you values, your time.

# Description
 - Need to protect your base from various dangers?
 - Want to peacefully sit in front of a fire without being interrupted by raids?
 - Tired of your ships being constantly damaged in docks?
 - Worry about weather damage?
 - Lost all your skills to fall damage while constructing?
 - Don't like your pets being killed by raids?
 - Want to feel more confident while you are within the walls of your fortress?
 - Ever thought of your stations to be more effective?
 - Exhausted of your hammer constant repairing?
 - Concerning about your food and fuel running out too fast?
 - Burnt out on keeping your base repaired?
 - Want to have fun with offering some items?
 - Fed of back and forth traveling to the Start Temple to change your forsaken power?
 - Can't find Haldor or Hildir?
 - Or maybe treasures just burn in your pockets and merchants are far off?

Make Ward your guard. Inside of an active and warm field of protective ward some miracles happen.

## Installation (manual)
extract ProtectiveWards.dll file to your BepInEx\Plugins\ folder

## Features

### Multipliers
* control how much damage will be taken by 
  - players
  - enemies
  - tamed
  - structures (and ships)
  - falling
* speed up your turret fire rate
* control the smelting speed of stations
  - smelting (kiln, furnace, windmill and so on)
  - cooking (that also means faster burn)
  - fermenting
  - sap collecting
* control your expences
  - food drain with time
  - stamina drain on actions
  - skills drain on death
  - fireplace fuel drain (including bathtub, torches and braziers)
  - hammer durability drain (for builders with love)

### Full protecion
* protect boars and hens from enemies and fire (for your damage tamed modifier works)
* protect structures from rain damage
* protect your ship from water damage, or any damage, your choice
* protect your plants from any damage (to harvest barley and flax just switch off the ward)
* protect your fireplaces from stepping on them
* protect yourself from the raids (if you are sitting next to an active fire on the all kind of chair but not the floor)
* protect players from traps

### Passive repair
You can activate the ward to start passive repair process of all pieces in all connected areas.
Ward will repair one piece every 10 seconds until all pieces are healthy. Then the process will stop.

### Passive door auto closing
All doors will be closed after specified time of the last door interaction

### Active offerings
Offer the certain item to ward to have some handy effect.
* surtling core to instantly repair everything
* black core to augment all structures (double the hp, ships included)
* food to start 3 min passive healing in all connected areas. Players and tamed. Better food means better heal. Helpful if you are being raided
* mead to share the effect to all players in connected areas
* thunderstone to call the Thor's wrath upon your enemies
* trophy to instantly kill all enemies of that speccy
* Ymir flesh to instantly grow every healthy plant
* eitr x5 to instantly grow every plant regardless the requirements (empty space or biome)
* dragon egg to activate Moder power on all players in all connected areas
* several items to call a taxi to the different locations

Everything mentioned above works only inside of an active ward range. Yes you can change the range, disable the flash and always see the marker.
And yes the configuration is locked if you're playing on the server.

### Taxi
You can offer:
* boss trophy to travel to Start Temple (initial spawn point) (trophy will NOT be consumed)
* coins to travel to the Haldor. x2000 if you didn't find him yet and x500 otherwise (coins will be consumed)
* any of Hildir's chest to travel to the Hildir (chest will NOT be consumed)
* Fuling totem to travel to Hildir (totem will be consumed)

That will call a Valkyrie to move you to your desired destination.

After landing you will have 2 minutes to do what you wanna do.

Then you will be moved back to initial point.

If you are 
* sleeping
* in dungeon
* sitting
* attached to a ship
* riding
* teleporting
* using your hammer

then the taxi will wait until you stop

You can end the flight early by pressing your binded Alternative + Use buttons (L.Shift + E by default).

You will be granted Slow Fall until you touched the ground.

Restrictions
* you can't be encumbered
* you should be teleportable
* target point should be far than 300 away
* you can't start next travel if the taxi awaits you to return to start point

## Compatibility
* The mod should be compatible with anything I know as its patches designed to be noninvasive. But other mods may break the mod's functionality.

## Mirrors
[Nexus](https://www.nexusmods.com/valheim/mods/2450)

[Thunderstore](https://valheim.thunderstore.io/package/shudnal/ProtectiveWards/)

## Changelog

v 1.1.5
* taxi offerings

v 1.1.4
* option to enable spawn in ward area

v 1.1.3
* bosses excluded from multipliers effects
* dragon egg offering
* hideable offering list

v 1.1.2
* auto closing doors
* instant plant growth offerings

v 1.1.1
* passive effect fix
* options enabled by default

v 1.1.0
* activatable passive repair
* active offering effects
* more multipliers
* more protections

v 1.0.4
* fixed fireplace protection bug

v 1.0.3
* boars and hens also protected from fire and smoke

v 1.0.2
* fixed implementation of raid protection to support both PTR and stable game version

v 1.0.1
* fixed exception in log when interacting with a ward

v 1.0.0
* Initial release