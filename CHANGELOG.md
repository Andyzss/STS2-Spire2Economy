# Changelog

All notable changes to this mod are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this
project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html) as read for
a game mod (see [RELEASING.md](RELEASING.md)). Each version section doubles as the Steam
Workshop update note for that version, so write every entry for players.

## [Unreleased]

### Added

- Added the personal debt system, unique protected Debt Curse, and configurable `MaxDebt`.
- Added financed card and relic purchases at standard merchants.
- Added financed potion purchases and financed card-removal services.
- Added merchant debt repayment with an amount slider and vanilla-styled controls.
- Added humorous localized merchant speech-bubble responses when there is no debt or no gold to repay.
- Added the top-bar debt display, hover descriptions, and localization for all 16 game languages.
- Added save/load reconciliation and 19 automated debt, loan, and localization tests.

### Changed

- Repayment UI now follows the merchant container's local coordinates and enters with the shop animation.
- Debt Curse automatic-removal text now appears on a real second line.

### Removed

- Removed multiplayer campfire relic trading from the project roadmap. Card trading and player-to-player gold trading are also not planned.
