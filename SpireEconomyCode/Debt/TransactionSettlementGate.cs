namespace SpireEconomy.SpireEconomyCode.Debt;

/// <summary>Last-line protection against committing the same asynchronous callback twice.</summary>
public sealed class TransactionSettlementGate
{
    private bool _started;
    private readonly object _gate = new();

    public bool TryBegin()
    {
        lock (_gate)
        {
            if (_started)
                return false;
            _started = true;
            return true;
        }
    }
}
