using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using SpireEconomy.SpireEconomyCode.Debt;

namespace SpireEconomy.SpireEconomyCode.BlackMarket;

internal static class BlackMarketAncientRelicCost
{
    internal const int Minimum = 5;
    internal const int Maximum = 15;

    private static readonly ConditionalWeakTable<MerchantRelicEntry, CostState> Costs = new();

    internal static void Register(MerchantRelicEntry entry, int amount) =>
        Costs.AddOrUpdate(entry, new CostState(Math.Clamp(amount, Minimum, Maximum)));

    internal static bool TryGet(MerchantRelicEntry entry, out int amount)
    {
        if (Costs.TryGetValue(entry, out CostState? state))
        {
            amount = state.Amount;
            return true;
        }

        amount = 0;
        return false;
    }

    internal static async Task<bool> ApplyAfterSuccessfulPurchase(
        MerchantEntry entry,
        Player player,
        bool ignoreCost,
        Task<bool> purchase)
    {
        bool purchased = await purchase;
        if (!purchased || ignoreCost || entry is not MerchantRelicEntry relicEntry ||
            !Costs.TryGetValue(relicEntry, out CostState? state) || !state.Settlement.TryBegin())
            return purchased;

        await CreatureCmd.LoseMaxHp(new ThrowingPlayerChoiceContext(), player.Creature, state.Amount, false);
        return true;
    }

    private sealed record CostState(int Amount)
    {
        internal TransactionSettlementGate Settlement { get; } = new();
    }
}
