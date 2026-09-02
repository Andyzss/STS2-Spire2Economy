using System.Runtime.CompilerServices;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.sts2.Core.Nodes.TopBar;
using SpireEconomy.SpireEconomyCode.Debt;

namespace SpireEconomy.SpireEconomyCode.UI;

/// <summary>
/// Adds a non-layout-participating debt readout beside the vanilla gold control.
/// It is an overlaid child, so it does not resize or shift any top-bar control.
/// </summary>
internal static class DebtHud
{
    private static readonly ConditionalWeakTable<NTopBarGold, State> States = new();
    private static readonly System.Reflection.FieldInfo? GoldLabelField =
        typeof(NTopBarGold).GetField("_goldLabel",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

    internal static void Attach(NTopBarGold goldControl, Player player)
    {
        if (States.TryGetValue(goldControl, out _))
            return;

        State state = new(goldControl, player);
        States.Add(goldControl, state);
    }

    private sealed class State
    {
        private readonly Player _player;
        private readonly Label _label;
        private readonly OfficialHoverTipBinding _hoverTip;

        internal State(NTopBarGold goldControl, Player player)
        {
            _player = player;
            Label? goldLabel = GoldLabelField?.GetValue(goldControl) as Label;
            _label = goldLabel?.Duplicate() as Label ?? new Label();
            _label.Name = "SpireEconomyDebtHud";
            _label.MouseFilter = Control.MouseFilterEnum.Stop;
            _label.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
            _label.OffsetLeft = 96;
            _label.OffsetTop = 0;
            _label.OffsetRight = 320;
            _label.OffsetBottom = 56;
            _label.AddThemeColorOverride("font_color", new Color("dc665f"));
            goldControl.AddChild(_label);
            _hoverTip = new OfficialHoverTipBinding(
                _label,
                "SPIREECONOMY-DEBT_HOVER.title",
                "SPIREECONOMY-DEBT_HOVER.description",
                OfficialHoverTipPlacement.TopBarBelow);

            DebtManager.DebtChanged += OnDebtChanged;
            goldControl.TreeExiting += OnTreeExiting;
            Refresh();
        }

        private void Refresh()
        {
            int debt = DebtManager.GetDebt(_player);
            _label.Visible = debt > 0;

            LocString text = new("gameplay_ui", "SPIREECONOMY-DEBT_HUD");
            text.Add("Debt", debt);
            _label.Text = text.GetFormattedText();
        }

        private void OnDebtChanged(Player player, int _)
        {
            if (ReferenceEquals(player, _player))
                Refresh();
        }

        private void OnTreeExiting() => DebtManager.DebtChanged -= OnDebtChanged;
    }
}
