using SpireEconomy.SpireEconomyCode.Debt;
using Xunit;

namespace SpireEconomy.Tests;

public sealed class DebtLoadMathTests
{
    [Fact]
    public void EmptyOrClearedSaveHasNoDebt()
    {
        Assert.Equal(0, DebtLoadMath.GetCanonicalDebt([]));
        Assert.Equal(0, DebtLoadMath.GetCanonicalDebt([0]));
    }

    [Fact]
    public void DuplicateDebtCardsUseOneCanonicalValueInsteadOfSumming() =>
        Assert.Equal(125, DebtLoadMath.GetCanonicalDebt([125, 125, 10]));

    [Fact]
    public void DamagedNegativeSaveValueIsClamped() =>
        Assert.Equal(20, DebtLoadMath.GetCanonicalDebt([-100, 20]));
}
