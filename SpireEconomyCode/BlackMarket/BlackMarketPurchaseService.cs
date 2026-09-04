using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;
using SpireEconomy.SpireEconomyCode.Debt;

namespace SpireEconomy.SpireEconomyCode.BlackMarket;

public static class BlackMarketPurchaseService
{
    private static readonly ConditionalWeakTable<Player, SemaphoreSlim> PlayerLocks = new();

    public static bool CanPurchase(Player player, BlackMarketOffer offer)
    {
        if (offer.IsPurchased)
            return false;
        if (player.Gold >= offer.Price)
            return true;

        int shortfall = LoanService.GetPurchaseShortfall(player, offer.Price);
        return shortfall > 0 && LoanService.CanBorrow(player, shortfall);
    }

    public static Task<bool> TryPurchaseCard(Player player, BlackMarketCardOffer offer) =>
        TryPurchase(player, offer, MerchantItemType.Card, () => GrantCard(player, offer.Card));

    public static Task<bool> TryPurchaseRelic(Player player, BlackMarketRelicOffer offer) =>
        TryPurchase(player, offer, MerchantItemType.Relic, () => GrantRelic(player, offer.Relic));

    private static async Task<bool> TryPurchase(
        Player player,
        BlackMarketOffer offer,
        MerchantItemType itemType,
        Func<Task<bool>> grant)
    {
        SemaphoreSlim gate = PlayerLocks.GetValue(player, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();
        try
        {
            if (!offer.TryReserve())
                return false;

            bool success;
            try
            {
                PurchaseContext context = new(
                    PurchaseSource.BlackMarket,
                    RequiresGoldPayment: true,
                    FinalCost: offer.Price,
                    ItemType: itemType);

                success = player.Gold < offer.Price
                    ? await LoanService.TryFinancePurchase(
                        player,
                        context,
                        () => PayAndGrant(player, offer.Price, grant))
                    : await PayAndGrant(player, offer.Price, grant);
            }
            catch
            {
                offer.Release();
                throw;
            }

            if (success)
                offer.Commit();
            else
                offer.Release();
            return success;
        }
        finally
        {
            gate.Release();
        }
    }

    private static async Task<bool> PayAndGrant(Player player, int price, Func<Task<bool>> grant)
    {
        int goldBefore = player.Gold;
        await PlayerCmd.LoseGold(price, player, GoldLossType.Spent);
        try
        {
            if (!await grant())
            {
                player.Gold = goldBefore;
                return false;
            }
        }
        catch
        {
            player.Gold = goldBefore;
            throw;
        }

        RunManager.Instance.RewardSynchronizer.SyncLocalGoldLost(price);
        return true;
    }

    private static async Task<bool> GrantCard(Player player, CardModel canonical)
    {
        CardModel card = ((RunState)player.RunState).CreateCard(canonical, player);
        CardPileAddResult result = await CardPileCmd.Add(
            card,
            player.Deck,
            CardPilePosition.Bottom,
            null,
            false);
        if (!result.success)
        {
            player.RunState.RemoveCard(card);
            return false;
        }

        RunManager.Instance.RewardSynchronizer.SyncLocalObtainedCard(card);
        return true;
    }

    private static async Task<bool> GrantRelic(Player player, RelicModel canonical)
    {
        RelicModel relic = canonical.ToMutable();
        await RelicCmd.Obtain(relic, player, -1);
        RunManager.Instance.RewardSynchronizer.SyncLocalObtainedRelic(relic);
        return true;
    }
}

public static class BlackMarketSaleService
{
    public static async Task<bool> TrySellRelic(Player player, RelicModel relic)
    {
        if (!RelicSaleRules.CanSell(player, relic))
            return false;

        int salePrice = RelicSaleRules.GetSalePrice(relic);
        if (salePrice <= 0)
            return false;

        await RelicCmd.Remove(relic);
        if (player.Relics.Contains(relic))
            return false;

        await PlayerCmd.GainGold(salePrice, player, false);
        RunManager.Instance.RewardSynchronizer.SyncLocalObtainedGold(salePrice);
        return true;
    }
}
