using MegaCrit.Sts2.Core.Entities.Players;
using SpireEconomy.SpireEconomyCode.Configuration;
using System.Runtime.CompilerServices;

namespace SpireEconomy.SpireEconomyCode.Debt;

public static class LoanService
{
    private static readonly ConditionalWeakTable<Player, SemaphoreSlim> FinancingLocks = new();

    public static int GetPurchaseShortfall(int currentGold, int purchasePrice) =>
        LoanMath.GetPurchaseShortfall(currentGold, purchasePrice);

    public static int GetPurchaseShortfall(Player player, int purchasePrice) =>
        GetPurchaseShortfall(player.Gold, purchasePrice);

    public static int GetBorrowCapacity(Player player) =>
        LoanMath.GetBorrowCapacity(DebtManager.GetDebt(player), EconomyConfig.MaxDebt);

    public static bool CanBorrow(Player player, int amount) =>
        CanBorrowFromDebt(DebtManager.GetDebt(player), amount);

    internal static bool CanBorrowWithPending(Player player, int amount, int pendingDebt)
    {
        long effectiveDebt = (long)DebtManager.GetDebt(player) + Math.Max(0, pendingDebt);
        return effectiveDebt <= int.MaxValue && CanBorrowFromDebt((int)effectiveDebt, amount);
    }

    internal static bool CanBorrowFromDebt(int outstandingDebt, int amount) =>
        LoanMath.CanBorrow(outstandingDebt, amount, EconomyConfig.MaxDebt);

    public static bool CanFinancePurchase(Player player, int purchasePrice)
    {
        int shortfall = GetPurchaseShortfall(player, purchasePrice);
        return shortfall > 0 && CanBorrow(player, shortfall);
    }

    /// <summary>
    /// Runs a purchase callback and commits only its shortfall as debt after the callback reports
    /// success. The callback remains responsible for the vanilla cash deduction and item grant.
    /// </summary>
    public static async Task<bool> TryFinancePurchase(
        Player player,
        int purchasePrice,
        Func<Task<bool>> purchase)
    {
        SemaphoreSlim gate = FinancingLocks.GetValue(player, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();
        try
        {
            int shortfall = GetPurchaseShortfall(player, purchasePrice);
            if (shortfall == 0)
                return await purchase();
            if (!CanBorrow(player, shortfall))
                return false;

            bool purchased = await purchase();
            if (!purchased)
                return false;

            if (!await DebtManager.TryAddDebtAsync(player, shortfall))
                throw new InvalidOperationException("The item was granted but its debt could not be persisted.");

            return true;
        }
        finally
        {
            gate.Release();
        }
    }
}

/// <summary>Pure balance logic kept independent of game objects for unit tests.</summary>
public static class LoanMath
{
    public static int GetPurchaseShortfall(int currentGold, int purchasePrice) =>
        Math.Max(0, Math.Max(0, purchasePrice) - Math.Max(0, currentGold));

    public static int GetBorrowCapacity(int outstandingDebt, int maxDebt) =>
        Math.Max(0, Math.Max(0, maxDebt) - Math.Max(0, outstandingDebt));

    public static bool CanBorrow(int outstandingDebt, int amount, int maxDebt) =>
        amount > 0 && amount <= GetBorrowCapacity(outstandingDebt, maxDebt);

    public static bool IsValidRepayment(int gold, int debt, int amount) =>
        amount > 0 && amount <= Math.Min(Math.Max(0, gold), Math.Max(0, debt));

    public static (int Gold, int Debt) ApplyRepayment(int gold, int debt, int amount)
    {
        if (!IsValidRepayment(gold, debt, amount))
            throw new ArgumentOutOfRangeException(nameof(amount));
        return (gold - amount, debt - amount);
    }
}
