using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using SpireEconomy.SpireEconomyCode.BlackMarket;
using MegaCrit.Sts2.addons.mega_text;

namespace SpireEconomy.SpireEconomyCode.Patches;

[HarmonyPatch(typeof(NRunMusicController), nameof(NRunMusicController.ToggleMerchantTrack))]
internal static class BlackMarketMerchantMusicPatch
{
    private static bool Prefix(IRunState ____runState)
    {
        // The Black Market deliberately renders the native merchant scene inside an EventRoom.
        // The scene's normal map-close signal asks the music controller to switch merchant music,
        // but vanilla rejects that request unless CurrentRoom.RoomType is Merchant. Let the event
        // keep its current music so that this cosmetic transition cannot abort input initialization.
        return ____runState.CurrentRoom is not EventRoom { LocalMutableEvent: BlackMarketEvent };
    }
}

[HarmonyPatch(typeof(EventModel), "get_LayoutScenePath")]
internal static class BlackMarketNativeScenePatch
{
    private const string MerchantScene = "res://scenes/rooms/merchant_room.tscn";

    private static bool Prefix(EventModel __instance, ref string __result)
    {
        if (__instance is not BlackMarketEvent)
            return true;
        __result = MerchantScene;
        return false;
    }
}

[HarmonyPatch(typeof(EventModel), nameof(EventModel.SetNode))]
internal static class BlackMarketMerchantNodePatch
{
    private static readonly ConditionalWeakTable<NMerchantRoom, BlackMarketEvent> Markets = new();
    private static readonly FieldInfo EventNodeField = AccessTools.Field(typeof(EventModel), "<Node>k__BackingField");
    private static readonly FieldInfo RoomInventoriesField = AccessTools.Field(typeof(MerchantRoom), "<Inventories>k__BackingField");
    private static readonly FieldInfo RoomRunStateField = AccessTools.Field(typeof(MerchantRoom), "_runState");
    private static readonly FieldInfo RoomDialogueField = AccessTools.Field(typeof(MerchantRoom), "_dialogue");
    private static readonly FieldInfo NodeRoomField = AccessTools.Field(typeof(NMerchantRoom), "<Room>k__BackingField");
    private static readonly FieldInfo PlayersField = AccessTools.Field(typeof(NMerchantRoom), "_players");
    private static readonly FieldInfo NodeDialogueField = AccessTools.Field(typeof(NMerchantRoom), "_dialogue");

    private static bool Prefix(EventModel __instance, Control node)
    {
        if (__instance is not BlackMarketEvent market || node is not NMerchantRoom merchantNode)
            return true;

        Player owner = market.Owner ?? throw new InvalidOperationException("Black Market owner is not initialized.");
        MerchantInventory inventory = market.MerchantInventory ??
            throw new InvalidOperationException("Black Market inventory is not initialized.");
        MerchantDialogueSet dialogue = MerchantRoom.Dialogue;
        MerchantRoom room = new();
        RoomInventoriesField.SetValue(room,
            Enumerable.Repeat(inventory, owner.RunState.Players.Count).ToList());
        RoomRunStateField.SetValue(room, owner.RunState);
        RoomDialogueField.SetValue(room, dialogue);

        NodeRoomField.SetValue(merchantNode, room);
        ((List<Player>)PlayersField.GetValue(merchantNode)!).AddRange(owner.RunState.Players);
        NodeDialogueField.SetValue(merchantNode, dialogue);
        EventNodeField.SetValue(market, merchantNode);

        Markets.Remove(merchantNode);
        Markets.Add(merchantNode, market);
        return false;
    }

    internal static bool TryGetMarket(NMerchantRoom node, out BlackMarketEvent market) =>
        Markets.TryGetValue(node, out market!);
}

[HarmonyPatch(typeof(NMerchantRoom), "_Ready")]
internal static class BlackMarketMerchantReadyPatch
{
    private static void Postfix(NMerchantRoom __instance) =>
        BlackMarketMerchantInteractivity.Refresh(__instance);
}

[HarmonyPatch(typeof(NMerchantRoom), "OnActiveScreenUpdated")]
internal static class BlackMarketMerchantScreenContextPatch
{
    private static void Postfix(NMerchantRoom __instance) =>
        BlackMarketMerchantInteractivity.Refresh(__instance);
}

