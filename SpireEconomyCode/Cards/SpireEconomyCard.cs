using BaseLib.Abstracts;
using BaseLib.Extensions;
using SpireEconomy.SpireEconomyCode.Extensions;
using MegaCrit.Sts2.Core.Entities.Cards;

namespace SpireEconomy.SpireEconomyCode.Cards;

// Shared asset-path behavior for this mod's cards. Concrete cards select their own pool.
public abstract class SpireEconomyCard : ConstructedCardModel
{
    protected SpireEconomyCard(
        int cost,
        CardType type,
        CardRarity rarity,
        TargetType target,
        bool showInCardLibrary = true,
        bool autoAdd = true)
        : base(cost, type, rarity, target, showInCardLibrary, autoAdd)
    {
    }

    // The class name gives the portrait filename: DebtCurse -> debt_curse.png
    public override string CustomPortraitPath => $"{Id.Entry.RemovePrefix().ToLowerInvariant()}.png".CardImagePath();
    public override string PortraitPath => $"{Id.Entry.RemovePrefix().ToLowerInvariant()}.png".CardImagePath();
}
