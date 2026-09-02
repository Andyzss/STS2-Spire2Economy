using Godot;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.HoverTips;

namespace SpireEconomy.SpireEconomyCode.UI;

internal enum OfficialHoverTipPlacement
{
    AdaptiveSide,
    TopBarBelow
}

/// <summary>
/// Connects a mod control to the game's native hover-tip presentation for both pointer hover
/// and keyboard/controller focus. The owner remains responsible for its normal input behavior.
/// </summary>
internal sealed class OfficialHoverTipBinding
{
    private readonly Control _owner;
    private readonly HoverTip _tip;
    private readonly OfficialHoverTipPlacement _placement;
    private NHoverTipSet? _activeSet;
    private bool _shown;

    internal OfficialHoverTipBinding(
        Control owner,
        string titleKey,
        string descriptionKey,
        OfficialHoverTipPlacement placement = OfficialHoverTipPlacement.AdaptiveSide)
    {
        _owner = owner;
        _placement = placement;
        _tip = new HoverTip(
            new LocString("gameplay_ui", titleKey),
            new LocString("gameplay_ui", descriptionKey));

        owner.MouseEntered += Show;
        owner.MouseExited += Hide;
        owner.FocusEntered += Show;
        owner.FocusExited += Hide;
        owner.TreeExiting += Dispose;
    }

    private void Show()
    {
        if (_shown || !_owner.Visible)
            return;

        _shown = true;
        if (_placement == OfficialHoverTipPlacement.TopBarBelow)
        {
            // Match NTopBarHp.OnFocus and NTopBarGold.OnFocus exactly: no directional
            // alignment, then place the tip immediately below its owning HUD control.
            _activeSet = NHoverTipSet.CreateAndShow(_owner, _tip, HoverTipAlignment.None);
            _activeSet?.SetGlobalPosition(
                _owner.GlobalPosition + new Vector2(0f, _owner.Size.Y + 20f),
                false);
            return;
        }

        Rect2 visible = _owner.GetViewport().GetVisibleRect();
        float ownerCenter = _owner.GetGlobalRect().GetCenter().X;
        HoverTipAlignment alignment = ownerCenter < visible.GetCenter().X
            ? HoverTipAlignment.Right
            : HoverTipAlignment.Left;
        _activeSet = NHoverTipSet.CreateAndShow(_owner, _tip, alignment);
        _activeSet?.SetFollowOwner();
    }

    private void Hide()
    {
        if (!_shown)
            return;

        _shown = false;
        NHoverTipSet.Remove(_owner);
        _activeSet = null;
    }

    private void Dispose()
    {
        Hide();
        _owner.MouseEntered -= Show;
        _owner.MouseExited -= Hide;
        _owner.FocusEntered -= Show;
        _owner.FocusExited -= Hide;
        _owner.TreeExiting -= Dispose;
    }
}
