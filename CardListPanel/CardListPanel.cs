using Godot;
using System;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Localization.Fonts;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Helpers.Models;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.HoverTips;

namespace sts2decktracker
{
    public partial class CardListPanel : Panel
    {
        private VBoxContainer _cardList;
        private Label _titleLabel;
        private bool _combatStartLogged = false;
        private PileType _pileType = PileType.Draw;
        private ModSettings _settings;
        private System.Collections.Generic.List<(CardModel card, int count)> _shuffledOrder = null;
        private float _targetOpacity = 0.3f;
        private float _currentOpacity = 0.3f;
        private const float OpacityTransitionSpeed = 5.0f;
        private float _timeSinceLastChange = 0f;
        private float _idleDelaySeconds = 2.0f;
        private CardPile _currentPile = null;
        private MegaCrit.Sts2.Core.Entities.Players.Player _currentPlayer = null;
        private static Font _KreonRegularFont;
        private static Font KreonRegular => _KreonRegularFont ??= ResourceLoader.Load<Font>("res://fonts/kreon_regular.ttf");
        private Control _contentContainer;
        private Button _resetBtn;
        private bool _isDragging = false;
        private Vector2 _dragOffset;
        private Vector2 _defaultPosition;
        private bool _hasCustomPosition = false;
        private Vector2 _customPosition;
        private CardModel _hoveredCard = null;
        private Control _hoveredClip = null;
        private readonly System.Collections.Generic.List<(CardModel card, Control clip, System.Collections.Generic.List<IHoverTip> tips)> _cardHoverData = new();
        private readonly System.Collections.Generic.Dictionary<CardModel, int> _cardSnapshot = new();
        private readonly System.Collections.Generic.Dictionary<string, RowView> _rowsByKey = new();


        private sealed class RowView
        {
            public HBoxContainer Root;
            public Control ClipContainer;
            public Label CountLabel;
            public Control EnergyCostContainer;
            public Control StarCostContainer;
            public int CostHash;
        }
        private ScrollContainer _scrollContainer;
        private Button _lockBtn;
        private Button _settingsBtn;
        private Control _miniSettingsPanel;
        private VBoxContainer _buttonStrip;
        private bool _isDraggableLocked = false;
        private Label _miniCardWidthLabel;
        private Label _miniCardHeightLabel;
        private Label _miniIdleLabel;
        private Label _miniActiveLabel;

        public void SetPileType(PileType pileType)
        {
            _pileType = pileType;
        }

        public override void _ExitTree()
        {
            if (_hasCustomPosition && !DeckTrackerInjectionPatch._isReturningToMainMenu)
                DeckTrackerInjectionPatch.SaveCustomPosition(_pileType, _customPosition);
        }

        public void SetSettings(ModSettings settings)
        {
            _settings = settings;
            _targetOpacity = settings?.IdleOpacity ?? 0.3f;
            _currentOpacity = _targetOpacity;
            _idleDelaySeconds = settings?.IdleDelaySeconds ?? 2.0f;
            if (_contentContainer != null)
                _contentContainer.Modulate = new Color(1, 1, 1, _currentOpacity);
            ApplyScrollSettings();
            RefreshMiniPanel();
            if (_currentPile != null && _cardList != null)
            {
                ClearAllRows();
                UpdateCardList(_cardList, _currentPile);
            }
        }

        public Vector2? GetCustomPosition() => _hasCustomPosition ? _customPosition : null;

        public Vector2 GetContentPosition() => _hasCustomPosition ? _customPosition : GlobalPosition;

        public void SetCustomPosition(Vector2 pos)
        {
            _hasCustomPosition = true;
            _customPosition = pos;
            if (IsInsideTree()) GlobalPosition = pos;
        }

        public void SetDefaultPosition(Vector2 pos)
        {
            _defaultPosition = pos;
            if (IsInsideTree()) UpdatePosition();
        }

        private void UpdatePosition()
        {
            GlobalPosition = _hasCustomPosition ? _customPosition : _defaultPosition;
        }

        public void UpdatePositionPublic() => UpdatePosition();

        private const float ScrollableBottomY = 790f;

        private void ApplyScrollSettings()
        {
            if (_scrollContainer == null) return;
            bool scrollable = _settings?.Scrollable ?? false;
            if (scrollable)
            {
                _scrollContainer.SizeFlagsVertical = SizeFlags.ShrinkBegin;
                if (_cardList != null) _cardList.SizeFlagsVertical = SizeFlags.ShrinkBegin;
                UpdateScrollHeight();
            }
            else
            {
                _scrollContainer.CustomMinimumSize = Vector2.Zero;
                _scrollContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
                if (_cardList != null) _cardList.SizeFlagsVertical = SizeFlags.ExpandFill;
            }
        }

        private void UpdateScrollHeight()
        {
            if (_scrollContainer == null) return;
            float height;
            if (_settings?.ScrollableAutoHeight ?? true)
                height = Mathf.Max(50f, ScrollableBottomY - GlobalPosition.Y);
            else
                height = Mathf.Max(50f, _settings?.ScrollableHeight ?? 400);
            _scrollContainer.CustomMinimumSize = new Vector2(0, height);
        }

        private static void SetMouseIgnoreRecursive(Node node)
        {
            if (node is Control control)
                control.MouseFilter = MouseFilterEnum.Ignore;
            foreach (Node child in node.GetChildren())
                SetMouseIgnoreRecursive(child);
        }

