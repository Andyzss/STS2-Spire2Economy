using System.Runtime.CompilerServices;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using MegaCrit.Sts2.addons.mega_text;
using SpireEconomy.SpireEconomyCode.Debt;

namespace SpireEconomy.SpireEconomyCode.UI;

/// <summary>
/// Adds a compact repayment button above the vanilla card-removal service. Amount selection
/// happens inside the game's modal popup so mouse, keyboard and controller users share the
/// same focus-managed flow and no persistent panel can extend beyond the shop viewport.
/// </summary>
internal static class DebtRepaymentUi
{
    private static readonly ConditionalWeakTable<NMerchantInventory, State> States = new();

    internal static void Attach(NMerchantInventory inventory, Player player)
    {
        if (States.TryGetValue(inventory, out _))
            return;

        States.Add(inventory, new State(inventory, player));
    }

    internal static void Refresh(NMerchantInventory inventory)
    {
        if (States.TryGetValue(inventory, out State? state))
            state.Refresh();
    }

    internal static void RefreshNavigation(NMerchantInventory inventory)
    {
        if (States.TryGetValue(inventory, out State? state))
            state.ApplyNavigation();
    }

    internal static async Task RefreshAfterOpenAnimationAsync(
        NMerchantInventory inventory,
        Task openAnimation)
    {
        await openAnimation;
        Refresh(inventory);
        RefreshNavigation(inventory);
    }

