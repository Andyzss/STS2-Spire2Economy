using SpireEconomy.SpireEconomyCode.Debt;
using Xunit;

namespace SpireEconomy.Tests;

public sealed class LoanMathTests
{
    [Fact]
    public void BorrowOneGoldUsesExactShortfall() =>
        Assert.Equal(1, LoanMath.GetPurchaseShortfall(99, 100));

    [Fact]
    public void BorrowExactlyToMaxDebtIsAllowed() =>
        Assert.True(LoanMath.CanBorrow(250, 50, 300));

    [Fact]
    public void BorrowBeyondMaxDebtIsRejected() =>
        Assert.False(LoanMath.CanBorrow(250, 51, 300));

    [Fact]
    public void SufficientGoldHasNoShortfall() =>
        Assert.Equal(0, LoanMath.GetPurchaseShortfall(100, 100));

    [Theory]
    [InlineData(100, 300, 1, true)]
    [InlineData(100, 300, 100, true)]
    [InlineData(100, 300, 101, false)]
    [InlineData(100, 300, 0, false)]
    [InlineData(0, 300, 1, false)]
    public void RepaymentBoundsAreEnforced(int gold, int debt, int amount, bool expected) =>
        Assert.Equal(expected, LoanMath.IsValidRepayment(gold, debt, amount));

    [Fact]
    public void PartialRepaymentReducesGoldAndDebtEqually() =>
        Assert.Equal((70, 170), LoanMath.ApplyRepayment(100, 200, 30));

    [Fact]
    public void FullRepaymentLeavesNoDebt() =>
        Assert.Equal((50, 0), LoanMath.ApplyRepayment(100, 50, 50));

    [Fact]
    public void InvalidRepaymentCannotCreateNegativeBalances() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => LoanMath.ApplyRepayment(10, 5, 6));
}