        public override void _Ready()
        {
            var pw = _settings?.PanelWidth ?? 312;
            var ph = _settings?.PanelHeight ?? 480;
            CustomMinimumSize = new Vector2(pw, ph);
            Size = new Vector2(pw, ph);
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
            SizeFlagsVertical = SizeFlags.ShrinkBegin;
            MouseFilter = MouseFilterEnum.Ignore;
            SetAnchorsPreset(LayoutPreset.TopLeft);
            AddThemeStyleboxOverride("panel", new StyleBoxEmpty());

            var mainContainer = new VBoxContainer();
            mainContainer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            mainContainer.AddThemeConstantOverride("separation", 5);
            AddChild(mainContainer);

            var marginContainer = new MarginContainer();
            marginContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
            marginContainer.AddThemeConstantOverride("margin_left", 10);
            marginContainer.AddThemeConstantOverride("margin_right", 10);
            marginContainer.AddThemeConstantOverride("margin_top", 10);
            marginContainer.AddThemeConstantOverride("margin_bottom", 10);
            mainContainer.AddChild(marginContainer);

            var innerContainer = new VBoxContainer();
            innerContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
            innerContainer.AddThemeConstantOverride("separation", 8);
            marginContainer.AddChild(innerContainer);


            _scrollContainer = new ScrollContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill,
                HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
                VerticalScrollMode = ScrollContainer.ScrollMode.ShowNever,
            };
            innerContainer.AddChild(_scrollContainer);

            _cardList = new VBoxContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill,
                Theme = new Theme()
            };
            _cardList.AddThemeConstantOverride("separation", 3);
            _scrollContainer.AddChild(_cardList);

            _contentContainer = mainContainer;

            SetMouseIgnoreRecursive(this);

            _scrollContainer.MouseFilter = MouseFilterEnum.Ignore;
            Callable.From(() => _scrollContainer.GetVScrollBar().Modulate = Colors.Transparent).CallDeferred();

            // 버튼 스트립 - SetMouseIgnoreRecursive 이후 추가하므로 MouseFilter 기본값(Stop) 유지
            bool isDiscard = _pileType == PileType.Discard;
            _buttonStrip = new VBoxContainer();
            var buttonStrip = _buttonStrip;
            buttonStrip.AnchorTop = 0f; buttonStrip.AnchorBottom = 0f;
            buttonStrip.OffsetTop = 4f; buttonStrip.OffsetBottom = 86f;
            buttonStrip.ZIndex = 1000;
            buttonStrip.MouseFilter = MouseFilterEnum.Pass;
            if (isDiscard)
            {
                buttonStrip.AnchorLeft = 0f; buttonStrip.AnchorRight = 0f;
                buttonStrip.OffsetLeft = 0f; buttonStrip.OffsetRight = 28f;
            }
            else
            {
                buttonStrip.AnchorLeft = 1f; buttonStrip.AnchorRight = 1f;
                buttonStrip.OffsetLeft = -28f; buttonStrip.OffsetRight = 0f;
            }

            if (!isDiscard)
            {
                _settingsBtn = new Button { Text = "⚙", CustomMinimumSize = new Vector2(24, 24), FocusMode = FocusModeEnum.None, Visible = false };
                _settingsBtn.Pressed += () =>
                {
                    if (_miniSettingsPanel != null)
                    {
                        _miniSettingsPanel.Visible = !_miniSettingsPanel.Visible;
                        RefreshMiniPanel();
                    }
                };
                buttonStrip.AddChild(_settingsBtn);
            }

            _lockBtn = new Button { CustomMinimumSize = new Vector2(24, 24), FocusMode = FocusModeEnum.None, Visible = false };
            _lockBtn.Pressed += () => _isDraggableLocked = !_isDraggableLocked;
            buttonStrip.AddChild(_lockBtn);
            _resetBtn = new Button { Text = "↺", CustomMinimumSize = new Vector2(24, 24), FocusMode = FocusModeEnum.None, Visible = false };
            _resetBtn.Pressed += () =>
            {
                _hasCustomPosition = false;
                DeckTrackerInjectionPatch.ClearCustomPosition(_pileType);
                UpdatePosition();
            };
            buttonStrip.AddChild(_resetBtn);
            AddChild(buttonStrip);

            if (!isDiscard)
            {
                _miniSettingsPanel = CreateMiniSettingsPanel();
                AddChild(_miniSettingsPanel);
                RefreshMiniPanel();
            }

