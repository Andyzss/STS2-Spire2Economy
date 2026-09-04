using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models;
using SpireEconomy.SpireEconomyCode.Configuration;

namespace SpireEconomy.SpireEconomyCode.BlackMarket;

public static class RelicSaleRules
{
    private static readonly HashSet<string> BuiltInDenylist = new(StringComparer.OrdinalIgnoreCase);

    public static bool CanSell(Player player, RelicModel relic)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(relic);

        if (!ReferenceEquals(relic.Owner, player) || relic.HasBeenRemovedFromState)
            return false;
        if (!ReferenceEquals(relic.GetType().Assembly, typeof(RelicModel).Assembly))
            return false;
        if (relic.Rarity is RelicRarity.Starter or RelicRarity.Event or RelicRarity.Ancient or RelicRarity.None)
            return false;
        if (!relic.IsTradable || relic.IsUsedUp || relic.IsMelted || relic.IsWax)
            return false;
        if (relic.HasUponPickupEffect || relic.SpawnsPets || relic.AddsPet || relic.IsStackable)
            return false;

        return !GetDenylist().Contains(relic.Id.Entry);
    }

    public static int GetSalePrice(RelicModel relic) =>
        BlackMarketPricing.CalculateRelicSalePrice(
            relic.MerchantCost,
            EconomyConfig.RelicSalePricePercent);

    private static HashSet<string> GetDenylist()
    {
        HashSet<string> result = new(BuiltInDenylist, StringComparer.OrdinalIgnoreCase);
        foreach (string entry in EconomyConfig.RelicSaleDenylist.Split(
                     [',', ';', '\n', '\r'],
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            result.Add(entry);
        }
        return result;
    }
}
