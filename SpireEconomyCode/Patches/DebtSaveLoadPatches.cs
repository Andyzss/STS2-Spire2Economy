using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Saves.Runs;
using SpireEconomy.SpireEconomyCode.Debt;

namespace SpireEconomy.SpireEconomyCode.Patches;

[HarmonyPatch(typeof(Player), nameof(Player.FromSerializable), typeof(SerializablePlayer))]
internal static class DebtPlayerLoadPatch
{
    private static void Postfix(Player __result) => DebtManager.ReconcileLoadedPlayer(__result);
}

[HarmonyPatch(typeof(Player), nameof(Player.SyncWithSerializedPlayer), typeof(SerializablePlayer))]
internal static class DebtPlayerResyncPatch
{
    private static void Postfix(Player __instance) => DebtManager.ReconcileLoadedPlayer(__instance);
}
