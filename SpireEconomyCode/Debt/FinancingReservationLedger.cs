namespace SpireEconomy.SpireEconomyCode.Debt;

/// <summary>Single-settlement reservation gate keyed by the vanilla stock entry instance.</summary>
internal sealed class FinancingReservationLedger<TKey, TReservation> where TKey : notnull
{
    private readonly Dictionary<TKey, TReservation> _entries = new();
    private readonly object _gate = new();

    internal IReadOnlyCollection<TReservation> SnapshotValues
    {
        get
        {
            lock (_gate)
                return _entries.Values.ToArray();
        }
    }

    internal bool Contains(TKey key)
    {
        lock (_gate)
            return _entries.ContainsKey(key);
    }

    internal bool TryReserve(TKey key, TReservation reservation)
    {
        lock (_gate)
            return _entries.TryAdd(key, reservation);
    }

    internal bool TryGet(TKey key, out TReservation reservation)
    {
        lock (_gate)
            return _entries.TryGetValue(key, out reservation!);
    }

    internal bool Release(TKey key)
    {
        lock (_gate)
            return _entries.Remove(key);
    }
}
