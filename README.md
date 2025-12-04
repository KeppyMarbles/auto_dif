# auto_dif
Solution to hot reloading of Interiors in Marble Blast. Uses local TCP to connect with Blender or Constructor.

## Setup


### Blender
Supported versions: 4.x

Ensure the latest [io_dif](https://github.com/RandomityGuy/io_dif) is installed, then install this repo's add-on (the `auto_dif` folder). Then navigate to `Scene Properties` > `AutoDIF Settings` > `Export Directory` and choose your desired interiors folder.

In PQ, hit the `Connect Blender` button in the editor.

### Constructor

Navigate to your Constructor installation, and place [csx3dif.exe](https://github.com/RandomityGuy/csx3dif/releases) in the root directory. Then make sure you have [my Constructor plugins](https://github.com/KeppyMarbles/kepstructor). You technically only need `autodif.cs` but I recommend installing them all.

Open Constructor and save or open a scene. Then click the `Auto DIF` plugin box (found in User tab), and set your desired interiors folder. 

In PQ, hit the `Connect Constructor` button in the editor.

## Features

### Blender
- File > Export > To Marble Blast: Operator that exports the scene as DIF and automatically places it into the currently loaded mission
  - Subobjects (such as those from [Marble Blast Toolkit](https://github.com/FlavoredSaucer/marbleBlastToolkit)) will be placed automatically
  - DIF splits (large, multi-file interiors) are handled properly
- Export Directory (Scene Property): determines where in the game directory the interior(s) should be placed
- Export on Save (Add-on Preference): if enabled, export will occur when saving the scene
- BSP Algorithm (Add-on Preference): The setting to pass on to io_dif for calculating BSP Tree, needed for raycasting in the game like in Drop To Ground. Disabled by default for faster exports and smaller file size  
- Game Directory Override (Add-on Preference): can be set if there is an issue with the in-game exe path value

https://github.com/user-attachments/assets/834daa00-930c-4c40-a6a1-4d914f5f6acd

### Constructor
 - Mostly the same as Blender.
 - Adds rotation field to PointEntities and checks if each Door_Elevator has enough path_nodes before exporting
 - Interior folder options can be added or changed in `constructor/prefs.cs`

## Important Info

- This plugin is intended for use with 1 instance of Constructor/Blender and 1 instance of Marble Blast. Further instances will not connect.
- Interior files will use the same name as the project name (or "Untitled" if not saved in Blender). Keep project names unique if you want to prevent other interiors from being overwritten.

### Dev Info

- This should be compatible with other mods besides PQ, but you must ensure that your mod uses the extended engine (containing FileExtension) so interiors can be refreshed properly.
  - There is also a PQ engine fix which changes the order of operations in applying subobject properties so that the rotation field works.

## Credits

Thanks to RandomityGuy for his DIF exporters making this possible