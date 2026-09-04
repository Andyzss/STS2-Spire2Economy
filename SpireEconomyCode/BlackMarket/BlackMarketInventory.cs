using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;

namespace SpireEconomy.SpireEconomyCode.BlackMarket;

public sealed class BlackMarketInventory
{
    private const int RareCardVanillaBasePrice = 150;
    private const int ColorlessCardPricePercent = 115;

    private BlackMarketInventory(
        IReadOnlyList<BlackMarketRelicOffer> relics,
        IReadOnlyList<BlackMarketCardOffer> cards)
    {
        Relics = relics;
        Cards = cards;
    }

    public IReadOnlyList<BlackMarketRelicOffer> Relics { get; }
    public IReadOnlyList<BlackMarketCardOffer> Cards { get; }

    public static BlackMarketInventory Generate(Player player, Rng rng)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(rng);

        player.PopulateRelicGrabBagIfNecessary(rng);
        List<BlackMarketRelicOffer> relicOffers = [];
        for (int i = 0; i < 2; i++)
        {
            RelicModel? relic = TryPullRareRelic(player);
            if (relic is null)
                break;

            int price = Math.Max(0, relic.MerchantCost);
            relicOffers.Add(new BlackMarketRelicOffer(relic, price));
        }

        List<CardModel> candidates = GetRareCardCandidates(player)
            .GroupBy(card => card.Id)
            .Select(group => group.First())
            .ToList();
        rng.Shuffle(candidates);

        int requestedCardCount = rng.NextBool() ? 3 : 2;
        List<BlackMarketCardOffer> cardOffers = candidates
            .Take(requestedCardCount)
            .Select(card => new BlackMarketCardOffer(card, GetCardPrice(card)))
            .ToList();

        return new BlackMarketInventory(relicOffers, cardOffers);
    }

    private static RelicModel? TryPullRareRelic(Player player)
    {
        try
        {
            return player.RelicGrabBag.PullFromFront(
                RelicRarity.Rare,
                relic => relic.Rarity == RelicRarity.Rare &&
                         relic.IsAllowed(player.RunState) &&
                         player.GetRelicById(relic.Id) is null,
                player.RunState);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static IEnumerable<CardModel> GetRareCardCandidates(Player player)
    {
        IEnumerable<CardModel> characterCards = player.Character.CardPool.GetUnlockedCards(
            player.UnlockState,
            player.RunState.CardMultiplayerConstraint);
        IEnumerable<CardModel> colorlessCards = ModelDb.AllSharedCardPools
            .Where(pool => pool.IsColorless)
            .SelectMany(pool => pool.GetUnlockedCards(
                player.UnlockState,
                player.RunState.CardMultiplayerConstraint));

        return characterCards.Concat(colorlessCards)
            .Where(card => card.Rarity == CardRarity.Rare);
    }

    private static int GetCardPrice(CardModel card)
    {
        int basePrice = card.Pool.IsColorless
            ? BlackMarketPricing.ApplyPercentage(RareCardVanillaBasePrice, ColorlessCardPricePercent)
            : RareCardVanillaBasePrice;
        return BlackMarketPricing.ApplyPurchaseMarkup(basePrice, BlackMarketPricing.CardPricePercent);
    }
}

public sealed class BlackMarketRelicOffer(RelicModel relic, int price) : BlackMarketOffer(price)
{
    public RelicModel Relic { get; } = relic;
}

public sealed class BlackMarketCardOffer(CardModel card, int price) : BlackMarketOffer(price)
{
    public CardModel Card { get; } = card;
}

public abstract class BlackMarketOffer
{
    private int _state;

    protected BlackMarketOffer(int price) => Price = Math.Max(0, price);

    public int Price { get; }
    public bool IsPurchased => Volatile.Read(ref _state) == 2;
    internal bool TryReserve() => Interlocked.CompareExchange(ref _state, 1, 0) == 0;
    internal void Commit() => Interlocked.CompareExchange(ref _state, 2, 1);
    internal void Release() => Interlocked.CompareExchange(ref _state, 0, 1);
}

public static class BlackMarketPricing
{
    public const int CardPricePercent = 125;

    // Ancient/Event relics are not normally merchant stock and may expose a sentinel MerchantCost.
    // Give them an explicit named base value before applying the configurable market multiplier.
    public const int SpecialRelicBaseCost = 200;

    public static int GetPurchaseBasePrice(int vanillaBasePrice, bool isSpecialRelic) =>
        isSpecialRelic ? SpecialRelicBaseCost : Math.Max(0, vanillaBasePrice);

    public static int ApplyPurchaseMarkup(int basePrice, int pricePercent) =>
        ApplyPercentage(basePrice, Math.Max(100, pricePercent));

    public static int CalculateRelicSalePrice(int basePrice, int salePercent) =>
        ApplyPercentage(basePrice, Math.Clamp(salePercent, 0, 100));

    public static int ApplyPercentage(int value, int percent)
    {
        decimal result = Math.Max(0, value) * Math.Max(0, percent) / 100m;
        return decimal.ToInt32(decimal.Round(result, 0, MidpointRounding.AwayFromZero));
    }
}
