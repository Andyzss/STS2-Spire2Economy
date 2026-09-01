using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.sts2.Core.Nodes.TopBar;
using SpireEconomy.SpireEconomyCode.UI;

namespace SpireEconomy.SpireEconomyCode.Patches;

[HarmonyPatch(typeof(NTopBarGold), nameof(NTopBarGold.Initialize))]
internal static class DebtHudInitializePatch
{
    private static void Postfix(NTopBarGold __instance, Player player) =>
        DebtHud.Attach(__instance, player);
}
