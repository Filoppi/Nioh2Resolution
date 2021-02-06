# Nioh2Resolution

Nioh2Resolution adds support for 21:9+ resolutions (or any) to *Nioh 2: Complete Edition* on PC.

Credit to  LennardF1989 for to original Nioh 1 version.

## How to use?

1) Download the latest release of Nioh2Resolution.
2) Save `Nioh2Resolution.exe` to the root of the *Nioh 2: Complete Edition* game-directory (Defaults to `C:\Program Files\Steam\steamapps\common\Nioh2` for Steam installs)
3) Run `Nioh2Resolution.exe` and follow the instructions.
4) Start the game and set the resolution to 3440x1440 (if that is not shown in the game menu, set it in %USERPROFILE%\Documents\KoeiTecmo\NIOH2\config.xml)
5) Restart the game as there seems to be a bug that breaks the brightness (and UI scaling) if you change resolution after startup.

## How does it work?

Before doing anything, the game will be unpacked using [Steamless](https://github.com/atom0s/Steamless). This is required because the Steam DRM will otherwise not allow a modified executable.

Once unpacked, the patcher will look for the byte representation of the 3440x1440 resolution, and change all occurances to your desired resolution.
Note that differently from the original patch for the first game, this doesn't patch the aspect ratio of the UI yet, so it won't work great at random resolution that are less than 16:9 for example, but anything above 16:9 should work decently, especially because the FOV scaling is Horizontal+.
When above 21:9 or below 16:9, some of the UI remains anchored as if you had one of these 2 aspect ratios (the closest one), some other anchors to your custom aspect ratio correctly. Some other UI scales its size incorrectly, but none of them break the game or become invisible. Loading screens and cutscenes seem fine.

In theory, this patcher should work for any version of the game.
