using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using SpireEconomy.SpireEconomyCode.Debt;

namespace SpireEconomy.SpireEconomyCode.Patches;

[HarmonyPatch(typeof(CardModel), "get_IsRemovable")]
internal static class DebtCurseRemovablePatch
{
    private static void Postfix(CardModel __instance, ref bool __result)
    {
        if (__instance is DebtCurse)
            __result = false;
    }
}

[HarmonyPatch(typeof(CardModel), "get_IsTransformable")]
internal static class DebtCurseTransformablePatch
{
    private static void Postfix(CardModel __instance, ref bool __result)
    {
        if (__instance is DebtCurse)
            __result = false;
    }
}

[HarmonyPatch(typeof(CardModel), nameof(CardModel.RemoveFromState))]
internal static class DebtCurseDirectRemovalPatch
{
    private static bool Prefix(CardModel __instance) =>
        __instance is not DebtCurse ||
        DebtRemovalAuthorization.IsAuthorized ||
        RunManager.Instance?.IsCleaningUp == true;
}

[HarmonyPatch(typeof(CardPileCmd), nameof(CardPileCmd.RemoveFromDeck), typeof(CardModel), typeof(bool))]
internal static class DebtCurseSingleDeckRemovalPatch
{
    private static bool Prefix(CardModel __0, ref Task __result)
    {
        if (__0 is not DebtCurse || DebtRemovalAuthorization.IsAuthorized)
            return true;

        __result = Task.CompletedTask;
        return false;
    }
}

[HarmonyPatch(typeof(CardPileCmd), nameof(CardPileCmd.RemoveFromDeck), typeof(IReadOnlyList<CardModel>), typeof(bool))]
internal static class DebtCurseBatchDeckRemovalPatch
{
    private static bool Prefix(ref IReadOnlyList<CardModel> __0, ref Task __result)
    {
        if (DebtRemovalAuthorization.IsAuthorized)
            return true;

        CardModel[] filtered = __0.Where(card => card is not DebtCurse).ToArray();
        if (filtered.Length == 0)
        {
            __result = Task.CompletedTask;
            return false;
        }

        __0 = filtered;
        return true;
    }
}
