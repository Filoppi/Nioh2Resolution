# Nioh2Resolution

Nioh2Resolution Adds support for any resolutions to *Nioh 2: Complete Edition* on PC (mainly useful for 21:9+ or 16:9-).

Credit to LennardF1989 for the original Nioh 1 version.

Donations: https://www.paypal.com/donate?hosted_button_id=BFT6XUJPRL6YC

## How to use?

1) Download the latest release of Nioh2Resolution.
2) Save the content of the archive to the root of the *Nioh 2: Complete Edition* game-directory (Defaults to `C:\Program Files\Steam\steamapps\common\Nioh2` for Steam installs) (you can delete it later on)
3) Run `Nioh2Resolution.exe` and follow the instructions.
4) Start the game and set the resolution to 3440x1440 (if that is not shown in the game menu, set it in %USERPROFILE%\Documents\KoeiTecmo\NIOH2\config.xml)
5) Restart the game as there seems to be a bug that breaks the brightness (and UI scaling) if you change resolution after startup.

## How does it work?

Before doing anything, the game will be unpacked using [Steamless](https://github.com/atom0s/Steamless). This is required because the Steam DRM will otherwise not allow a modified executable.

Once unpacked, the patcher will look for the byte representation of the 3440x1440 resolution, and change all occurances to your desired resolution.

Note that differently from the original patch for the first game, UI scaling isn't perfect yet.
Anything wider than 21:9 should work decently, especially because the FOV scaling is Horizontal+, but some UI anchoring will be wrong and some other won't be scaled correctly. Loading screens and cutscenes seem fine.
EXPRERIMENTAL: When less wide than 16:9 (e.g. 16:10, 4:3) this patch scales the UI as it remains anchored around 16:9 and some elements would not be visible. Scaling it should not cause any stretching. Note that some menus might look worse, but in game UI will be better.

In theory, this patcher should work for any version of the game.