internal static class BlackMarketMerchantInteractivity
{
    internal static void Refresh(NMerchantRoom node)
    {
        if (!BlackMarketMerchantNodePatch.TryGetMarket(node, out _)
            || node.MerchantButton is null || node.ProceedButton is null)
            return;

        // Native merchant rooms are registered as the active screen context. Our merchant scene
        // is hosted by an EventRoom, so vanilla's context check disables both controls. Restore
        // the same visible-room behavior without leaving controls active behind the inventory/map.
        NMapScreen? mapScreen = NMapScreen.Instance;
        bool roomCanReceiveInput = node.IsVisibleInTree()
            && node.Inventory is not { IsOpen: true }
            && (mapScreen is null || !mapScreen.IsVisible());

        if (roomCanReceiveInput)
        {
            node.MerchantButton.Enable();
            node.ProceedButton.Enable();
        }
        else
        {
            node.MerchantButton.Disable();
            node.ProceedButton.Disable();
        }
    }
}

[HarmonyPatch(typeof(NMerchantRoom), "OnMerchantOpened")]
internal static class BlackMarketMerchantOpenedPatch
{
    private static readonly ConditionalWeakTable<NMerchantRoom, object> ParasolApplied = new();
    private static readonly object Marker = new();

    private static void Postfix(NMerchantRoom __instance)
    {
        if (!BlackMarketMerchantNodePatch.TryGetMarket(__instance, out BlackMarketEvent market))
            return;

        // Follow the native interaction order: enter the room first, then open inventory only when
        // the player activates the merchant. Lord's Parasol is applied once on that first opening.
        if (ParasolApplied.TryGetValue(__instance, out _))
            return;
        ParasolApplied.Add(__instance, Marker);
        _ = ObserveParasolTask(BlackMarketNativePresentation.ApplyLordsParasol(market));
    }

    private static async Task ObserveParasolTask(Task task)
    {
        try { await task; }
        catch (Exception exception) { MainFile.Logger.Error($"Lord's Parasol failed in Black Market: {exception}"); }
    }
}

[HarmonyPatch(typeof(NMerchantRoom), "HideScreen")]
internal static class BlackMarketNativeExitPatch
{
    private static void Prefix(NMerchantRoom __instance)
    {
        if (BlackMarketMerchantNodePatch.TryGetMarket(__instance, out BlackMarketEvent market))
            market.FinishMarket();
    }
}

[HarmonyPatch(typeof(NMerchantInventory), nameof(NMerchantInventory.Open))]
internal static class BlackMarketInventoryBackButtonOpenPatch
{
    private static void Postfix(NMerchantInventory __instance) =>
        BlackMarketInventoryBackButton.EnsureAvailable(__instance);
}

[HarmonyPatch(typeof(NMerchantInventory), "OnActiveScreenUpdated")]
internal static class BlackMarketInventoryBackButtonScreenPatch
{
    private static void Postfix(NMerchantInventory __instance)
    {
        if (__instance.IsOpen)
            BlackMarketInventoryBackButton.EnsureAvailable(__instance);
    }
}

internal static class BlackMarketInventoryBackButton
{
    private static readonly FieldInfo BackButtonField =
        AccessTools.Field(typeof(NMerchantInventory), "_backButton");

    internal static void EnsureAvailable(NMerchantInventory inventoryNode)
    {
        if (inventoryNode.Inventory is not { } inventory ||
            !BlackMarketMerchantRegistry.Contains(inventory) ||
            BackButtonField.GetValue(inventoryNode) is not MegaCrit.Sts2.Core.Nodes.CommonUi.NBackButton backButton)
            return;

        // The EventRoom host is not considered the active vanilla merchant screen. Vanilla then
        // disables its authored Back button; restore that same button and its native Close action.
        backButton.Show();
        backButton.MouseFilter = Control.MouseFilterEnum.Stop;
        backButton.Enable();
    }
}

[HarmonyPatch(typeof(NMerchantInventory), "GetClosestStockedSlot")]
internal static class PartialMerchantInventoryNavigationPatch
{
    private static bool Prefix(int idx, List<NMerchantSlot> row, ref Control? __result)
    {
        // Vanilla merchants fill every authored slot. Black Market intentionally leaves unused
        // card/potion/relic slots empty, so the vanilla helper would dereference a null Entry.
        if (row.Count > 0 && row.All(slot => slot.Entry is not null))
            return true;

        int target = Math.Clamp(idx, 0, Math.Max(0, row.Count - 1));
        __result = row
            .Select((slot, index) => new { Slot = slot, Index = index })
            .Where(candidate => candidate.Slot.Entry is { IsStocked: true })
            .OrderBy(candidate => Math.Abs(candidate.Index - target))
            .ThenBy(candidate => candidate.Index)
            .Select(candidate => (Control)candidate.Slot)
            .FirstOrDefault();
        return false;
    }
}

