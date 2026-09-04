using HarmonyLib;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using SpireEconomy.SpireEconomyCode.BlackMarket;
using SpireEconomy.SpireEconomyCode.Configuration;

namespace SpireEconomy.SpireEconomyCode.Patches;

[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyNextEvent))]
internal static class BlackMarketEventSelectionPatch
{
    private static void Postfix(IRunState runState, ref EventModel __result)
    {
        int chance = Math.Clamp(EconomyConfig.BlackMarketEventWeightPercent, 0, 100);
        if (chance <= 0 || runState is not RunState state)
            return;

        BlackMarketEvent blackMarket = ModelDb.Event<BlackMarketEvent>();
        bool alreadyVisited = state.VisitedEventIds.Contains(blackMarket.Id);
        bool alreadySelected = __result.Id == blackMarket.Id;
        if (alreadyVisited || alreadySelected)
            return;

        int roll = runState.Rng.UnknownMapPoint.NextInt(100);
        if (BlackMarketEventSelection.ShouldReplace(
                roll,
                chance,
                alreadyVisited,
                alreadySelected))
            __result = blackMarket;
    }
}

public static class BlackMarketEventSelection
{
    public static bool ShouldReplace(int roll, int chancePercent, bool alreadyVisited, bool alreadySelected)
    {
        int chance = Math.Clamp(chancePercent, 0, 100);
        return !alreadyVisited && !alreadySelected && roll >= 0 && roll < chance;
    }
}
