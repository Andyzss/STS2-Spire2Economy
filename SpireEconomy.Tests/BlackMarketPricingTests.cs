using SpireEconomy.SpireEconomyCode.BlackMarket;
using SpireEconomy.SpireEconomyCode.Debt;
using SpireEconomy.SpireEconomyCode.Patches;
using Xunit;

namespace SpireEconomy.Tests;

public sealed class BlackMarketPricingTests
{
    [Theory]
    [InlineData(100, 150, 150)]
    [InlineData(101, 150, 152)]
    [InlineData(0, 150, 0)]
    [InlineData(-10, 150, 0)]
    public void PurchaseMarkupUsesConfiguredPercentage(int basePrice, int percent, int expected) =>
        Assert.Equal(expected, BlackMarketPricing.ApplyPurchaseMarkup(basePrice, percent));

    [Fact]
    public void PurchaseMarkupCannotDiscountBelowVanillaPrice() =>
        Assert.Equal(100, BlackMarketPricing.ApplyPurchaseMarkup(100, 50));

    [Theory]
    [InlineData(100, 125)]
    [InlineData(101, 126)]
    [InlineData(150, 188)]
    public void BlackMarketCardsUseTwentyFivePercentMarkup(int basePrice, int expected) =>
        Assert.Equal(expected, BlackMarketPricing.ApplyPurchaseMarkup(
            basePrice,
            BlackMarketPricing.CardPricePercent));

    [Theory]
    [InlineData(4_999_999, true, BlackMarketPricing.SpecialRelicBaseCost)]
    [InlineData(280, false, 280)]
    public void SpecialRelicsDoNotUseVanillaSentinelMerchantPrices(
        int vanillaBasePrice,
        bool isSpecialRelic,
        int expected) =>
        Assert.Equal(expected, BlackMarketPricing.GetPurchaseBasePrice(vanillaBasePrice, isSpecialRelic));

    [Theory]
    [InlineData(200, 50, 100)]
    [InlineData(201, 50, 101)]
    [InlineData(200, 0, 0)]
    [InlineData(200, 150, 200)]
    public void RelicSalePriceIsClampedToZeroThroughOneHundredPercent(
        int basePrice,
        int percent,
        int expected) =>
        Assert.Equal(expected, BlackMarketPricing.CalculateRelicSalePrice(basePrice, percent));

    [Fact]
    public void BlackMarketCardPurchaseCanUseExactShortfall()
    {
        PurchaseContext context = new(
            PurchaseSource.BlackMarket,
            RequiresGoldPayment: true,
            FinalCost: 225,
            MerchantItemType.Card);

        FinancingDecision result = MerchantFinancingPolicy.Evaluate(
            context,
            currentGold: 200,
            outstandingDebt: 100,
            maxDebt: 300);

        Assert.True(result.Allowed);
        Assert.Equal(25, result.Shortfall);
    }

    [Fact]
    public void BlackMarketPurchaseStillRespectsMaxDebt()
    {
        PurchaseContext context = new(
            PurchaseSource.BlackMarket,
            RequiresGoldPayment: true,
            FinalCost: 225,
            MerchantItemType.Relic);

        FinancingDecision result = MerchantFinancingPolicy.Evaluate(
            context,
            currentGold: 200,
            outstandingDebt: 290,
            maxDebt: 300);

        Assert.False(result.Allowed);
    }

    [Theory]
    [InlineData(0, 3, true)]
    [InlineData(2, 3, true)]
    [InlineData(3, 3, false)]
    [InlineData(99, 100, true)]
    [InlineData(0, 0, false)]
    public void EventSelectionUsesConfiguredPercent(int roll, int chance, bool expected) =>
        Assert.Equal(expected, BlackMarketEventSelection.ShouldReplace(
            roll,
            chance,
            alreadyVisited: false,
            alreadySelected: false));

    [Fact]
    public void EventSelectionCannotRepeatAfterVisit() =>
        Assert.False(BlackMarketEventSelection.ShouldReplace(
            roll: 0,
            chancePercent: 100,
            alreadyVisited: true,
            alreadySelected: false));
}
