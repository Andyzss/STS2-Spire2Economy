using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using SpireEconomy.SpireEconomyCode.Configuration;
using SpireEconomy.SpireEconomyCode.BlackMarket;
using SpireEconomy.SpireEconomyCode.Debt;
using System.Reflection;

namespace SpireEconomy.SpireEconomyCode.Patches;

internal static class MerchantFinancingReservations
{
    private static readonly FinancingReservationLedger<MerchantEntry, Reservation> Active = new();
    private static readonly object Gate = new();
    [ThreadStatic] private static MerchantEntry? _authorizedEntry;
    [ThreadStatic] private static MerchantEntry? _blockedEntry;

    internal static MerchantItemType GetItemType(MerchantEntry entry) => entry switch
    {
        MerchantCardEntry => MerchantItemType.Card,
        MerchantRelicEntry => MerchantItemType.Relic,
        MerchantPotionEntry => MerchantItemType.Potion,
        // Explicit project exception: paid card removal remains financeable in v0.1.
        MerchantCardRemovalEntry => MerchantItemType.CardRemoval,
        _ => MerchantItemType.Unsupported
    };

    internal static bool IsLoanEligible(MerchantEntry entry) =>
        GetItemType(entry) != MerchantItemType.Unsupported;

    internal static bool BeginAttempt(MerchantEntry entry, Player player, bool ignoreCost)
    {
        _authorizedEntry = null;
        _blockedEntry = null;
        MerchantPurchaseScope.ScopeState? scope = MerchantPurchaseScope.Current;
        if (scope is null || !scope.Matches(entry) ||
            scope.Source != PurchaseSource.PlayerInitiated)
            return false;

        int finalCost = entry.Cost;
        PurchaseContext context = new(
            PurchaseSource.PlayerInitiated,
            RequiresGoldPayment: !ignoreCost,
            FinalCost: finalCost,
            ItemType: GetItemType(entry));

        lock (Gate)
        {
            int pendingDebt = Active.SnapshotValues
                .Where(reservation => ReferenceEquals(reservation.Player, player))
                .Sum(reservation => reservation.Shortfall);
            int effectiveDebt = checked(DebtManager.GetDebt(player) + pendingDebt);
            FinancingDecision decision = MerchantFinancingPolicy.Evaluate(
                context,
                player.Gold,
                effectiveDebt,
                EconomyConfig.MaxDebt);
            if (!decision.Allowed)
                return false;

            if (Active.TryReserve(entry, new Reservation(player, finalCost, player.Gold, decision.Shortfall)))
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
        MerchantPurchaseScope.ScopeState? scope = MerchantPurchaseScope.Current;
        if (scope is null || !scope.Matches(entry) ||
            scope.Source is not (PurchaseSource.UiPreview or PurchaseSource.PlayerInitiated))
            return false;
        if (ReferenceEquals(_authorizedEntry, entry))
            return true;
        if (ReferenceEquals(_blockedEntry, entry) || Active.Contains(entry))
            return false;
        int shortfall = LoanService.GetPurchaseShortfall(player, entry.Cost);
        lock (Gate)
        {
            int pendingDebt = Active.SnapshotValues
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
            return Active.TryGet(entry, out reservation!);
    }

    internal static void Release(MerchantEntry entry)
    {
        lock (Gate)
            Active.Release(entry);
    }

    internal static async Task<bool> CompleteFinancedPurchase(
        MerchantEntry entry,
        Task<bool> vanillaPurchase)
    {
        if (!TryGet(entry, out Reservation? reservation))
            return await vanillaPurchase;
        if (!reservation.Settlement.TryBegin())
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
                // Restore only the wallet mutation. Vanilla currently mutates inventory/deck only
                // on success; item/removal rollback remains an upstream atomicity limitation.
                reservation.Player.Gold = reservation.GoldBefore;
                throw;
            }

            if (!purchased)
            {
                reservation.Player.Gold = reservation.GoldBefore;
                return false;
            }

            if (!await DebtManager.TryAddDebtAsync(reservation.Player, reservation.Shortfall))
                throw new InvalidOperationException(
                    "Financed merchant purchase completed without durable debt state.");

            return true;
        }
        finally
        {
            Release(entry);
        }
    }

    internal sealed record Reservation(Player Player, int Price, int GoldBefore, int Shortfall)
    {
        internal TransactionSettlementGate Settlement { get; } = new();
    }
}

[HarmonyPatch]
internal static class MerchantFinancingPreviewContextPatch
{
    private static IEnumerable<MethodBase> TargetMethods() => MerchantUiTypes
        .Select(type => AccessTools.DeclaredMethod(type, "UpdateVisual"))
        .OfType<MethodBase>();

    private static readonly Type[] MerchantUiTypes =
    [
        typeof(NMerchantCard),
        typeof(NMerchantRelic),
        typeof(NMerchantPotion),
        typeof(NMerchantCardRemoval)
    ];

    private static void Prefix(NMerchantSlot __instance, out IDisposable __state) =>
        __state = MerchantPurchaseScope.Begin(__instance.Entry, PurchaseSource.UiPreview);

    private static void Postfix(IDisposable __state) => __state.Dispose();
}

[HarmonyPatch]
internal static class MerchantPlayerPurchaseContextPatch
{
    private static IEnumerable<MethodBase> TargetMethods() => MerchantUiTypes
        .Select(type => AccessTools.DeclaredMethod(
            type,
            "OnTryPurchase",
            [typeof(MerchantInventory)]))
        .OfType<MethodBase>();

    private static readonly Type[] MerchantUiTypes =
    [
        typeof(NMerchantCard),
        typeof(NMerchantRelic),
        typeof(NMerchantPotion),
        // Paid removal is an intentional v0.1 financing exception requested by the project owner.
        typeof(NMerchantCardRemoval)
    ];

    private static void Prefix(NMerchantSlot __instance, out IDisposable __state) =>
        __state = MerchantPurchaseScope.Begin(__instance.Entry, PurchaseSource.PlayerInitiated);

    private static void Postfix(IDisposable __state) => __state.Dispose();
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

    private static void Postfix(
        MerchantEntry __instance,
        MerchantInventory inventory,
        bool ignoreCost,
        bool __state,
        ref Task<bool> __result)
    {
        MerchantFinancingReservations.EndSynchronousAttempt();
        if (__state)
            __result = MerchantFinancingReservations.CompleteFinancedPurchase(__instance, __result);
        __result = BlackMarketAncientRelicCost.ApplyAfterSuccessfulPurchase(
            __instance,
            inventory.Player,
            ignoreCost,
            __result);
    }
}

[HarmonyPatch(
    typeof(MerchantCardRemovalEntry),
    nameof(MerchantCardRemovalEntry.OnTryPurchaseWrapper),
    typeof(MerchantInventory),
    typeof(bool),
    typeof(bool))]
internal static class MerchantCardRemovalLoanPurchasePatch
{
    private static void Prefix(
        MerchantCardRemovalEntry __instance,
        MerchantInventory inventory,
        bool ignoreCost,
        out bool __state)
    {
        __state = MerchantFinancingReservations.BeginAttempt(__instance, inventory.Player, ignoreCost);
    }

    private static void Postfix(
        MerchantCardRemovalEntry __instance,
        bool __state,
        ref Task<bool> __result)
    {
        MerchantFinancingReservations.EndSynchronousAttempt();
        if (__state)
            __result = MerchantFinancingReservations.CompleteFinancedPurchase(__instance, __result);
    }
}
