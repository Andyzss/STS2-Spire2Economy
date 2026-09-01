using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using SpireEconomy.SpireEconomyCode.UI;

namespace SpireEconomy.SpireEconomyCode.Patches;

[HarmonyPatch(typeof(NMerchantInventory), nameof(NMerchantInventory.Initialize))]
internal static class MerchantRepaymentInitializePatch
{
    private static void Postfix(NMerchantInventory __instance, MerchantInventory inventory)
        => DebtRepaymentUi.Attach(__instance, inventory.Player);
}

[HarmonyPatch(typeof(NMerchantInventory), nameof(NMerchantInventory.Open))]
internal static class MerchantRepaymentOpenPatch
{
    private static void Postfix(NMerchantInventory __instance) =>
        DebtRepaymentUi.Refresh(__instance);
}

[HarmonyPatch(typeof(NMerchantInventory), "DoOpenAnimation")]
internal static class MerchantRepaymentOpenAnimationPatch
{
    private static void Postfix(NMerchantInventory __instance, ref Task __result) =>
        __result = DebtRepaymentUi.RefreshAfterOpenAnimationAsync(__instance, __result);
}

[HarmonyPatch(typeof(NMerchantInventory), "UpdateNavigation")]
internal static class MerchantRepaymentNavigationPatch
{
    private static void Postfix(NMerchantInventory __instance) =>
        DebtRepaymentUi.RefreshNavigation(__instance);
}

[HarmonyPatch(typeof(NMerchantInventory), "OnPurchaseCompleted")]
internal static class MerchantRepaymentPurchaseCompletedPatch
{
    private static void Postfix(NMerchantInventory __instance) =>
        DebtRepaymentUi.Refresh(__instance);
}