[HarmonyPatch(typeof(NMerchantInventory), nameof(NMerchantInventory.Initialize))]
internal static class BlackMarketUnusedSlotVisibilityPatch
{
    private static void Postfix(NMerchantInventory __instance, MerchantInventory inventory)
    {
        if (!BlackMarketMerchantRegistry.Contains(inventory))
            return;

        // The native rug authors enough nodes for a full merchant. Initialize() only fills as
        // many children as the supplied inventory contains, leaving the remaining scene defaults
        // (a 99 cost label and sale tag) visible. Hide only never-bound slots; stocked/replenished
        // Black Market entries keep the complete vanilla slot behavior.
        foreach (NMerchantSlot slot in __instance.GetAllSlots())
        {
            if (slot.Entry is null)
            {
                slot.Visible = false;
                slot.MouseFilter = Control.MouseFilterEnum.Ignore;
            }
        }
    }
}

[HarmonyPatch(typeof(NMerchantRelic), "UpdateVisual")]
internal static class BlackMarketAncientRelicPriceLabelPatch
{
    private static readonly FieldInfo CostLabelField =
        AccessTools.Field(typeof(NMerchantSlot), "_costLabel");
    private static readonly ConditionalWeakTable<NMerchantRelic, MegaLabel> HpCostLabels = new();

    private static void Postfix(NMerchantRelic __instance)
    {
        if (__instance.Entry is not MerchantRelicEntry entry ||
            !BlackMarketAncientRelicCost.TryGet(entry, out int maxHpCost) ||
            CostLabelField.GetValue(__instance) is not MegaLabel label)
            return;

        MegaLabel hpLabel = HpCostLabels.GetValue(__instance, _ => CreateHpCostLabel(label));
        LocString text = new("gameplay_ui", "SPIREECONOMY-BLACK_MARKET_ANCIENT_PRICE");
        text.Add("MaxHp", maxHpCost);
        hpLabel.Text = text.GetFormattedText();
        PositionHpCostLabel(label, hpLabel);
        hpLabel.Visible = entry.IsStocked;
    }

    private static MegaLabel CreateHpCostLabel(MegaLabel goldLabel)
    {
        MegaLabel hpLabel = (MegaLabel)goldLabel.Duplicate();
        hpLabel.Name = "SpireEconomyAncientHpCost";
        hpLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
        hpLabel.Modulate = new Color("e24a4a");
        hpLabel.Scale = Vector2.One;
        hpLabel.AutoSizeEnabled = false;
        hpLabel.AddThemeFontSizeOverride(
            "font_size",
            Math.Max(16, (int)MathF.Round(goldLabel.GetThemeFontSize("font_size") * 0.75f)));
        hpLabel.HorizontalAlignment = HorizontalAlignment.Center;
        hpLabel.VerticalAlignment = VerticalAlignment.Center;
        hpLabel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
        hpLabel.Size = new Vector2(180f, Math.Max(36f, goldLabel.Size.Y));

        // Make this a child of the gold number itself. The merchant rug and its containers finish
        // layout after UpdateVisual and also move during the opening animation; parenting here
        // makes the HP cost follow those changes instead of keeping a stale global position.
        goldLabel.AddChild(hpLabel);
        PositionHpCostLabel(goldLabel, hpLabel);
        return hpLabel;
    }

    private static void PositionHpCostLabel(MegaLabel goldLabel, MegaLabel hpLabel)
    {
        hpLabel.Position = new Vector2(
            (goldLabel.Size.X - hpLabel.Size.X) * 0.5f,
            goldLabel.Size.Y + 2f);
    }
}

[HarmonyPatch(typeof(MerchantEntry), "get_Cost")]
internal static class BlackMarketMerchantDiscountPatch
{
    private static void Postfix(MerchantEntry __instance, Player ____player, ref int __result)
    {
        if (!BlackMarketMerchantRegistry.Contains(__instance) || ____player.RunState.CurrentRoom is MerchantRoom)
            return;
        __result = decimal.ToInt32(Hook.ModifyMerchantPrice(
            ____player.RunState, ____player, __instance, __result));
    }
}

[HarmonyPatch]
internal static class BlackMarketMerchantBasePricePatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.DeclaredMethod(typeof(MerchantCardEntry), nameof(MerchantCardEntry.CalcCost));
        yield return AccessTools.DeclaredMethod(typeof(MerchantRelicEntry), nameof(MerchantRelicEntry.CalcCost));
        yield return AccessTools.DeclaredMethod(typeof(MerchantCardRemovalEntry), nameof(MerchantCardRemovalEntry.CalcCost));
    }

    private static void Postfix(MerchantEntry __instance) =>
        BlackMarketMerchantRegistry.ApplyBlackMarketBasePrice(__instance);
}
