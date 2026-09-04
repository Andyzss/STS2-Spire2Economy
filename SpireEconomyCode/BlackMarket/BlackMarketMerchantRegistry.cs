using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Relics;
using SpireEconomy.SpireEconomyCode.Configuration;

namespace SpireEconomy.SpireEconomyCode.BlackMarket;

internal static class BlackMarketMerchantRegistry
{
    private static readonly ConditionalWeakTable<MerchantInventory, object> Inventories = new();
    private static readonly ConditionalWeakTable<MerchantEntry, object> Entries = new();
    private static readonly ConditionalWeakTable<MerchantEntry, object> StandardPriceEntries = new();
    private static readonly object Marker = new();
    private static readonly AccessTools.FieldRef<MerchantEntry, int> RawCost =
        AccessTools.FieldRefAccess<MerchantEntry, int>("_cost");

    internal static void Register(MerchantInventory inventory) => Inventories.AddOrUpdate(inventory, Marker);
    internal static void Register(MerchantEntry entry, bool useBlackMarketMarkup = true)
    {
        Entries.AddOrUpdate(entry, Marker);
        if (!useBlackMarketMarkup)
            StandardPriceEntries.AddOrUpdate(entry, Marker);
        ApplyBlackMarketBasePrice(entry);
    }
    internal static bool Contains(MerchantInventory inventory) => Inventories.TryGetValue(inventory, out _);
    internal static bool Contains(MerchantEntry entry) => Entries.TryGetValue(entry, out _);

    internal static void ApplyBlackMarketBasePrice(MerchantEntry entry)
    {
        if (!Contains(entry)) return;
        if (entry is MerchantCardRemovalEntry)
        {
            RawCost(entry) = BlackMarketMerchantInventoryFactory.CardRemovalBaseCost;
            return;
        }

        bool isSpecialRelic = entry is MerchantRelicEntry { Model: { } model } &&
            model.Rarity is RelicRarity.Ancient or RelicRarity.Event;
        if (isSpecialRelic)
        {
            RawCost(entry) = BlackMarketPricing.SpecialRelicBaseCost;
            return;
        }

        if (StandardPriceEntries.TryGetValue(entry, out _))
            return;

        int basePrice = BlackMarketPricing.GetPurchaseBasePrice(RawCost(entry), isSpecialRelic);
        int pricePercent = entry is MerchantCardEntry
            ? BlackMarketPricing.CardPricePercent
            : EconomyConfig.BlackMarketPricePercent;
        RawCost(entry) = BlackMarketPricing.ApplyPurchaseMarkup(basePrice, pricePercent);
    }
}
