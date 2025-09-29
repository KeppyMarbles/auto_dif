# auto_dif
A Blender plugin to enable hot reloading of DIFs. Hosts a local server to communicate with a Marble Blast game. Requires [io_dif](https://github.com/RandomityGuy/io_dif).

## Setup

Ensure the latest io_dif is installed. Then install this plugin (the auto_dif folder) and navigate to Preferences > Add-ons > Auto DIF to set the Game Directory. This should point to where marbleblast.exe is located. Then in your project, navigate to Scene Properties > AutoDIF Settings > Export Directory and choose your desired interiors folder.

In PQ, hit the Connect Blender button in the editor.

## Features

- File > Export > To Marble Blast: Operator that exports the scene as DIF and automatically places it into the currently loaded mission
  - Subobjects (such as those from [Marble Blast Toolkit](https://github.com/FlavoredSaucer/marbleBlastToolkit)) will be placed automatically
  - DIF splits (large, multi-file interiors) are handled properly
  - Export Directory (Scene Property): determines where in the game directory the interior(s) should be placed
- Export on Save (AutoDIF pref): if enabled, export will occur when saving the scene


https://github.com/user-attachments/assets/834daa00-930c-4c40-a6a1-4d914f5f6acd


## Important Info

- Interior files will use the same name as the Blender project name (or "Untitled" if not saved). Keep project names unique if you want to prevent other interiors from being overwritten.
- The game will not recieve commands from Blender if it is paused.
- If Blender is connected, don't use the same interior file multiple times in the same mission since that isn't handled currently.
- This should be compatible with other mods besides PQ, but you must ensure that the deleteFile function exists in your mod so that interiors can be refreshed properly.