            ApplyScrollSettings();
            Callable.From(UpdatePosition).CallDeferred();
        }


        public override void _Input(InputEvent @event)
        {
            if (!(_settings?.Draggable ?? false) || _isDraggableLocked) return;
            if (@event is not InputEventMouseButton mb || mb.ButtonIndex != MouseButton.Left) return;
            if (mb.Pressed)
            {
                if (new Rect2(GlobalPosition, Size).HasPoint(mb.GlobalPosition))
                {
                    _isDragging = true;
                    _dragOffset = mb.GlobalPosition - GlobalPosition;
                }
            }
            else if (_isDragging)
            {
                _isDragging = false;
                _hasCustomPosition = true;
                _customPosition = GlobalPosition;
            }
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (_scrollContainer == null || !(_settings?.Scrollable ?? false)) return;
            if (@event is not InputEventMouseButton mb) return;
            if (mb.ButtonIndex != MouseButton.WheelUp && mb.ButtonIndex != MouseButton.WheelDown) return;
            var rect = new Rect2(_scrollContainer.GlobalPosition, _scrollContainer.Size);
            if (!rect.HasPoint(GetGlobalMousePosition())) return;
            int scrollStep = _settings?.CardHeight ?? 32;
            _scrollContainer.ScrollVertical += mb.ButtonIndex == MouseButton.WheelDown ? scrollStep : -scrollStep;
            GetViewport().SetInputAsHandled();
        }

        public override void _Process(double delta)
        {
            if (_isDragging && (_settings?.Draggable ?? true))
                GlobalPosition = GetGlobalMousePosition() - _dragOffset;

            var mousePos = GetGlobalMousePosition();
            bool mouseOver = (_buttonStrip != null && _buttonStrip.GetGlobalRect().HasPoint(mousePos))
                || (GodotObject.IsInstanceValid(_miniSettingsPanel) && (_miniSettingsPanel?.Visible ?? false)
                    && _miniSettingsPanel.GetGlobalRect().HasPoint(mousePos));
            bool draggable = _settings?.Draggable ?? false;
            if (_resetBtn != null)
                _resetBtn.Visible = draggable && _hasCustomPosition && mouseOver;
            if (_lockBtn != null)
            {
                _lockBtn.Visible = draggable && mouseOver;
                _lockBtn.Text = _isDraggableLocked ? "🔒" : "🔓";
            }
            if (_settingsBtn != null)
                _settingsBtn.Visible = mouseOver;

            if (_settings?.Scrollable ?? false)
                UpdateScrollHeight();

            // Card hover tooltip detection
            if (!(_settings?.ShowCardTooltip ?? true) && _hoveredCard != null)
            {
                if (_hoveredClip != null && GodotObject.IsInstanceValid(_hoveredClip))
                    NHoverTipSet.Remove(_hoveredClip);
                _hoveredCard = null;
                _hoveredClip = null;
            }
            else if (_cardHoverData.Count > 0 && (_settings?.ShowCardTooltip ?? true))
            {
                CardModel newHovered = null;
                Control newHoveredClip = null;
                System.Collections.Generic.List<IHoverTip> newHoveredTips = null;
                var scrollClip = _scrollContainer != null
                    ? new Rect2(_scrollContainer.GlobalPosition, _scrollContainer.Size)
                    : (Rect2?)null;
                foreach (var entry in _cardHoverData)
                {
                    if (GodotObject.IsInstanceValid(entry.clip))
                    {
                        var rect = new Rect2(entry.clip.GlobalPosition, entry.clip.Size);
                        if (rect.HasPoint(mousePos) && (scrollClip == null || scrollClip.Value.HasPoint(mousePos)))
                        {
                            newHovered = entry.card;
                            newHoveredClip = entry.clip;
                            newHoveredTips = entry.tips;
                            break;
                        }
                    }
                }
                if (newHovered != _hoveredCard)
                {
                    if (_hoveredClip != null && GodotObject.IsInstanceValid(_hoveredClip))
                        NHoverTipSet.Remove(_hoveredClip);
                    if (newHovered != null)
                        NHoverTipSet.CreateAndShow(newHoveredClip, newHoveredTips, HoverTip.GetHoverTipAlignment(newHoveredClip));
                    _hoveredCard = newHovered;
                    _hoveredClip = newHoveredClip;
                }
            }

            if (Math.Abs(_currentOpacity - _targetOpacity) > 0.01f)
            {
                _currentOpacity = Mathf.Lerp(_currentOpacity, _targetOpacity, OpacityTransitionSpeed * (float)delta);
                if (_contentContainer != null)
                    _contentContainer.Modulate = new Color(1, 1, 1, _currentOpacity);
            }

            if (CombatManager.Instance == null || !CombatManager.Instance.IsInProgress)
            {
                _currentPile = null;
                _currentPlayer = null;
                _combatStartLogged = false;
                return;
            }

            if (_currentPlayer == null)
            {
                var combatState = CombatManager.Instance.DebugOnlyGetState();
                if (combatState == null)
                    return;

                _currentPlayer = LocalContext.GetMe(combatState);
                if (_currentPlayer?.PlayerCombatState == null)
                    return;
            }

            var drawPile = _currentPlayer.PlayerCombatState.DrawPile;
            if (drawPile == null)
            {
                if (!_combatStartLogged)
                {
                    GD.PrintErr("[CardListPanel] DrawPile is null!");
                    _combatStartLogged = true;
                }
                return;
            }

            var pile = _pileType == PileType.Draw ? drawPile : _currentPlayer.PlayerCombatState.DiscardPile;
            if (pile == null)
                return;

            if (!_combatStartLogged)
                _combatStartLogged = true;

            if (_currentPile != pile)
            {
                _currentPile = pile;
                UpdateCardList(_cardList, _currentPile);
                _targetOpacity = _settings?.ActiveOpacity ?? 1.0f;
                _timeSinceLastChange = 0f;
            }
            else if (_currentPile != null)
            {
                bool changed = _currentPile.Cards.Count != _cardSnapshot.Count;
                if (!changed)
                {
                    foreach (var c in _currentPile.Cards)
                    {
                        if (!_cardSnapshot.TryGetValue(c, out int savedHash) || GetCardHash(c) != savedHash)
                        {
                            changed = true;
                            break;
                        }
                    }
                }
                if (changed)
                {
                    if (_pileType == PileType.Draw)
                        TopCardTracker.PruneCards(_currentPile);
                    UpdateCardList(_cardList, _currentPile);
                    _targetOpacity = _settings?.ActiveOpacity ?? 1.0f;
                    _timeSinceLastChange = 0f;
                }
            }

            _timeSinceLastChange += (float)delta;
            if (_timeSinceLastChange >= _idleDelaySeconds || (_currentPile != null && _currentPile.IsEmpty))
                _targetOpacity = _settings?.IdleOpacity ?? 0.3f;
        }

        private void UpdateCardList(VBoxContainer container, CardPile pile)
        {
            if (container == null || pile == null)
                return;

            if (_hoveredCard != null && _hoveredClip != null && GodotObject.IsInstanceValid(_hoveredClip))
                NHoverTipSet.Remove(_hoveredClip);
            _hoveredCard = null;
            _hoveredClip = null;

            _cardSnapshot.Clear();
            foreach (var c in pile.Cards)
                _cardSnapshot[c] = GetCardHash(c);

            var cardGroups = new System.Collections.Generic.Dictionary<string, (CardModel card, int count)>();
            foreach (var card in pile.Cards)
            {
                string key = GroupKey(card);
                if (cardGroups.TryGetValue(key, out var existing))
                    cardGroups[key] = (existing.card, existing.count + 1);
                else
                    cardGroups[key] = (card, 1);
            }

            System.Collections.Generic.List<(CardModel card, int count)> displayGroups;
            if (_shuffledOrder == null || _shuffledOrder.Count == 0)
            {
                displayGroups = new System.Collections.Generic.List<(CardModel card, int count)>(cardGroups.Values);
                var random = new System.Random();
                for (int i = displayGroups.Count - 1; i > 0; i--)
                {
                    int j = random.Next(i + 1);
                    (displayGroups[i], displayGroups[j]) = (displayGroups[j], displayGroups[i]);
                }
                _shuffledOrder = displayGroups;
            }
            else
            {
                displayGroups = new System.Collections.Generic.List<(CardModel card, int count)>();
                var remainingCards = new System.Collections.Generic.Dictionary<string, (CardModel card, int count)>(cardGroups);

                foreach (var oldGroup in _shuffledOrder)
                {
                    string key = GroupKey(oldGroup.card);
                    if (remainingCards.TryGetValue(key, out var newGroup))
                    {
                        displayGroups.Add(newGroup);
                        remainingCards.Remove(key);
                    }
                    else
                    {
                        displayGroups.Add((oldGroup.card, 0));
                    }
                }

                foreach (var newCard in remainingCards.Values)
                    displayGroups.Add(newCard);

                _shuffledOrder = displayGroups;
            }

            var desiredKeys = new System.Collections.Generic.HashSet<string>();
            foreach (var g in displayGroups)
            {
                if (g.count > 0) desiredKeys.Add(GroupKey(g.card));
            }

            System.Collections.Generic.List<string> toRemove = null;
            foreach (var kvp in _rowsByKey)
            {
                if (!desiredKeys.Contains(kvp.Key))
                {
                    (toRemove ??= new System.Collections.Generic.List<string>()).Add(kvp.Key);
                }
            }
            if (toRemove != null)
            {
                foreach (var key in toRemove)
                {
                    var row = _rowsByKey[key];
                    if (GodotObject.IsInstanceValid(row.Root))
                    {
                        container.RemoveChild(row.Root);
                        row.Root.QueueFree();
                    }
                    _rowsByKey.Remove(key);
                }
            }

            _cardHoverData.Clear();

            int desiredIndex = 0;
            foreach (var group in displayGroups)
            {
                if (group.count == 0) continue;
                string key = GroupKey(group.card);

                if (!_rowsByKey.TryGetValue(key, out var row))
                {
                    row = BuildCardRow(group.card, group.count);
                    if (row == null) continue;
                    container.AddChild(row.Root);
                    _rowsByKey[key] = row;
                }
                else
                {
                    if (row.CountLabel != null)
                        row.CountLabel.Text = group.count.ToString();
                    int newCostHash = GetCostHash(group.card);
                    if (newCostHash != row.CostHash)
                    {
                        RebuildCostContainers(row, group.card);
                        row.CostHash = newCostHash;
                    }
                }

                if (row.ClipContainer != null && GodotObject.IsInstanceValid(row.ClipContainer))
                {
                    var hoverTips = new System.Collections.Generic.List<IHoverTip> { new CardHoverTip(group.card) };
                    hoverTips.AddRange(group.card.HoverTips);
                    _cardHoverData.Add((group.card, row.ClipContainer, hoverTips));
                }

                if (row.Root.GetIndex() != desiredIndex)
                    container.MoveChild(row.Root, desiredIndex);
                desiredIndex++;
            }

            SetMouseIgnoreRecursiveExceptHover(container);
        }

        private void ClearAllRows()
        {
            foreach (var kvp in _rowsByKey)
            {
                var row = kvp.Value;
                if (GodotObject.IsInstanceValid(row.Root))
                {
                    if (_cardList != null && row.Root.GetParent() == _cardList)
                        _cardList.RemoveChild(row.Root);
                    row.Root.QueueFree();
                }
            }
            _rowsByKey.Clear();
        }

        private static string GroupKey(CardModel card)
        {
            string enchantmentKey = card.Enchantment != null ? card.Enchantment.GetType().Name : "none";
            int energy = card.EnergyCost.CostsX ? int.MinValue : card.EnergyCost.GetWithModifiers(CostModifiers.All);
            int star = card.HasStarCostX ? int.MinValue : card.GetStarCostWithModifiers();
            return $"{card.Title}|{card.IsUpgraded}|{enchantmentKey}|{energy}|{star}";
        }

        private static int GetCostHash(CardModel c)
        {
            int energy = c.EnergyCost.CostsX ? int.MinValue : c.EnergyCost.GetWithModifiers(CostModifiers.All);
            int star = c.HasStarCostX ? int.MinValue : c.GetStarCostWithModifiers();
            int costColor = 0;
            try
            {
                if (c.CombatState != null)
                    costColor = (int)CardCostHelper.GetEnergyCostColor(c, c.CombatState);
            }
            catch { }
            bool justUpgraded = c.EnergyCost != null && !c.EnergyCost.CostsX && c.EnergyCost.WasJustUpgraded;
            return HashCode.Combine(energy, star, costColor, justUpgraded);
        }


        private RowView BuildCardRow(CardModel card, int count)
        {
            try
            {
                var portrait = card.Portrait;
                if (portrait == null)
                {
                    var fallbackRoot = new HBoxContainer();
                    var fallbackLabel = new Label();
                    fallbackLabel.Text = GetCardDisplayName(card);
                    fallbackLabel.AddThemeFontSizeOverride("font_size", 12);
                    fallbackRoot.AddChild(fallbackLabel);
                    return new RowView { Root = fallbackRoot };
                }

                int cardHeight = _settings?.CardHeight ?? 36;
                int cardWidth = _settings?.CardWidth ?? 280;
                int cardImageWidth = _settings?.CardImageWidth ?? 202;

                var cardRowContainer = new HBoxContainer
                {
                    CustomMinimumSize = new Vector2(cardWidth, cardHeight)
                };
                cardRowContainer.AddThemeConstantOverride("separation", 2);

                var clipContainer = new Control
                {
                    CustomMinimumSize = new Vector2(cardImageWidth, cardHeight),
                    Size = new Vector2(cardImageWidth, cardHeight),
                    ClipContents = true,
                    MouseFilter = Control.MouseFilterEnum.Ignore
                };

                var textureRect = new TextureRect
                {
                    Texture = portrait,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                };
                textureRect.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
                textureRect.OffsetTop = -cardHeight;
                clipContainer.AddChild(textureRect);

                var labelRow = new HBoxContainer();
                labelRow.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
                labelRow.MouseFilter = MouseFilterEnum.Ignore;
                labelRow.AddThemeConstantOverride("separation", 0);
                clipContainer.AddChild(labelRow);

                int countFontSize = _settings?.CardCountFontSize ?? 28;
                var countLabel = new Label
                {
                    Text = count.ToString(),
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    CustomMinimumSize = new Vector2(cardHeight, 0),
                    SizeFlagsVertical = SizeFlags.ExpandFill,
                };
                countLabel.AddThemeFontSizeOverride("font_size", countFontSize);
                countLabel.AddThemeColorOverride("font_color", StsColors.gold);
                countLabel.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.188f));
                countLabel.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 1));
                countLabel.AddThemeConstantOverride("shadow_offset_x", 2);
                countLabel.AddThemeConstantOverride("shadow_offset_y", 2);
                countLabel.AddThemeConstantOverride("outline_size", 10);
                countLabel.AddThemeConstantOverride("shadow_outline_size", 10);
                countLabel.AddThemeFontOverride("font", KreonRegular);
                countLabel.ApplyLocaleFontSubstitution(FontType.Bold, "font");
                labelRow.AddChild(countLabel);

                var nameLabel = new Label
                {
                    SizeFlagsHorizontal = SizeFlags.ExpandFill,
                    SizeFlagsVertical = SizeFlags.ExpandFill,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                nameLabel.Text = GetCardDisplayName(card);
                int nameFontSize = _settings?.CardNameFontSize ?? 24;
                nameLabel.AddThemeFontSizeOverride("font_size", nameFontSize);

                Color titleColor;
                Color titleOutlineColor;
                var colorMode = _settings?.CardColorMode ?? CardColorMode.Full;
                if (colorMode == CardColorMode.None)
                {
                    titleColor = StsColors.cream;
                    titleOutlineColor = StsColors.cardTitleOutlineCommon;
                }
                else if (card.Enchantment != null)
                {
                    titleColor = new Color(0.85f, 0.6f, 1f, 1f);
                    titleOutlineColor = new Color(0.3f, 0.05f, 0.45f, 1f);
                }
                else if (card.CurrentUpgradeLevel > 0)
                {
                    titleColor = StsColors.green;
                    titleOutlineColor = StsColors.cardTitleOutlineSpecial;
                }
                else if (colorMode == CardColorMode.Full)
                {
                    titleColor = StsColors.cream;
                    titleOutlineColor = GetTitleOutlineColorByRarity(card.Rarity);
                }
                else
                {
                    titleColor = StsColors.cream;
                    titleOutlineColor = StsColors.cardTitleOutlineCommon;
                }

                nameLabel.AddThemeColorOverride("font_color", titleColor);
                nameLabel.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.188f));
                nameLabel.AddThemeColorOverride("font_outline_color", titleOutlineColor);
                nameLabel.AddThemeConstantOverride("shadow_offset_x", 2);
                nameLabel.AddThemeConstantOverride("shadow_offset_y", 2);
                nameLabel.AddThemeConstantOverride("outline_size", 10);
                nameLabel.AddThemeConstantOverride("shadow_outline_size", 10);
                nameLabel.AddThemeFontOverride("font", KreonRegular);
                nameLabel.ApplyLocaleFontSubstitution(FontType.Bold, "font");
                labelRow.AddChild(nameLabel);

                if (card.Enchantment != null)
                {
                    try
                    {
                        string enchantPath = card.Enchantment.IntendedIconPath;
                        var enchantIcon = string.IsNullOrEmpty(enchantPath) ? null : ResourceLoader.Load<Texture2D>(enchantPath);
                        if (enchantIcon != null)
                        {
                            int enchantIconSize = cardHeight - 6;
                            float enchantLeftInLabel = (cardImageWidth - enchantIconSize - 4) - cardHeight;

                            nameLabel.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
                            nameLabel.CustomMinimumSize = new Vector2(enchantLeftInLabel, 0);
                            nameLabel.ClipText = true;

                            var enchantIconRect = new TextureRect
                            {
                                Texture = enchantIcon,
                                Position = new Vector2(cardImageWidth - enchantIconSize - 4, 2),
                                CustomMinimumSize = new Vector2(enchantIconSize, enchantIconSize),
                                ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional,
                                StretchMode = TextureRect.StretchModeEnum.KeepAspect,
                                MouseFilter = Control.MouseFilterEnum.Ignore
                            };
                            enchantIconRect.Modulate = new Color(1.5f, 1.3f, 1.8f, 1.0f);
                            clipContainer.AddChild(enchantIconRect);
                        }
                    }
                    catch (Exception ex)
                    {
                        GD.PrintErr($"[CardListPanel] Error adding enchantment icon: {ex.Message}");
                    }
                }

                cardRowContainer.AddChild(clipContainer);

                var view = new RowView
                {
                    Root = cardRowContainer,
                    ClipContainer = clipContainer,
                    CountLabel = countLabel,
                    CostHash = GetCostHash(card)
                };

                view.EnergyCostContainer = BuildEnergyCostContainer(card);
                if (view.EnergyCostContainer != null)
                    cardRowContainer.AddChild(view.EnergyCostContainer);

                view.StarCostContainer = BuildStarCostContainer(card);
                if (view.StarCostContainer != null)
                    cardRowContainer.AddChild(view.StarCostContainer);

                return view;
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[CardListPanel] Error building card row: {ex.Message}");
                var fallbackRoot = new HBoxContainer();
                var fallbackLabel = new Label();
                fallbackLabel.Text = GetCardDisplayName(card);
                fallbackLabel.AddThemeFontSizeOverride("font_size", 12);
                fallbackRoot.AddChild(fallbackLabel);
                return new RowView { Root = fallbackRoot };
            }
        }

        private void RebuildCostContainers(RowView row, CardModel card)
        {
            if (row.EnergyCostContainer != null && GodotObject.IsInstanceValid(row.EnergyCostContainer))
            {
                if (row.EnergyCostContainer.GetParent() == row.Root)
                    row.Root.RemoveChild(row.EnergyCostContainer);
                row.EnergyCostContainer.QueueFree();
                row.EnergyCostContainer = null;
            }
            if (row.StarCostContainer != null && GodotObject.IsInstanceValid(row.StarCostContainer))
            {
                if (row.StarCostContainer.GetParent() == row.Root)
                    row.Root.RemoveChild(row.StarCostContainer);
                row.StarCostContainer.QueueFree();
                row.StarCostContainer = null;
            }

            row.EnergyCostContainer = BuildEnergyCostContainer(card);
            if (row.EnergyCostContainer != null)
                row.Root.AddChild(row.EnergyCostContainer);

            row.StarCostContainer = BuildStarCostContainer(card);
            if (row.StarCostContainer != null)
                row.Root.AddChild(row.StarCostContainer);
        }

        private Control BuildEnergyCostContainer(CardModel card)
        {
            try
            {
                string energyIconPath = card.VisualCardPool?.EnergyIconPath;
                var energyIcon = string.IsNullOrEmpty(energyIconPath) ? card.EnergyIcon : ResourceLoader.Load<Texture2D>(energyIconPath);
                if (energyIcon == null) return null;

                string costText;
                bool showIcon = true;
                if (card.EnergyCost.CostsX)
                {
                    costText = "X";
                }
                else
                {
                    int costWithModifiers = card.EnergyCost.GetWithModifiers(CostModifiers.All);
                    costText = costWithModifiers.ToString();
                    showIcon = costWithModifiers >= 0;
                }
                if (!showIcon) return null;

                int iconSize = _settings?.CostIconSize ?? 30;
                var energyCostContainer = new Control
                {
                    CustomMinimumSize = new Vector2(iconSize, iconSize),
                    Size = new Vector2(iconSize, iconSize),
                    MouseFilter = Control.MouseFilterEnum.Ignore
                };

                var energyIconRect = new TextureRect
                {
                    Texture = energyIcon,
                    CustomMinimumSize = new Vector2(iconSize, iconSize),
                    ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspect,
                    MouseFilter = Control.MouseFilterEnum.Ignore
                };
                energyCostContainer.AddChild(energyIconRect);

                var costLabel = new Label
                {
                    Text = costText,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Size = new Vector2(iconSize, iconSize)
                };
                int costFontSize = _settings?.EnergyCostFontSize ?? 28;
                costLabel.AddThemeFontSizeOverride("font_size", costFontSize);

                Color fontColor = StsColors.cream;
                Color outlineColor = card.Pool.EnergyOutlineColor;
                if (card.EnergyCost != null && !card.EnergyCost.CostsX && card.EnergyCost.WasJustUpgraded)
                {
                    fontColor = StsColors.green;
                    outlineColor = StsColors.energyGreenOutline;
                }
                else if (card.CombatState != null)
                {
                    CardCostColor costColor = CardCostHelper.GetEnergyCostColor(card, card.CombatState);
                    switch (costColor)
                    {
                        case CardCostColor.Increased:
                            fontColor = StsColors.energyBlue;
                            outlineColor = StsColors.energyBlueOutline;
                            break;
                        case CardCostColor.Decreased:
                            fontColor = StsColors.green;
                            outlineColor = StsColors.energyGreenOutline;
                            break;
                    }
                }

                costLabel.AddThemeColorOverride("font_color", fontColor);
                costLabel.AddThemeColorOverride("font_outline_color", outlineColor);
                costLabel.AddThemeConstantOverride("shadow_offset_x", 2);
                costLabel.AddThemeConstantOverride("shadow_offset_y", 2);
                costLabel.AddThemeConstantOverride("outline_size", 10);
                costLabel.AddThemeConstantOverride("shadow_outline_size", 10);
                costLabel.AddThemeFontOverride("font", KreonRegular);
                costLabel.ApplyLocaleFontSubstitution(FontType.Bold, "font");
                energyCostContainer.AddChild(costLabel);
                return energyCostContainer;
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[CardListPanel] Error loading energy icon: {ex.Message}");
                return null;
            }
        }

        private Control BuildStarCostContainer(CardModel card)
        {
            try
            {
                int starCost = card.GetStarCostWithModifiers();
                if (!card.HasStarCostX && starCost < 0) return null;

                var starIcon = ResourceLoader.Load<Texture2D>("res://images/packed/sprite_fonts/star_icon.png");
                if (starIcon == null) return null;

                string starCostText = card.HasStarCostX ? "X" : starCost.ToString();
                int iconSize = _settings?.CostIconSize ?? 30;

                var starCostContainer = new Control
                {
                    CustomMinimumSize = new Vector2(iconSize, iconSize),
                    Size = new Vector2(iconSize, iconSize),
                    MouseFilter = Control.MouseFilterEnum.Ignore
                };

                var starIconRect = new TextureRect
                {
                    Texture = starIcon,
                    CustomMinimumSize = new Vector2(iconSize, iconSize),
                    ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspect,
                    MouseFilter = Control.MouseFilterEnum.Ignore
                };
                starCostContainer.AddChild(starIconRect);

                var starCostLabel = new Label
                {
                    Text = starCostText,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Size = new Vector2(iconSize, iconSize)
                };
                int starCostFontSize = _settings?.EnergyCostFontSize ?? 28;
                starCostLabel.AddThemeFontSizeOverride("font_size", starCostFontSize);
                starCostLabel.AddThemeColorOverride("font_color", StsColors.cream);
                starCostLabel.AddThemeColorOverride("font_outline_color", card.Pool.EnergyOutlineColor);
                starCostLabel.AddThemeConstantOverride("shadow_offset_x", 2);
                starCostLabel.AddThemeConstantOverride("shadow_offset_y", 2);
                starCostLabel.AddThemeConstantOverride("outline_size", 10);
                starCostLabel.AddThemeConstantOverride("shadow_outline_size", 10);
                starCostLabel.AddThemeFontOverride("font", KreonRegular);
                starCostLabel.ApplyLocaleFontSubstitution(FontType.Bold, "font");
                starCostContainer.AddChild(starCostLabel);
                return starCostContainer;
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[CardListPanel] Error loading star icon: {ex.Message}");
                return null;
            }
        }

        private static void SetMouseIgnoreRecursiveExceptHover(Node node)
        {
            if (node is Control control && !control.ClipContents)
                control.MouseFilter = MouseFilterEnum.Ignore;
            foreach (Node child in node.GetChildren())
                SetMouseIgnoreRecursiveExceptHover(child);
        }

        private static string GetCardDisplayName(CardModel card)
        {
            return card.IsUpgraded ? $"{card.Title}" : card.Title;
        }

        private static int GetCardHash(CardModel c)
        {
            int energyCost = c.EnergyCost.CostsX ? -1 : c.EnergyCost.GetWithModifiers(CostModifiers.All);
            int starCost = c.HasStarCostX ? -1 : c.GetStarCostWithModifiers();
            return HashCode.Combine(
                c.CurrentUpgradeLevel,
                energyCost,
                starCost,
                c.Enchantment?.GetType().GetHashCode() ?? 0
            );
        }

        public void ResetTemporaryState()
        {
            _isDraggableLocked = false;
            if (_miniSettingsPanel != null) _miniSettingsPanel.Visible = false;
        }

        private void RefreshMiniPanel()
        {
            if (_settings == null) return;
            if (_miniCardWidthLabel != null)
                _miniCardWidthLabel.Text = _settings.CardWidth.ToString();
            if (_miniCardHeightLabel != null)
                _miniCardHeightLabel.Text = _settings.CardHeight.ToString();
            if (_miniIdleLabel != null)
                _miniIdleLabel.Text = $"{(int)Math.Round(_settings.IdleOpacity * 100)}%";
            if (_miniActiveLabel != null)
                _miniActiveLabel.Text = $"{(int)Math.Round(_settings.ActiveOpacity * 100)}%";
        }

        private void ChangeSetting(System.Action mutate)
        {
            if (_settings == null) return;
            mutate();
            _settings.Save();
            DeckTrackerInjectionPatch.ApplySettings(_settings);
        }

        private Control CreateMiniSettingsPanel()
        {
            var bg = new Panel();
            bg.AnchorLeft = 1f; bg.AnchorRight = 1f;
            bg.AnchorTop = 0f; bg.AnchorBottom = 0f;
            bg.OffsetLeft = 4f; bg.OffsetRight = 204f;
            bg.OffsetTop = 0f; bg.OffsetBottom = 140f;
            bg.ZIndex = 20;
            bg.Visible = false;
            bg.MouseFilter = MouseFilterEnum.Stop;
            var style = new StyleBoxFlat { BgColor = new Color(0.08f, 0.08f, 0.08f, 0.92f) };
            style.SetCornerRadiusAll(4);
            bg.AddThemeStyleboxOverride("panel", style);

            var margin = new MarginContainer();
            margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            margin.AddThemeConstantOverride("margin_left", 8);
            margin.AddThemeConstantOverride("margin_right", 8);
            margin.AddThemeConstantOverride("margin_top", 6);
            margin.AddThemeConstantOverride("margin_bottom", 6);
            margin.MouseFilter = MouseFilterEnum.Pass;
            bg.AddChild(margin);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 4);
            vbox.MouseFilter = MouseFilterEnum.Pass;
            margin.AddChild(vbox);

            var (widthLabel, widthRow) = MakeMiniRow("Card W",
                () => ChangeSetting(() => _settings.CardWidthInt = Math.Max(50, _settings.CardWidth - 10)),
                () => ChangeSetting(() => _settings.CardWidthInt = Math.Min(800, _settings.CardWidth + 10)));
            _miniCardWidthLabel = widthLabel;
            vbox.AddChild(widthRow);

            var (heightLabel, heightRow) = MakeMiniRow("Card H",
                () => ChangeSetting(() => _settings.CardHeightInt = Math.Max(16, _settings.CardHeight - 1)),
                () => ChangeSetting(() => _settings.CardHeightInt = Math.Min(120, _settings.CardHeight + 1)));
            _miniCardHeightLabel = heightLabel;
            vbox.AddChild(heightRow);

            var (idleLabel, idleRow) = MakeMiniRow("Idle %",
                () => ChangeSetting(() => _settings.IdleOpacity = (float)Math.Round(Math.Max(0.0, _settings.IdleOpacity - 0.05), 2)),
                () => ChangeSetting(() => _settings.IdleOpacity = (float)Math.Round(Math.Min(1.0, _settings.IdleOpacity + 0.05), 2)));
            _miniIdleLabel = idleLabel;
            vbox.AddChild(idleRow);

            var (activeLabel, activeRow) = MakeMiniRow("Active %",
                () => ChangeSetting(() => _settings.ActiveOpacity = (float)Math.Round(Math.Max(0.0, _settings.ActiveOpacity - 0.05), 2)),
                () => ChangeSetting(() => _settings.ActiveOpacity = (float)Math.Round(Math.Min(1.0, _settings.ActiveOpacity + 0.05), 2)));
            _miniActiveLabel = activeLabel;
            vbox.AddChild(activeRow);

            return bg;
        }

        private static (Label, HBoxContainer) MakeMiniRow(string labelText, System.Action onMinus, System.Action onPlus)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 4);
            row.MouseFilter = MouseFilterEnum.Pass;

            var lbl = new Label { Text = labelText, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            lbl.AddThemeFontSizeOverride("font_size", 14);
            lbl.MouseFilter = MouseFilterEnum.Ignore;
            row.AddChild(lbl);

            var minusBtn = new Button { Text = "−", CustomMinimumSize = new Vector2(24, 22), FocusMode = FocusModeEnum.None };
            minusBtn.AddThemeFontSizeOverride("font_size", 14);
            minusBtn.Pressed += onMinus;
            row.AddChild(minusBtn);

            var valueLabel = new Label
            {
                CustomMinimumSize = new Vector2(44, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            valueLabel.AddThemeFontSizeOverride("font_size", 14);
            valueLabel.MouseFilter = MouseFilterEnum.Ignore;
            row.AddChild(valueLabel);

            var plusBtn = new Button { Text = "+", CustomMinimumSize = new Vector2(24, 22), FocusMode = FocusModeEnum.None };
            plusBtn.AddThemeFontSizeOverride("font_size", 14);
            plusBtn.Pressed += onPlus;
            row.AddChild(plusBtn);

            return (valueLabel, row);
        }

        private static Color GetTitleOutlineColorByRarity(CardRarity rarity)
        {
            return rarity switch
            {
                CardRarity.None => StsColors.cardTitleOutlineCommon,
                CardRarity.Basic => StsColors.cardTitleOutlineCommon,
                CardRarity.Common => StsColors.cardTitleOutlineCommon,
                CardRarity.Token => StsColors.cardTitleOutlineCommon,
                CardRarity.Uncommon => StsColors.cardTitleOutlineUncommon,
                CardRarity.Rare => StsColors.cardTitleOutlineRare,
                CardRarity.Curse => StsColors.cardTitleOutlineCurse,
                CardRarity.Quest => StsColors.cardTitleOutlineQuest,
                CardRarity.Status => StsColors.cardTitleOutlineStatus,
                CardRarity.Event => StsColors.cardTitleOutlineSpecial,
                CardRarity.Ancient => StsColors.cardTitleOutlineCommon,
                _ => StsColors.cardTitleOutlineCommon
            };
        }
    }
}
