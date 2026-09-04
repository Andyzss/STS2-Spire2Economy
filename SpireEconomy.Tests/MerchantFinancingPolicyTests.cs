using SpireEconomy.SpireEconomyCode.Debt;
using Xunit;

namespace SpireEconomy.Tests;

public sealed class MerchantFinancingPolicyTests
{
    private const int MaxDebt = 300;

    [Fact]
    public void SufficientGoldPreservesVanillaPurchase()
    {
        FinancingDecision result = Evaluate(PlayerPurchase(100), gold: 100);
        Assert.False(result.Allowed);
        Assert.Equal(0, result.Shortfall);
    }

    [Fact]
    public void PlayerPurchaseUsesExactShortfall()
    {
        FinancingDecision result = Evaluate(PlayerPurchase(120), gold: 70);
        Assert.True(result.Allowed);
        Assert.Equal(50, result.Shortfall);
    }

    [Fact]
    public void PurchaseBeyondMaxDebtIsRejected()
    {
        FinancingDecision result = Evaluate(PlayerPurchase(100), gold: 49, debt: 250);
        Assert.False(result.Allowed);
    }

    [Fact]
    public void FreeMerchantItemCannotCreateDebt()
    {
        FinancingDecision result = Evaluate(PlayerPurchase(0), gold: 0);
        Assert.False(result.Allowed);
    }

    [Fact]
    public void IgnoreCostPurchaseCannotCreateDebt()
    {
        PurchaseContext context = new(
            PurchaseSource.PlayerInitiated,
            RequiresGoldPayment: false,
            FinalCost: 120,
            MerchantItemType.Relic);
        Assert.False(Evaluate(context, gold: 0).Allowed);
    }

    [Fact]
    public void LordsParasolStyleAutomaticFreePurchaseCannotCreateDebt()
    {
        PurchaseContext context = new(
            PurchaseSource.Automatic,
            RequiresGoldPayment: false,
            FinalCost: 250,
            MerchantItemType.Relic);
        Assert.False(Evaluate(context, gold: 0, debt: 100).Allowed);
    }

    [Theory]
    [InlineData(PurchaseSource.Automatic)]
    [InlineData(PurchaseSource.Forced)]
    [InlineData(PurchaseSource.NonMerchant)]
    [InlineData(PurchaseSource.Unknown)]
    public void NonPlayerSourcesCannotCreateDebt(PurchaseSource source)
    {
        PurchaseContext context = new(source, true, 100, MerchantItemType.Card);
        Assert.False(Evaluate(context, gold: 0).Allowed);
    }

    [Fact]
    public void FinalModifiedCostDrivesShortfall()
    {
        // Base price is intentionally absent: only the final vanilla Cost=120 is accepted.
        FinancingDecision result = Evaluate(PlayerPurchase(120), gold: 70);
        Assert.Equal(50, result.Shortfall);
    }

    [Fact]
    public void PaidCardRemovalIsExplicitlyFinanceable()
    {
        PurchaseContext context = new(
            PurchaseSource.PlayerInitiated,
            RequiresGoldPayment: true,
            FinalCost: 75,
            MerchantItemType.CardRemoval);
        Assert.True(Evaluate(context, gold: 25).Allowed);
    }

    [Fact]
    public void AutomaticPurchaseDoesNotDisableLaterManualRestockPurchase()
    {
        PurchaseContext automatic = new(
            PurchaseSource.Automatic,
            RequiresGoldPayment: false,
            FinalCost: 120,
            MerchantItemType.Card);
        PurchaseContext laterPlayerPurchase = PlayerPurchase(120);

        Assert.False(Evaluate(automatic, gold: 70).Allowed);
        FinancingDecision later = Evaluate(laterPlayerPurchase, gold: 70);
        Assert.True(later.Allowed);
        Assert.Equal(50, later.Shortfall);
    }

    [Fact]
    public void UnsupportedMerchantEntryCannotCreateDebt()
    {
        PurchaseContext context = new(
            PurchaseSource.PlayerInitiated,
            RequiresGoldPayment: true,
            FinalCost: 100,
            MerchantItemType.Unsupported);
        Assert.False(Evaluate(context, gold: 0).Allowed);
    }

    [Fact]
    public void DuplicateTransactionCallbackCanSettleOnlyOnce()
    {
        TransactionSettlementGate gate = new();
        Assert.True(gate.TryBegin());
        Assert.False(gate.TryBegin());
    }

    private static PurchaseContext PlayerPurchase(int finalCost) => new(
        PurchaseSource.PlayerInitiated,
        RequiresGoldPayment: true,
        FinalCost: finalCost,
        MerchantItemType.Card);

    private static FinancingDecision Evaluate(
        PurchaseContext context,
        int gold,
        int debt = 0) =>
        MerchantFinancingPolicy.Evaluate(context, gold, debt, MaxDebt);
}
