using MegaCrit.Sts2.Core.Entities.Merchant;

namespace SpireEconomy.SpireEconomyCode.Debt;

public enum PurchaseSource
{
    Unknown,
    UiPreview,
    PlayerInitiated,
    BlackMarket,
    Automatic,
    Forced,
    NonMerchant
}

public enum MerchantItemType
{
    Unsupported,
    Card,
    Relic,
    Potion,
    CardRemoval
}

/// <summary>A value snapshot used to decide whether one transaction may create debt.</summary>
public readonly record struct PurchaseContext(
    PurchaseSource Source,
    bool RequiresGoldPayment,
    int FinalCost,
    MerchantItemType ItemType)
{
    public bool PlayerInitiated => Source is PurchaseSource.PlayerInitiated or PurchaseSource.BlackMarket;
    public bool IsAutomatic => Source == PurchaseSource.Automatic;
    public bool IsForced => Source == PurchaseSource.Forced;
    public bool IsFree => !RequiresGoldPayment || FinalCost <= 0;
    public bool IsFinanceEligible =>
        PlayerInitiated &&
        !IsAutomatic &&
        !IsForced &&
        !IsFree &&
        ItemType != MerchantItemType.Unsupported;
}

public readonly record struct FinancingDecision(bool Allowed, int Shortfall)
{
    public static FinancingDecision Denied => new(false, 0);
}

/// <summary>Pure transaction policy. It never mutates Gold, Debt, inventory, or event state.</summary>
public static class MerchantFinancingPolicy
{
    public static FinancingDecision Evaluate(
        PurchaseContext context,
        int currentGold,
        int outstandingDebt,
        int maxDebt)
    {
        if (!context.IsFinanceEligible)
            return FinancingDecision.Denied;

        int shortfall = LoanMath.GetPurchaseShortfall(currentGold, context.FinalCost);
        if (shortfall <= 0 || !LoanMath.CanBorrow(outstandingDebt, shortfall, maxDebt))
            return FinancingDecision.Denied;

        return new FinancingDecision(true, shortfall);
    }
}

/// <summary>
/// Marks only the synchronous UI-to-entity call boundary. Automatic callers such as Lord's
/// Parasol and AutoSlay never receive this scope and therefore cannot enter financing.
/// </summary>
internal static class MerchantPurchaseScope
{
    [ThreadStatic] private static ScopeState? _current;

    internal static ScopeState? Current => _current;

    internal static IDisposable Begin(MerchantEntry? entry, PurchaseSource source)
    {
        ScopeState? previous = _current;
        _current = entry is null ? previous : new ScopeState(entry, source);
        return new Scope(previous);
    }

    internal sealed record ScopeState(MerchantEntry Entry, PurchaseSource Source)
    {
        internal bool Matches(MerchantEntry entry) => ReferenceEquals(Entry, entry);
    }

    private sealed class Scope(ScopeState? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _current = previous;
        }
    }
}
