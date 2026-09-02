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
        private const float MaximumButtonWidth = 210f;
        private const float MaximumButtonHeight = 58f;
        private const float PopupContentHorizontalMargin = 64f;
        private const float PopupContentTop = 142f;
        private const float PopupContentBottomMargin = 184f;
        private static readonly System.Reflection.FieldInfo? RemovalVisualField =
            typeof(NMerchantCardRemoval).GetField("_removalVisual",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        private static readonly System.Reflection.MethodInfo? MerchantShowRandomMethod =
            typeof(NMerchantDialogue).GetMethod("ShowRandom",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        private readonly NMerchantInventory _inventory;
        private readonly Player _player;
        private readonly NMerchantCardRemoval? _cardRemoval;
        private readonly NMerchantDialogue? _merchantDialogue;
        private readonly Control _shopParent;
        private readonly NPopupYesNoButton _openButton;
        private readonly OfficialHoverTipBinding _hoverTip;
        private NodePath? _originalRemovalTop;
        private bool _busy;
        private bool _pointerOverButton;
        private bool _buttonHasFocus;

        internal State(NMerchantInventory inventory, Player player)
        {
            _inventory = inventory;
            _player = player;
            _cardRemoval = inventory.GetNodeOrNull<NMerchantCardRemoval>("%MerchantCardRemoval");
            _merchantDialogue = inventory.GetNodeOrNull<NMerchantDialogue>("%MerchantDialogue")
                ?? FindDescendant<NMerchantDialogue>(inventory);
            _shopParent = _cardRemoval?.GetParent<Control>()
                ?? inventory.GetNode<Control>("%SlotsContainer");
            _openButton = CreateOfficialButton();

            _openButton.Name = "SpireEconomyRepaymentButton";
            _openButton.IsYes = true;
            _openButton.FocusMode = Control.FocusModeEnum.All;
            _openButton.Released += OnOpenButtonReleased;
            _openButton.MouseEntered += OnRepaymentButtonMouseEntered;
            _openButton.MouseExited += OnRepaymentButtonMouseExited;
            _openButton.FocusEntered += OnRepaymentButtonFocusEntered;
            _openButton.FocusExited += OnRepaymentButtonFocusExited;
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
                _cardRemoval.ItemRectChanged += QueuePositionUpdate;
            }

            _shopParent.Resized += QueuePositionUpdate;
            _openButton.Resized += QueuePositionUpdate;
            // Initialize can run before the merchant inventory enters the scene tree. The first
            // positioning request is then skipped, so schedule one as soon as the button becomes
            // drawable rather than waiting for the shop's open animation to finish.
            _openButton.TreeEntered += QueuePositionUpdate;
            DebtManager.DebtChanged += OnDebtChanged;
            _openButton.TreeExiting += OnTreeExiting;
            Refresh();
            QueuePositionUpdate();
        }

        internal void Refresh()
        {
            bool canInteract = !_busy;

            // Keep the service clickable even when no payment can be made. In those states the
            // merchant responds through the vanilla speech bubble with a localized line.
            _openButton.Visible = true;
            _openButton.SetEnabled(canInteract);
            _openButton.FocusMode = canInteract
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

            Vector2 nativeSize = _openButton.Size;
            float scale = Math.Min(1f, Math.Min(
                MaximumButtonWidth / Math.Max(1f, nativeSize.X),
                MaximumButtonHeight / Math.Max(1f, nativeSize.Y)));
            _openButton.Scale = Vector2.One * scale;
            Vector2 buttonSize = nativeSize * scale;
            Rect2 removalBounds = GetRemovalVisualBoundsInShopParent();

            // Both controls belong to the animated merchant slots container. Keeping the
            // repayment button in that container's local coordinate space makes it enter with
            // the vanilla shop UI immediately. Viewport/global clamping here would counteract
            // the opening tween and make the button appear only after the tween completed.
            _openButton.Position = new Vector2(
                removalBounds.GetCenter().X - buttonSize.X / 2f,
                removalBounds.Position.Y - buttonSize.Y - ButtonGap);
        }

        private Rect2 GetRemovalVisualBoundsInShopParent()
        {
            NMerchantCardRemoval? cardRemoval = _cardRemoval;
            if (cardRemoval is null)
                return new Rect2();

            if (RemovalVisualField?.GetValue(cardRemoval) is Sprite2D visual &&
                visual.IsInsideTree())
            {
                Rect2 local = visual.GetRect();
                Transform2D shopParentInverse = _shopParent.GetGlobalTransform().AffineInverse();
                Vector2[] corners =
                [
                    shopParentInverse * visual.ToGlobal(local.Position),
                    shopParentInverse * visual.ToGlobal(new Vector2(local.End.X, local.Position.Y)),
                    shopParentInverse * visual.ToGlobal(local.End),
                    shopParentInverse * visual.ToGlobal(new Vector2(local.Position.X, local.End.Y))
                ];
                float left = corners.Min(point => point.X);
                float top = corners.Min(point => point.Y);
                float right = corners.Max(point => point.X);
                float bottom = corners.Max(point => point.Y);
                return new Rect2(left, top, right - left, bottom - top);
            }

            // Conservative fallback for a future game version where the visual field changes.
            Transform2D removalToParent =
                _shopParent.GetGlobalTransform().AffineInverse() * cardRemoval.GetGlobalTransform();
            Vector2 topLeft = removalToParent * Vector2.Zero;
            Vector2 bottomRight = removalToParent * cardRemoval.Size;
            return new Rect2(topLeft, bottomRight - topLeft);
        }

        private async void OnOpenPressed()
        {
            if (_busy)
                return;

            int debt = DebtManager.GetDebt(_player);
            if (debt <= 0)
            {
                ShowMerchantLine("SPIREECONOMY-REPAY_NO_DEBT");
                return;
            }

            if (_player.Gold <= 0)
            {
                ShowMerchantLine("SPIREECONOMY-REPAY_NO_GOLD");
                return;
            }

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

        private void ShowMerchantLine(string key)
        {
            if (_merchantDialogue is null || MerchantShowRandomMethod is null)
            {
                MainFile.Logger.Warn($"Merchant dialogue node unavailable for localization key {key}.");
                return;
            }

            MerchantShowRandomMethod?.Invoke(
                _merchantDialogue,
                [new LocString[] { new("gameplay_ui", key) }]);
        }

        private void OnRepaymentButtonMouseEntered()
        {
            _pointerOverButton = true;
            PointMerchantHandAtRepayment();
        }

        private void OnRepaymentButtonMouseExited()
        {
            _pointerOverButton = false;
            StopMerchantHandIfUntargeted();
        }

        private void OnRepaymentButtonFocusEntered()
        {
            _buttonHasFocus = true;
            PointMerchantHandAtRepayment();
        }

        private void OnRepaymentButtonFocusExited()
        {
            _buttonHasFocus = false;
            StopMerchantHandIfUntargeted();
        }

        private void PointMerchantHandAtRepayment()
        {
            if (_openButton.Visible)
                _inventory.MerchantHand.PointAtTarget(_openButton, Vector2.Zero);
        }

        private void StopMerchantHandIfUntargeted()
        {
            if (!_pointerOverButton && !_buttonHasFocus)
                _inventory.MerchantHand.StopPointing(2f);
        }

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
                Alignment = BoxContainer.AlignmentMode.Center,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
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
                CustomMinimumSize = new Vector2(0, 42),
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

            // NVerticalPopup is intentionally an absolute-layout Control: its header, body and
            // buttons are anchored directly to a fixed-size panel. It is not a layout container.
            // Keep the vanilla panel size and overlay our selector inside the body rectangle.
            // Calling ResetSize() here would collapse the panel to the selector's minimum size,
            // which moves the bottom-anchored yes/no buttons to the top edge of the viewport.
            verticalPopup.AddChild(selector);
            selector.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            selector.OffsetLeft = PopupContentHorizontalMargin;
            selector.OffsetTop = PopupContentTop;
            selector.OffsetRight = -PopupContentHorizontalMargin;
            selector.OffsetBottom = -PopupContentBottomMargin;
            MegaRichTextLabel? body = verticalPopup.GetNodeOrNull<MegaRichTextLabel>("Description");
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
            _openButton.TreeEntered -= QueuePositionUpdate;
            _openButton.MouseEntered -= OnRepaymentButtonMouseEntered;
            _openButton.MouseExited -= OnRepaymentButtonMouseExited;
            _openButton.FocusEntered -= OnRepaymentButtonFocusEntered;
            _openButton.FocusExited -= OnRepaymentButtonFocusExited;
            if (_cardRemoval is not null)
            {
                _cardRemoval.Resized -= QueuePositionUpdate;
                _cardRemoval.ItemRectChanged -= QueuePositionUpdate;
            }
        }

        private static string Localize(string key) =>
            new LocString("gameplay_ui", key).GetFormattedText();
    }
}
