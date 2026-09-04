using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models;
using SpireEconomy.SpireEconomyCode.Configuration;

namespace SpireEconomy.SpireEconomyCode.BlackMarket;

internal static class BlackMarketMerchantInventoryFactory
{
    internal const int CharacterRareCount = 2;
    internal const int ColorlessRareCount = 2;
    internal const int OffCharacterCardCount = 3;
    internal const int RelicCount = 2;
    internal const int CardRemovalBaseCost = 200;

    private static readonly CardRarity[] OffCharacterCardRarities =
        [CardRarity.Common, CardRarity.Uncommon, CardRarity.Rare];

    private static readonly FieldInfo CharacterCardsField = AccessTools.Field(typeof(MerchantInventory), "_characterCardEntries");
    private static readonly FieldInfo ColorlessCardsField = AccessTools.Field(typeof(MerchantInventory), "_colorlessCardEntries");
    private static readonly FieldInfo CardRemovalField = AccessTools.Field(typeof(MerchantInventory), "<CardRemovalEntry>k__BackingField");
    private static readonly MethodInfo UpdateEntriesMethod = AccessTools.Method(typeof(MerchantInventory), "UpdateEntries");
    private static readonly MethodInfo PopulatePotionEntriesMethod =
        AccessTools.Method(typeof(MerchantInventory), "PopulatePotionEntries");
    private static readonly HashSet<string> UnsafeSpecialRelicTypes =
    [
        "ChoicesParadox", "CursedPearl", "Storybook", "ScrollBoxes",
        "WongoCustomerAppreciationBadge", "WongosMysteryTicket"
    ];

    internal static MerchantInventory Create(Player player)
    {
        MerchantInventory inventory = new(player);
        List<CardModel> characterRarePool = player.Character.CardPool
            .GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint)
            .Where(card => card.Rarity == CardRarity.Rare)
            .DistinctBy(card => card.Id)
            .ToList();
        List<CardModel> colorlessRarePool = ModelDb.AllSharedCardPools
            .Where(pool => pool.IsColorless)
            .SelectMany(pool => pool.GetUnlockedCards(
                player.UnlockState,
                player.RunState.CardMultiplayerConstraint))
            .Where(card => card.Rarity == CardRarity.Rare)
            .DistinctBy(card => card.Id)
            .ToList();
        List<CardModel> offCharacterPool = ModelDb.AllCharacters
            .Where(character => character.IsPlayable && character.Id != player.Character.Id)
            .SelectMany(character => character.CardPool.GetUnlockedCards(
                player.UnlockState,
                player.RunState.CardMultiplayerConstraint))
            .DistinctBy(card => card.Id)
            .ToList();

        List<MerchantCardEntry> characterEntries = [];
        List<MerchantCardEntry> colorlessEntries = [];
        CharacterCardsField.SetValue(inventory, characterEntries);
        ColorlessCardsField.SetValue(inventory, colorlessEntries);
        for (int i = 0; i < CharacterRareCount; i++)
            PopulateCard(characterEntries, player, inventory, characterRarePool, CardRarity.Rare);
        foreach (CardRarity rarity in OffCharacterCardRarities)
            PopulateCard(characterEntries, player, inventory, offCharacterPool, rarity);
        for (int i = 0; i < ColorlessRareCount; i++)
            PopulateCard(colorlessEntries, player, inventory, colorlessRarePool, CardRarity.Rare);
        foreach (RelicModel relic in SelectRelics(player, RelicCount))
        {
            MerchantRelicEntry entry = new(relic.ToMutable(), player);
            if (relic.Rarity == RelicRarity.Ancient)
                BlackMarketAncientRelicCost.Register(entry, player.RunState.Rng.Shuffle.NextInt(5, 16));
            inventory.AddRelicEntry(entry);
        }
        PopulatePotionEntriesMethod.Invoke(inventory, null);
        CardRemovalField.SetValue(inventory, new MerchantCardRemovalEntry(player));

        foreach (MerchantEntry entry in inventory.AllEntries)
        {
            // Cards cost 25% more. Relics and potions keep their normal merchant price;
            // Ancient relics use their own explicit gold and max-HP costs.
            BlackMarketMerchantRegistry.Register(entry, useBlackMarketMarkup: entry is MerchantCardEntry);
            entry.PurchaseCompleted += (status, purchasedEntry) =>
                UpdateEntriesMethod.Invoke(inventory, [status, purchasedEntry]);
        }
        BlackMarketMerchantRegistry.Register(inventory);
        return inventory;
    }

    private static void PopulateCard(List<MerchantCardEntry> entries, Player player, MerchantInventory inventory,
        IReadOnlyCollection<CardModel> pool, CardRarity rarity)
    {
        List<CardModel> rarityPool = pool.Where(card => card.Rarity == rarity).ToList();
        if (rarityPool.Count == 0)
            return;

        MerchantCardEntry entry = new(player, inventory, rarityPool, rarity);
        // MerchantCardEntry's constructor only records its selection rules. Vanilla calls
        // Populate() before placing the entry in an inventory; that creates CreationResult,
        // which NMerchantCard requires in order to build and display the actual card node.
        entry.Populate();
        if (entry.IsStocked)
            entries.Add(entry);
    }

    private static IEnumerable<RelicModel> SelectRelics(Player player, int count)
    {
        // This slot promises an Ancient relic and carries a max-HP price. Do not mix Event
        // relics into it: those otherwise look like Ancient stock but correctly have no HP cost.
        List<RelicModel> special = ModelDb.AllRelics
            .Where(relic => relic.Rarity == RelicRarity.Ancient)
            .Where(relic => IsSafeSpecialRelic(player, relic)).ToList();
        player.RunState.Rng.Shuffle.Shuffle(special);
        List<RelicModel> result = special.Take(1).ToList();

        player.PopulateRelicGrabBagIfNecessary(player.RunState.Rng.Shuffle);
        while (result.Count < count)
        {
            RelicModel? relic;
            try
            {
                relic = player.RelicGrabBag.PullFromFront(RelicRarity.Rare,
                    candidate => candidate.Rarity == RelicRarity.Rare && candidate.IsAllowed(player.RunState) &&
                                 player.GetRelicById(candidate.Id) is null && result.All(x => x.Id != candidate.Id),
                    player.RunState);
            }
            catch (InvalidOperationException) { break; }
            if (relic is null) break;
            result.Add(relic);
        }
        return result;
    }

    private static bool IsSafeSpecialRelic(Player player, RelicModel relic)
    {
        string typeName = relic.GetType().Name;
        if (!relic.IsAllowed(player.RunState) || player.GetRelicById(relic.Id) is not null ||
            relic.HasUponPickupEffect || relic.SpawnsPets || relic.AddsPet || relic.IsStackable ||
            typeName.StartsWith("Fake", StringComparison.Ordinal) ||
            typeName.StartsWith("Paels", StringComparison.Ordinal) ||
            typeName.StartsWith("Neow", StringComparison.Ordinal) ||
            UnsafeSpecialRelicTypes.Contains(typeName))
            return false;
        HashSet<string> denied = EconomyConfig.BlackMarketSpecialRelicDenylist
            .Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return !denied.Contains(relic.Id.Entry) && !denied.Contains(relic.Id.ToString());
    }
}
