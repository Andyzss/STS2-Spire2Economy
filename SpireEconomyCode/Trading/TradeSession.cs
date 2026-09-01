namespace SpireEconomy.SpireEconomyCode.Trading;

internal enum TradeSessionState
{
    Draft,
    AwaitingConfirmations,
    Committed,
    Cancelled,
    Rejected
}

/// <summary>
/// Host-authoritative state machine for one proposed campfire trade.
/// Network serialization and mutation logic are deliberately deferred.
/// </summary>
internal sealed class TradeSession
{
    public TradeSessionState State { get; private set; } = TradeSessionState.Draft;
}
