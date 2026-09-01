using System.Collections.Concurrent;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using SpireEconomy.SpireEconomyCode.Debt;

namespace SpireEconomy.SpireEconomyCode.Patches;

internal static class MerchantFinancingReservations
{
    private static readonly ConcurrentDictionary<MerchantEntry, Reservation> Active = new();
    private static readonly object Gate = new();
    [ThreadStatic] private static MerchantEntry? _authorizedEntry;
    [ThreadStatic] private static MerchantEntry? _blockedEntry;

    internal static bool IsLoanEligible(MerchantEntry entry) =>
        entry is MerchantCardEntry or MerchantRelicEntry;

    internal static bool BeginAttempt(MerchantEntry entry, Player player, bool ignoreCost)
    {
        _authorizedEntry = null;
        _blockedEntry = null;
        if (ignoreCost || !IsLoanEligible(entry))
            return false;

        int shortfall = LoanService.GetPurchaseShortfall(player, entry.Cost);
        if (shortfall <= 0)
            return false;

        lock (Gate)
        {
            int pendingDebt = Active.Values
                .Where(reservation => ReferenceEquals(reservation.Player, player))
                .Sum(reservation => reservation.Shortfall);
            if (!LoanService.CanBorrowWithPending(player, shortfall, pendingDebt))
                return false;

            if (Active.TryAdd(entry, new Reservation(player, entry.Cost, player.Gold, shortfall)))
            {
                _authorizedEntry = entry;
                return true;
            }
        }

        // A second callback for the same stock item must fail its affordability check.
        _blockedEntry = entry;
        return false;
    }

    internal static bool CanDisplayOrUseFinancing(MerchantEntry entry, Player player)
    {
        if (ReferenceEquals(_authorizedEntry, entry))
            return true;
        if (ReferenceEquals(_blockedEntry, entry) || Active.ContainsKey(entry))
            return false;
        int shortfall = LoanService.GetPurchaseShortfall(player, entry.Cost);
        lock (Gate)
        {
            int pendingDebt = Active.Values
                .Where(reservation => ReferenceEquals(reservation.Player, player))
                .Sum(reservation => reservation.Shortfall);
            return shortfall > 0 && LoanService.CanBorrowWithPending(player, shortfall, pendingDebt);
        }
    }

    internal static void EndSynchronousAttempt()
    {
        _authorizedEntry = null;
        _blockedEntry = null;
    }

    internal static bool TryGet(MerchantEntry entry, out Reservation reservation)
    {
        lock (Gate)
            return Active.TryGetValue(entry, out reservation!);
    }

    internal static void Release(MerchantEntry entry)
    {
        lock (Gate)
            Active.TryRemove(entry, out _);
    }

    internal sealed record Reservation(Player Player, int Price, int GoldBefore, int Shortfall);
}

[HarmonyPatch(typeof(MerchantEntry), "get_EnoughGold")]
internal static class MerchantLoanAffordabilityPatch
{
    private static void Postfix(
        MerchantEntry __instance,
        Player ____player,
        ref bool __result)
    {
        if (!__result && MerchantFinancingReservations.IsLoanEligible(__instance))
            __result = MerchantFinancingReservations.CanDisplayOrUseFinancing(__instance, ____player);
    }
}

[HarmonyPatch(typeof(MerchantEntry), nameof(MerchantEntry.OnTryPurchaseWrapper))]
internal static class MerchantLoanPurchasePatch
{
    private static void Prefix(
        MerchantEntry __instance,
        MerchantInventory inventory,
        bool ignoreCost,
        out bool __state)
    {
        __state = MerchantFinancingReservations.BeginAttempt(__instance, inventory.Player, ignoreCost);
    }

    private static void Postfix(MerchantEntry __instance, bool __state, ref Task<bool> __result)
    {
        MerchantFinancingReservations.EndSynchronousAttempt();
        if (__state)
            __result = CompleteFinancedPurchase(__instance, __result);
    }

    private static async Task<bool> CompleteFinancedPurchase(MerchantEntry entry, Task<bool> vanillaPurchase)
    {
        if (!MerchantFinancingReservations.TryGet(entry, out var reservation))
            return await vanillaPurchase;

        try
        {
            bool purchased;
            try
            {
                purchased = await vanillaPurchase;
            }
            catch
            {
                // Restore only the wallet mutation. Vanilla currently mutates card inventory only on
                // success; relic grant exceptions remain an upstream atomicity risk documented in design.
                reservation.Player.Gold = reservation.GoldBefore;
                throw;
            }

            if (!purchased)
            {
                reservation.Player.Gold = reservation.GoldBefore;
                return false;
            }

            if (!await DebtManager.TryAddDebtAsync(reservation.Player, reservation.Shortfall))
                throw new InvalidOperationException("Financed merchant purchase completed without durable debt state.");

            return true;
        }
        finally
        {
            MerchantFinancingReservations.Release(entry);
        }
    }
}
