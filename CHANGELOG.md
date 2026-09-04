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
- Added transaction-scoped merchant financing, compatibility audit tooling, a vanilla compatibility matrix, and an in-game test plan.
- Added policy and isolation coverage for free, automatic, forced, non-merchant, restocked, and duplicate-callback transactions (36 total tests).
- Added the rare Black Market event using the native merchant room and inventory UI, with seven cards, two relics, three potions, card removal, optional purchases, and a safe exit.
- Added Black Market loan purchases, conservative relic selling, configurable pricing, and a configurable relic-sale denylist.
- Added localized Black Market event text for all 16 supported game languages and expanded automated coverage to 58 tests.

### Changed

- Repayment UI now follows the merchant container's local coordinates and enters with the shop animation.
- Debt Curse automatic-removal text now appears on a real second line.
- Financing now requires a matching player-initiated merchant UI context; automatic callers such as Lord's Parasol and AutoSlay cannot create Debt.
- Purchase shortfall explicitly uses the final vanilla `MerchantEntry.Cost` after price modifiers.
- Black Market occurrence now uses the run RNG and its configured percentage, with at most one appearance per run.
- Black Market cards now cost 25% above their vanilla base price; normal relics and potions retain vanilla pricing.
- The special relic slot now contains only a safety-checked Ancient relic. Its 5–15 max-HP cost appears below the gold price and is charged once after a successful paid purchase.
- Restored the native inventory Back button when the Black Market is hosted by an event room.
- Ancient max-HP prices now use localized red text attached directly below their gold price.
- Repayment no longer captures the global confirm key; keyboard and controller confirmation now requires explicit focus navigation.
- Completed in-game validation of the current debt, loan, repayment, Black Market, save/load, controller, discount, restock, Lord's Parasol, and multiplayer paths.

### Removed

- Removed multiplayer campfire relic trading from the project roadmap. Card trading and player-to-player gold trading are also not planned.
