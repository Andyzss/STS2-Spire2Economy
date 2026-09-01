using BaseLib.Config;

namespace SpireEconomy.SpireEconomyCode.Configuration;

/// <summary>
/// Central balance configuration. Gameplay code must consume these values instead of duplicating constants.
/// </summary>
internal sealed class EconomyConfig : SimpleModConfig
{
    [ConfigSection("Debt")]
    [ConfigSlider(0, 2000, 25)]
    public static int MaxDebt { get; set; } = 300;

    [ConfigSection("BlackMarket")]
    [ConfigSlider(100, 300, 5, Format = "{0}%")]
    public static int BlackMarketPricePercent { get; set; } = 150;

    [ConfigSlider(0, 100, 5, Format = "{0}%")]
    public static int RelicSalePricePercent { get; set; } = 50;

    [ConfigSlider(0, 100, 1, Format = "{0}%")]
    public static int BlackMarketEventWeightPercent { get; set; } = 3;
}
