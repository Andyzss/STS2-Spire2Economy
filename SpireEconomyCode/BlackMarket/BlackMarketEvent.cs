using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Runs;

namespace SpireEconomy.SpireEconomyCode.BlackMarket;

public sealed class BlackMarketEvent : CustomEventModel
{
    public override bool IsShared => false;
    public override bool IsDeterministic => true;
    public override EventLayoutType LayoutType => EventLayoutType.Custom;

    public MerchantInventory? MerchantInventory { get; private set; }

    // Natural insertion is handled by BlackMarketEventSelectionPatch so the configured
    // percentage is meaningful. Returning false prevents BaseLib's unweighted pool entry.
    public override bool IsAllowed(IRunState runState) => false;

    protected override Task BeforeEventStarted(bool resumed)
    {
        MerchantInventory ??= BlackMarketMerchantInventoryFactory.Create(
            Owner ?? throw new InvalidOperationException("Black Market owner is not initialized."));
        return Task.CompletedTask;
    }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() => [];

    internal void FinishMarket()
    {
        if (!IsFinished)
            SetEventFinished(new LocString("events", $"{Id.Entry}.pages.LEAVE.description"));
    }
}