    private sealed class State
    {
        private const float ButtonGap = 10f;
        private const float ViewportMargin = 24f;
        private const float MaximumButtonWidth = 210f;
        private const float MaximumButtonHeight = 58f;
        private static readonly System.Reflection.FieldInfo? RemovalVisualField =
            typeof(NMerchantCardRemoval).GetField("_removalVisual",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        private readonly Player _player;
        private readonly NMerchantCardRemoval? _cardRemoval;
        private readonly Control _shopParent;
        private readonly NPopupYesNoButton _openButton;
        private readonly OfficialHoverTipBinding _hoverTip;
        private NodePath? _originalRemovalTop;
        private bool _busy;

        internal State(NMerchantInventory inventory, Player player)
        {
            _player = player;
            _cardRemoval = inventory.GetNodeOrNull<NMerchantCardRemoval>("%MerchantCardRemoval");
            _shopParent = _cardRemoval?.GetParent<Control>()
                ?? inventory.GetNode<Control>("%SlotsContainer");
            _openButton = CreateOfficialButton();

            _openButton.Name = "SpireEconomyRepaymentButton";
            _openButton.IsYes = true;
            _openButton.FocusMode = Control.FocusModeEnum.All;
            _openButton.Released += OnOpenButtonReleased;
            _shopParent.AddChild(_openButton);
            _openButton.SetText(Localize("SPIREECONOMY-REPAY_DEBT"));
            // Popup buttons register a global confirm/cancel hotkey by default. This shop action
            // must activate only through pointer release or when normal focus navigation selects it.
            _openButton.DisconnectHotkeys();
            _hoverTip = new OfficialHoverTipBinding(
                _openButton,
                "SPIREECONOMY-REPAY_HOVER.title",
                "SPIREECONOMY-REPAY_HOVER.description");

            if (_cardRemoval is not null)
            {
                _originalRemovalTop = _cardRemoval.FocusNeighborTop;
                _cardRemoval.Resized += QueuePositionUpdate;
            }

            _shopParent.Resized += QueuePositionUpdate;
            _openButton.Resized += QueuePositionUpdate;
            DebtManager.DebtChanged += OnDebtChanged;
            _openButton.TreeExiting += OnTreeExiting;
            Refresh();
            QueuePositionUpdate();
        }

        internal void Refresh()
        {
            int debt = DebtManager.GetDebt(_player);
            bool hasDebt = debt > 0;
            bool canRepay = !_busy && hasDebt && _player.Gold > 0;

            // The merchant service is always present. Debt/gold only control availability;
            // this avoids a late save-state synchronization making the entry disappear.
            _openButton.Visible = true;
            _openButton.SetEnabled(canRepay);
            _openButton.FocusMode = canRepay
                ? Control.FocusModeEnum.All
                : Control.FocusModeEnum.None;
            ApplyNavigation();
            QueuePositionUpdate();
        }

        internal void ApplyNavigation()
        {
            if (_cardRemoval is null)
                return;

            if (!_openButton.Visible || !_openButton.IsEnabled)
            {
                if (_originalRemovalTop is not null)
                    _cardRemoval.FocusNeighborTop = _originalRemovalTop;
                return;
            }

            NodePath removalPath = _cardRemoval.GetPath();
            NodePath repaymentPath = _openButton.GetPath();
            _cardRemoval.FocusNeighborTop = repaymentPath;
            _openButton.FocusNeighborBottom = removalPath;
            _openButton.FocusNeighborTop = repaymentPath;
            _openButton.FocusNeighborLeft = repaymentPath;
            _openButton.FocusNeighborRight = repaymentPath;
        }

        private static NPopupYesNoButton CreateOfficialButton()
        {
            NGenericPopup? templatePopup = NGenericPopup.Create();
            if (templatePopup is null)
                throw new InvalidOperationException("Could not instantiate the game's generic popup button template.");

            NPopupYesNoButton? template = FindDescendant<NPopupYesNoButton>(templatePopup);
            NPopupYesNoButton? duplicate = template?.Duplicate() as NPopupYesNoButton;
            templatePopup.Free();
            return duplicate ?? throw new InvalidOperationException(
                "The game's generic popup no longer contains an NPopupYesNoButton template.");
        }

        private static T? FindDescendant<T>(Node parent) where T : Node
        {
            foreach (Node child in parent.GetChildren())
            {
                if (child is T match)
                    return match;

                T? descendant = FindDescendant<T>(child);
                if (descendant is not null)
                    return descendant;
            }

            return null;
        }

        private void QueuePositionUpdate()
        {
            if (_openButton.IsInsideTree())
                Callable.From(PositionAboveCardRemoval).CallDeferred();
        }

        private void PositionAboveCardRemoval()
        {
            if (_cardRemoval is null || !_openButton.IsInsideTree())
                return;

            Rect2 visible = _openButton.GetViewport().GetVisibleRect();
            Vector2 nativeSize = _openButton.Size;
            float scale = Math.Min(1f, Math.Min(
                MaximumButtonWidth / Math.Max(1f, nativeSize.X),
                MaximumButtonHeight / Math.Max(1f, nativeSize.Y)));
            _openButton.Scale = Vector2.One * scale;
            Vector2 buttonSize = nativeSize * scale;
            Rect2 removalBounds = GetRemovalVisualBounds();
            float centeredX = removalBounds.GetCenter().X - buttonSize.X / 2f;
            float maximumX = Math.Max(visible.Position.X + ViewportMargin,
                visible.End.X - buttonSize.X - ViewportMargin);
            float maximumY = Math.Max(visible.Position.Y + ViewportMargin,
                visible.End.Y - buttonSize.Y - ViewportMargin);

            _openButton.GlobalPosition = new Vector2(
                Mathf.Clamp(centeredX, visible.Position.X + ViewportMargin, maximumX),
                Mathf.Clamp(removalBounds.Position.Y - buttonSize.Y - ButtonGap,
                    visible.Position.Y + ViewportMargin, maximumY));
        }

        private Rect2 GetRemovalVisualBounds()
        {
            NMerchantCardRemoval? cardRemoval = _cardRemoval;
            if (cardRemoval is null)
                return new Rect2();

            if (RemovalVisualField?.GetValue(cardRemoval) is Sprite2D visual &&
                visual.IsInsideTree())
            {
                Rect2 local = visual.GetRect();
                Vector2[] corners =
                [
                    visual.ToGlobal(local.Position),
                    visual.ToGlobal(new Vector2(local.End.X, local.Position.Y)),
                    visual.ToGlobal(local.End),
                    visual.ToGlobal(new Vector2(local.Position.X, local.End.Y))
                ];
                float left = corners.Min(point => point.X);
                float top = corners.Min(point => point.Y);
                float right = corners.Max(point => point.X);
                float bottom = corners.Max(point => point.Y);
                return new Rect2(left, top, right - left, bottom - top);
            }

            // Conservative fallback for a future game version where the visual field changes.
            return new Rect2(cardRemoval.GlobalPosition, cardRemoval.Size);
        }

        private async void OnOpenPressed()
        {
            if (_busy)
                return;

            int debt = DebtManager.GetDebt(_player);
            int maximum = Math.Min(Math.Max(0, _player.Gold), debt);
            if (maximum <= 0 || NModalContainer.Instance is null)
                return;

            NGenericPopup? popup = NGenericPopup.Create();
            if (popup is null)
                return;

            _busy = true;
            Refresh();
            try
            {
                NModalContainer.Instance.Add(popup);
                Task<bool> confirmation = popup.WaitForConfirmation(
                    body: new LocString("gameplay_ui", "SPIREECONOMY-REPAY_PROMPT"),
                    header: new LocString("gameplay_ui", "SPIREECONOMY-REPAY_DEBT"),
                    noButton: new LocString("main_menu_ui", "GENERIC_POPUP.cancel"),
                    yesButton: new LocString("gameplay_ui", "SPIREECONOMY-CONFIRM_REPAYMENT"));

                int selectedAmount = maximum;
                AddAmountSelector(popup, maximum, debt, value => selectedAmount = value);
                bool confirmed = await confirmation;
                if (confirmed)
                    await DebtManager.TryRepayAsync(_player, selectedAmount);
            }
            catch (Exception exception)
            {
                MainFile.Logger.Error($"Debt repayment failed: {exception}");
            }
            finally
            {
                _busy = false;
                Refresh();
            }
        }

        private void OnOpenButtonReleased(NClickableControl _) => OnOpenPressed();

        private static void AddAmountSelector(
            NGenericPopup popup,
            int maximum,
            int debt,
            Action<int> onAmountChanged)
        {
            NVerticalPopup verticalPopup = popup.GetNode<NVerticalPopup>("VerticalPopup");
            VBoxContainer selector = new()
            {
                Name = "SpireEconomyRepaymentAmountSelector",
                CustomMinimumSize = new Vector2(440, 128),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            Label prompt = new()
            {
                Text = Localize("SPIREECONOMY-REPAY_PROMPT"),
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            HSlider amount = new()
            {
                MinValue = 1,
                MaxValue = maximum,
                Value = maximum,
                Step = 1,
                AllowGreater = false,
                AllowLesser = false,
                FocusMode = Control.FocusModeEnum.All,
                CustomMinimumSize = new Vector2(420, 42),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            Label amountLabel = new()
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                CustomMinimumSize = new Vector2(0, 32)
            };

            void UpdateAmountLabel(double value)
            {
                LocString text = new("gameplay_ui", "SPIREECONOMY-REPAY_AMOUNT");
                text.Add("Amount", (int)value);
                text.Add("Debt", debt);
                amountLabel.Text = text.GetFormattedText();
                onAmountChanged((int)value);
            }

            amount.ValueChanged += UpdateAmountLabel;
            UpdateAmountLabel(amount.Value);
            selector.AddChild(prompt);
            selector.AddChild(amountLabel);
            selector.AddChild(amount);

            // Reuse the space already reserved for the popup body. Adding below the body can be
            // clipped because NVerticalPopup has a fixed presentation height.
            MegaRichTextLabel? body = FindDescendant<MegaRichTextLabel>(verticalPopup);
            Node buttonRow = verticalPopup.YesButton.GetParent();
            Node selectorParent = body?.GetParent() ?? buttonRow.GetParent();
            int insertionIndex = body?.GetIndex() ?? buttonRow.GetIndex();
            selectorParent.AddChild(selector);
            selectorParent.MoveChild(selector, insertionIndex);
            if (body is not null)
                body.Visible = false;

            NodePath sliderPath = amount.GetPath();
            NodePath yesPath = verticalPopup.YesButton.GetPath();
            NodePath noPath = verticalPopup.NoButton.GetPath();
            amount.FocusNeighborBottom = yesPath;
            amount.FocusNeighborTop = sliderPath;
            amount.FocusNeighborLeft = sliderPath;
            amount.FocusNeighborRight = sliderPath;
            verticalPopup.YesButton.FocusNeighborTop = sliderPath;
            verticalPopup.NoButton.FocusNeighborTop = sliderPath;
            verticalPopup.YesButton.FocusNeighborLeft = noPath;
            verticalPopup.NoButton.FocusNeighborRight = yesPath;
            Callable.From(amount.GrabFocus).CallDeferred();
            Callable.From(verticalPopup.ResetSize).CallDeferred();
        }

        private void OnDebtChanged(Player player, int _)
        {
            if (ReferenceEquals(player, _player))
                Refresh();
        }

        private void OnTreeExiting()
        {
            DebtManager.DebtChanged -= OnDebtChanged;
            _shopParent.Resized -= QueuePositionUpdate;
            _openButton.Resized -= QueuePositionUpdate;
            if (_cardRemoval is not null)
                _cardRemoval.Resized -= QueuePositionUpdate;
        }

        private static string Localize(string key) =>
            new LocString("gameplay_ui", key).GetFormattedText();
    }
}
