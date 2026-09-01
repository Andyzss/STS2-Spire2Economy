using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models.CardPools;
using SpireEconomy.SpireEconomyCode.Cards;

namespace SpireEconomy.SpireEconomyCode.Debt;

[Pool(typeof(CurseCardPool))]
public sealed class DebtCurse : SpireEconomyCard
{
    // This is the sole persisted debt value. DebtManager is the only gameplay API that mutates it.
    internal static readonly SavedSpireField<DebtCurse, int> OutstandingDebt = CreateDebtField();

    public DebtCurse()
        : base(0, CardType.Curse, CardRarity.Curse, TargetType.None, showInCardLibrary: false)
    {
    }

    public override bool CanBeGeneratedInCombat => false;
    public override bool CanBeGeneratedByModifiers => false;
    public override int MaxUpgradeLevel => 0;
    protected override bool IsPlayable => false;

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        description.Add("Debt", DebtManager.GetDebt(this));
    }

    private static SavedSpireField<DebtCurse, int> CreateDebtField()
    {
        SavedSpireField<DebtCurse, int> field = new(() => 0, "OutstandingDebt");
        field.CopyOnClone();
        return field;
    }

    internal static void RegisterSavedFields() => _ = OutstandingDebt;
}
