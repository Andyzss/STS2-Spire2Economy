using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Models.Relics;

namespace SpireEconomy.SpireEconomyCode.BlackMarket;

internal static class BlackMarketNativePresentation
{
    internal static async Task ApplyLordsParasol(BlackMarketEvent market)
    {
        if (market.Owner is not { } owner || owner.GetRelic<LordsParasol>() is null || market.MerchantInventory is null)
            return;

        MerchantInventory inventory = market.MerchantInventory;
        foreach (MerchantCardEntry entry in inventory.CharacterCardEntries.Concat(inventory.ColorlessCardEntries).ToList())
            if (entry.IsStocked) await entry.OnTryPurchaseWrapper(inventory, true);
        foreach (MerchantRelicEntry entry in inventory.RelicEntries.ToList())
            if (entry.IsStocked) await entry.OnTryPurchaseWrapper(inventory, true);
        if (inventory.CardRemovalEntry is { IsStocked: true } removal)
            await removal.OnTryPurchaseWrapper(inventory, true, false);
    }
}
