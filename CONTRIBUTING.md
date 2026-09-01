# Contributing

[BUILD.md](BUILD.md) has the setup steps and the build commands. To make a card, copy the
closest existing card and follow the three-way rule below.

## The three-way update rule

> [!IMPORTANT]
> A card is in three files. The three files must agree. If you change one file, change
> all three files. The command `scripts/dev.sh lint` checks the three files offline.

1. **Code**: the card class under the mod's `Cards/` folder. Each number is in a builder
   pair with the format `WithDamage(base, upgradeDelta)`.
2. **Localization**: `localization/eng/cards.json`. Use `{Var:diff()}` tokens. Do not
   write numbers in the text. The tokens let the game show the upgrade preview. Each
   power also needs the keys `.title`, `.description`, and `.smartDescription`.
3. **cards.csv**: the design sheet in plain text. The format is `base (upgraded)`.

## Code style

- **A comment must tell why, not what.** The names and the structure must make the _what_
  clear. Write a comment only where a reader can become confused: a workaround for a
  problem in the base game, a dependency on the order of operations, a reflection hack,
  or a choice that looks incorrect where a "fix" would break the code. Keep each comment
  short. Do not write TODO comments. Do not write notes about removed code.
- Put each Harmony patch in a `Patches/` folder. Use one file for each concern. Start
  each file with a `//` header. The header must explain the engine constraint.
  `MainFile.Initialize` keeps a patch failure inside one class. Thus you must never
  assume that another patch ran.
- Keep the mod assets under the mod's own `res://` root. Never put them at base game
  paths. [BUILD.md](BUILD.md) gives the reason.
- Put state that must survive a save and a reload in `DynamicVars`. Do not use plain
  fields.
- Description code and tooltip code must not read `Owner` without a guard. A canonical
  model (in the compendium) throws an exception. Use `IsMutable` as the guard.

## Testing

`dotnet build` must pass with 0 errors. The localization analyzer runs as part of the
build. After a change to the gameplay, test the card in the game. Test the base card and
the upgraded card. For a power, also test more than one stack.

## Changelog & releases

Each change that a player can see needs an entry in [CHANGELOG.md](CHANGELOG.md) under
`## [Unreleased]`. This includes content, balance, a bug fix, text, and art. Write the
entry for players. [RELEASING.md](RELEASING.md) has the version policy and the release
procedure.
