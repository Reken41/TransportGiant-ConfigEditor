# TransportGiant Config Editor + Map Generator FIX

Transport Giant Config Editor allows you to edit main game config file. 
For fans! You can really change that game with this extra options! Enjoy!
Current version is v0.6.0.0

There is AI generated code inside.

## Features
* Loads only TGConfig.gen file
* Works with TG Steam edition
* Extracts game graphics
* Edit game Products params
* Edit game Factories params (acceppted, produced products)
* Edit game Factory lines
* Edit Storage Place params
* Edit global constants
* Edit game map creator initial values
* Edit game Vechicles params
* Edit all stations terminals sizes
* More sections will be added on demand

## Map generator FIX

**Copy FE_RandomMap.uif to Steam\steamapps\common\Transport Giant\uif\uif.**

**Overwrite the original file. Done! The game will generate all map sizes.**

You can download the file from release of from source code. Archive name: FE_RandomMap.zip

### HISTORY OF THIS BUG

The Steam version contains an incorrectly configured `uif/uif/FE_RandomMap.uif` file. Although the menu displays the correct map-size options—Small, Normal, Large, and Huge—the buttons were connected to the wrong callback functions.

The original Steam configuration used:

- Small → `Cheat`
- Normal → `Easy`
- Large → `Medium`
- Huge → `Hard`

`Cheat`, `Easy`, and `Hard` are difficulty-related callbacks and do not belong to the random map size selection screen. Additionally, selecting Large called the `Medium` function, causing the game to generate a Normal-sized map. Selecting Huge also left the generator at the default Normal size.

The file was corrected to use the map-size callbacks already supported by the Steam executable:

- Small → `Small`
- Normal → `Medium`
- Large → `Large`
- Huge → `XLarge`

Only these four callback assignments were changed. The interface layout, graphics, game configuration, and executable were not modified.

After this correction, the Steam version can generate all four available map sizes: Small, Normal, Large, and Huge.

### Water Amount Warning !
### Do not move the Water Amount slider to either of its extreme positions.

Setting the minimum or maximum water amount can cause the map-generation progress to exceed 100% and crash the game. This is an original bug in the Transport Giant map generator and is not caused by the map-size fix.

Use a value between the two endpoints. Values such as 90% have been confirmed to work correctly.

## Editor Screens
![TGConfEditor1](https://github.com/Reken41/TransportGiant-ConfigEditor/raw/master/screens/screen01.jpg)

![TGConfEditor1](https://github.com/Reken41/TransportGiant-ConfigEditor/raw/master/screens/screen02.jpg)
