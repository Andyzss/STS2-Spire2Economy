using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;

namespace SpireEconomy.SpireEconomyCode.Debt;

public static class DebtManager
{
    private static readonly ConditionalWeakTable<Player, SemaphoreSlim> PlayerLocks = new();

    public static event Action<Player, int>? DebtChanged;

    public static int GetDebt(Player player) =>
        FindDebtCards(player).Select(GetDebt).DefaultIfEmpty(0).Max();

    internal static int GetDebt(DebtCurse card) =>
        Math.Max(0, DebtCurse.OutstandingDebt.Get(card));

    internal static async Task<bool> TryAddDebtAsync(Player player, int amount)
    {
        if (amount <= 0)
            return false;

        SemaphoreSlim gate = PlayerLocks.GetValue(player, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();
        try
        {
            int currentDebt = GetDebt(player);
            if (!LoanService.CanBorrowFromDebt(currentDebt, amount))
                return false;
            int nextDebt;
            try
            {
                nextDebt = checked(currentDebt + amount);
            }
            catch (OverflowException)
            {
                return false;
            }

            List<DebtCurse> cards = FindDebtCards(player);
            DebtCurse primary;
            if (cards.Count == 0)
            {
                // CreateCard registers the mutable model with this run before it enters a pile.
                primary = player.RunState.CreateCard<DebtCurse>(player);
                SetDebt(primary, nextDebt);

                CardPileAddResult result;
                try
                {
                    result = await CardPileCmd.Add(
                        primary,
                        player.Deck,
                        CardPilePosition.Bottom,
                        null,
                        false);
                }
                catch
                {
                    player.RunState.RemoveCard(primary);
                    throw;
                }
                if (!result.success)
                {
                    player.RunState.RemoveCard(primary);
                    return false;
                }

                RunManager.Instance.RewardSynchronizer.SyncLocalObtainedCard(primary);
            }
            else
            {
                primary = cards[0];
                SetDebt(primary, nextDebt);
                await RemoveDuplicatesAsync(cards.Skip(1));
            }

            DebtChanged?.Invoke(player, nextDebt);
            return true;
        }
        finally
        {
            gate.Release();
        }
    }

    public static async Task<bool> TryRepayAsync(Player player, int amount)
    {
        SemaphoreSlim gate = PlayerLocks.GetValue(player, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();
        try
        {
            int debt = GetDebt(player);
            if (!LoanMath.IsValidRepayment(player.Gold, debt, amount))
                return false;

            DebtCurse? primary = FindDebtCards(player).FirstOrDefault();
            if (primary is null)
                return false;

            int goldBefore = player.Gold;
            (int expectedGold, int nextDebt) = LoanMath.ApplyRepayment(goldBefore, debt, amount);
            SetDebt(primary, nextDebt);
            try
            {
                await PlayerCmd.LoseGold(amount, player, GoldLossType.Spent);
                if (player.Gold != expectedGold)
                    throw new InvalidOperationException("Vanilla gold deduction did not produce the validated repayment balance.");
                RunManager.Instance.RewardSynchronizer.SyncLocalGoldLost(amount);
            }
            catch
            {
                player.Gold = goldBefore;
                SetDebt(primary, debt);
                throw;
            }

            if (nextDebt == 0)
                await RemoveDebtCardsAsync(FindDebtCards(player));
            else
                await RemoveDuplicatesAsync(FindDebtCards(player).Skip(1));

            DebtChanged?.Invoke(player, nextDebt);
            return true;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Restores invariants after deserialization. If a damaged save contains duplicates, the
    /// largest non-negative saved value wins; values are never summed.
    /// </summary>
    public static void ReconcileLoadedPlayer(Player player)
    {
        List<DebtCurse> cards = FindDebtCards(player);
        if (cards.Count == 0)
            return;

        int debt = DebtLoadMath.GetCanonicalDebt(cards.Select(GetDebt));
        if (debt <= 0)
        {
            RemoveDebtCardsImmediately(cards);
            return;
        }

        SetDebt(cards[0], debt);
        RemoveDebtCardsImmediately(cards.Skip(1));
    }

    private static List<DebtCurse> FindDebtCards(Player player) =>
        player.Deck.Cards.OfType<DebtCurse>().ToList();

    private static void SetDebt(DebtCurse card, int amount) =>
        DebtCurse.OutstandingDebt.Set(card, Math.Max(0, amount));

    private static async Task RemoveDuplicatesAsync(IEnumerable<DebtCurse> cards) =>
        await RemoveDebtCardsAsync(cards.ToList());

    private static async Task RemoveDebtCardsAsync(IReadOnlyList<DebtCurse> cards)
    {
        if (cards.Count == 0)
            return;

        using (DebtRemovalAuthorization.Begin())
            await CardPileCmd.RemoveFromDeck(cards, false);
    }

    private static void RemoveDebtCardsImmediately(IEnumerable<DebtCurse> cards)
    {
        using (DebtRemovalAuthorization.Begin())
        {
            foreach (DebtCurse card in cards.ToList())
                card.RemoveFromState();
        }
    }
}

public static class DebtLoadMath
{
    public static int GetCanonicalDebt(IEnumerable<int> savedValues) =>
        savedValues.Select(value => Math.Max(0, value)).DefaultIfEmpty(0).Max();
}

internal static class DebtRemovalAuthorization
{
    private static readonly AsyncLocal<int> Depth = new();

    internal static bool IsAuthorized => Depth.Value > 0;

    internal static IDisposable Begin()
    {
        Depth.Value++;
        return new Scope();
    }

    private sealed class Scope : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            Depth.Value = Math.Max(0, Depth.Value - 1);
        }
    }
}
