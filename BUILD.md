# How to build

## Prerequisites

- **.NET 9 SDK**
- **Slay the Spire 2**, installed with Steam. The build finds the install path
  automatically (see `Sts2PathDiscovery.props`).
- **Godot 4.5.1 (.NET/mono build)**. You need Godot only for `dotnet publish`, which
  exports the assets and the pck. MegaDot, the Godot fork from MegaCrit, also works. Get
  a standard build from
  [godotengine.org/download/archive](https://godotengine.org/download/archive/). Select
  the ".NET" build.

  > [!IMPORTANT]
  > The **4.5.1 version match** is necessary. The game does not load a `.pck` file that a
  > newer Godot exported. There is no error message. The assets of the mod do not appear.

  The path to Godot is in **two places**. If your path is not the default path, change
  both:

  - `Directory.Build.props` `<GodotPath>`: `dotnet publish` uses it for the pck export.
  - the `GODOT` env var: `scripts/dev.sh import` uses it for the image import step.

## Optional: the modding MCP server

[sts2-modding-mcp](https://github.com/sethmcleod/sts2-modding-mcp) indexes the decompiled
game and drives in-game playtests. To use it here, copy `.mcp.json.example` to `.mcp.json`
and correct the two paths. Git ignores `.mcp.json`, because those paths are yours alone.

## Commands

- `dotnet build` compiles the mod dll. It copies the dll and the manifest json into the
  game's `mods/<Mod>/` folder. This is enough for a change to the **code only**.
- `dotnet publish -c Debug` also exports the pck, which contains the images, the scenes,
  and the **localization JSON**. Use this command when you change any file in the asset
  folder.
- Godot must import each new image and each changed image before you publish:
  `"<GodotPath>" --headless --import --path .`
- Or use `scripts/dev.sh publish`, which does all of the steps above in the correct
  sequence.

> [!CAUTION]
> Publish first, then start or restart the game. Godot keeps the pck open. If you replace
> the pck while the game runs, every later asset load from it fails, and combat shows no
> background. Nothing is corrupted; restart the game.

## CI

`.github/workflows/lint.yml` runs on each push and each PR. It does the three-way rule
check. It also checks the localization JSON. It does no more than this.

A compile needs `sts2.dll` from a Steam install. A public runner cannot have this file.
Thus the build fails without the game (`Sts2PathDiscovery.props`). The compile runs on
the local machine instead.

## Conventions

- The class name gives the localization keys and the icon file names for each card,
  power, relic, and potion. For example, `MyCard` in mod `MyMod` gives `MYMOD-MY_CARD`
  and `my_card.png`.
- The icon sizes are:
  - card portrait: 1000×760
  - power: 64 and 256 (big)
  - relic: 94, 94 (outline), and 256 (big)
  - potion: 256 and 256 (outline)
- Each icon folder holds a placeholder (`card.png`, `power.png`, `relic.png`,
  `relic_outline.png`, `potion.png`). The `*ImagePath` helpers in
  `Extensions/StringExtensions.cs` return it when the real file is absent, so a typo shows
  one wrong icon instead of throwing on the whole screen. The lint reports the miss.
- Each power needs the localization keys `.title`, `.description`, **and**
  `.smartDescription`. The localization analyzer in the build checks this.
- The file `cards.csv` is the primary record of the design. Update it when you change the
  stats or the text of a card.
- The export packs `all_resources`, which means only the file types that Godot imports.
  A file type that Godot does not recognize needs an entry in `include_filter` in
  `export_presets.cfg`, or it is missing from the pck with no error. The Spine pair
  `*.atlas,*.skel` is already there for this reason.
- Godot also imports the manifest json and `cards.csv`, which are repo files that the
  game never reads from the pck. The `exclude_filter` keeps them out. Add a repo file to
  that list whenever it does not belong in a player's download.

> [!CAUTION]
> Never put the mod assets at base game paths (`res://images/...`). Keep all of them
> under the mod's own root (`res://<Mod>/`). A collision silently replaces the base-game
> asset for the whole session.

## Audio

Put the sound files under the mod's own root, in `<Mod>/audio/`. The pck carries them
like any other asset.

- **To play your own sound, give a `res://` path where the game expects an FMOD event
  path.** BaseLib patches `NAudioManager.PlayOneShot` and `NAudioManager.PlayMusic`
  (`Patches/Audio/PlayResourcePatch.cs`): a path that starts with `res://`, `user://`, or
  `uid://` and that exists plays through a Godot audio player instead of FMOD. The volume
  sliders still apply. So a `CharacterSelectSfx` override can return
  `res://<Mod>/audio/select.wav` and nothing else has to change.
- **To reuse a base game sound, pass its `event:/` path to `SfxCmd.Play`.** The event
  paths are string literals in the decompiled source, for example
  `"event:/music/act1_a1_v1"`.
- **For direct playback, use `BaseLib.Audio.ModAudio`** (`PlaySound`, `PlaySoundInRun`,
  `PlaySoundGlobal`). Do not use `BaseLib.Utils.FmodAudio`: it carries an `[Obsolete]`
  attribute that says it may not keep its current form.
- **Match the loudness of the base game.** Its short sfx sit near -16 dB RMS. Peak
  normalization alone does not get sparse material there; a little soft saturation does.
