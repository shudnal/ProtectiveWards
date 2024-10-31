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
 - Fed of back and forth traveling to the Sacrificial Stones to change your forsaken power?
 - Can't find Haldor, Hildir or Bog Witch?
 - Or maybe treasures just burn in your pockets and merchants are far off?
 
Make Ward your guard. Inside of an active and warm field of protective ward some miracles happen.

## Features

Everything mentioned below works only inside of an active ward range.

And yes the configuration is locked if you play on a server.

### Customization 

Customization works for distinct wards. To change settings of a ward you should be its creator, disable the ward and then press LeftShift + E to apply current mod settings to that ward. Toggling a ward doesn't change its settings.

You can customize:
* range (markers, bubble and demister range is changes accordingly)
* emission color (that yellow light on default ward model and also flare and light)
* circle area marker style (colors, size, amount, speed)
* ward bubble (color and other shader properties, experiment with it to get best effects)

You can also disable the flash and always see the area marker (shared for all wards).

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

Detailed information about what item causes what effect appears on ward hover after certain amount of time to not spam regular vision.

### Taxi
You can offer:
* boss trophy to travel to Sacrificial Stones (initial spawn point) (trophy will NOT be consumed)
* coins to travel to the Haldor. x2000 if you didn't find him yet and x500 otherwise (coins will be consumed)
* any of Hildir's chest to travel to the Hildir (chest will NOT be consumed)
* Fuling totem to travel to Hildir (totem will be consumed)
* Pukeberries to travel to Bog Witch (berries will be consumed)

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

## Localization

Some messages and captions uses well fit vanilla lines. The rest is localized.

To add your own localization create a file with the name ProtectiveWards.LanguageName.yml or ProtectiveWards.LanguageName.json anywhere inside of the Bepinex folder. For example, to add a French translation you could create a ProtectiveWards.French.yml file inside of the config folder and add French translations there.

Localization file will be loaded on the next game launch or on the next language change.

You can send me a file with your localization at [GitHub](https://github.com/shudnal/ProtectiveWards/issues) or [Nexus](https://www.nexusmods.com/valheim/mods/2450?tab=posts) so I can add it to mod's bundle.

[Language list](https://valheim-modding.github.io/Jotunn/data/localization/language-list.html).

English localization example is located in `English.json` file next to plugin dll.

## Installation (manual)
extract ProtectiveWards.dll file to your BepInEx\Plugins\ folder

## Compatibility
* The mod should be compatible with anything I know as its patches designed to be noninvasive. But other mods may break the mod's functionality.

## Configurating
The best way to handle configs is [Configuration Manager](https://thunderstore.io/c/valheim/p/shudnal/ConfigurationManager/).

Or [Official BepInEx Configuration Manager](https://valheim.thunderstore.io/package/Azumatt/Official_BepInEx_ConfigurationManager/).

## Mirrors
[Nexus](https://www.nexusmods.com/valheim/mods/2450)