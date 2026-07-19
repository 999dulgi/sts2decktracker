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
using MegaCrit.Sts2.Core.Nodes.Rooms;

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
        private MegaCrit.Sts2.Core.Entities.Creatures.Creature _subscribedCreature = null;
        private MegaCrit.Sts2.Core.Entities.Players.PlayerCombatState _subscribedCombatState = null;
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
        private bool _pileDirty = false;
        private readonly System.Collections.Generic.HashSet<CardModel> _pileCardSet = new();
        private readonly System.Collections.Generic.Dictionary<string, RowView> _rowsByKey = new();
        // GroupKey(card) 결과를 카드별로 캐싱한다. 카드 자체 상태 변화는 그 카드 캐시만 지우고,
        // 코스트/키워드 계산에 쓰이는 훅 자체가 바뀔 수 있는 이벤트는 캐시 전체를 지운다.
        private readonly System.Collections.Generic.Dictionary<CardModel, string> _groupKeyCache = new();
        private static readonly System.Collections.Generic.Dictionary<string, PackedScene> _afflictionEffectCache = new();


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

            CardStateBridge.CardStateChanged -= OnCardStateChanged;
            if (_currentPile != null)
            {
                _currentPile.CardAdded -= OnCardAdded;
                _currentPile.CardRemoved -= OnCardRemoved;
            }
            UnsubscribeCreaturePowerEvents();
            UnsubscribeEnergyEvents();
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
                UpdateDynamicHeight();
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
            }
            else
            {
                _scrollContainer.CustomMinimumSize = Vector2.Zero;
                _scrollContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
                if (_cardList != null) _cardList.SizeFlagsVertical = SizeFlags.ExpandFill;
            }
        }

        private void UpdateDynamicHeight()
        {
            if (_cardList == null) return;
            float contentH = _cardList.GetMinimumSize().Y;
            float panelH;

            if (_settings?.Scrollable ?? false)
            {
                float maxH = (_settings?.ScrollableAutoHeight ?? true)
                    ? Mathf.Max(50f, ScrollableBottomY - GlobalPosition.Y)
                    : Mathf.Max(50f, _settings?.ScrollableHeight ?? 200f);
                float cappedH = Mathf.Min(contentH, maxH);
                if (_scrollContainer != null)
                    _scrollContainer.CustomMinimumSize = new Vector2(0, cappedH);
                panelH = cappedH;
            }
            else
            {
                panelH = contentH;
            }

            panelH = panelH > 0 ? panelH + 20 : 0;

            if (!Mathf.IsEqualApprox(CustomMinimumSize.Y, panelH))
            {
                CustomMinimumSize = new Vector2(CustomMinimumSize.X, panelH);
                Size = new Vector2(Size.X, panelH);
            }
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
            CardStateBridge.CardStateChanged += OnCardStateChanged;

            var pw = _settings?.PanelWidth ?? 312;
            CustomMinimumSize = new Vector2(pw, 0);
            Size = new Vector2(pw, 0);
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
                if (_currentPile != null)
                {
                    _currentPile.CardAdded -= OnCardAdded;
                    _currentPile.CardRemoved -= OnCardRemoved;
                }
                UnsubscribeCreaturePowerEvents();
                UnsubscribeEnergyEvents();
                _pileCardSet.Clear();
                _groupKeyCache.Clear();
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

                SubscribeCreaturePowerEvents(_currentPlayer.Creature);
                SubscribeEnergyEvents(_currentPlayer.PlayerCombatState);
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

            bool intentTransparency = _settings?.IntentTransparency ?? false;

            if (_currentPile != pile)
            {
                if (_currentPile != null)
                {
                    _currentPile.CardAdded -= OnCardAdded;
                    _currentPile.CardRemoved -= OnCardRemoved;
                }

                _currentPile = pile;
                _currentPile.CardAdded += OnCardAdded;
                _currentPile.CardRemoved += OnCardRemoved;

                UpdateCardList(_cardList, _currentPile);
                UpdateDynamicHeight();
                if (!intentTransparency)
                {
                    _targetOpacity = _settings?.ActiveOpacity ?? 1.0f;
                    _timeSinceLastChange = 0f;
                }
            }
            else if (_currentPile != null && _pileDirty)
            {
                _pileDirty = false;
                if (_pileType == PileType.Draw)
                    TopCardTracker.PruneCards(_currentPile);
                UpdateCardList(_cardList, _currentPile);
                UpdateDynamicHeight();
                if (!intentTransparency)
                {
                    _targetOpacity = _settings?.ActiveOpacity ?? 1.0f;
                    _timeSinceLastChange = 0f;
                }
            }

            if (intentTransparency)
            {
                _targetOpacity = IntentOverlapHelper.IsOverlappingEnemyCreature(this)
                    ? (_settings?.IdleOpacity ?? 0.3f)
                    : (_settings?.ActiveOpacity ?? 1.0f);
            }
            else
            {
                _timeSinceLastChange += (float)delta;
                if (_timeSinceLastChange >= _idleDelaySeconds || (_currentPile != null && _currentPile.IsEmpty))
                    _targetOpacity = _settings?.IdleOpacity ?? 0.3f;
            }
        }

        private void UpdateCardList(VBoxContainer container, CardPile pile)
        {
            if (container == null || pile == null)
                return;

            if (_hoveredCard != null && _hoveredClip != null && GodotObject.IsInstanceValid(_hoveredClip))
                NHoverTipSet.Remove(_hoveredClip);
            _hoveredCard = null;
            _hoveredClip = null;

            // 증분 경로(OnCardAdded/OnCardRemoved)가 놓친 게 있어도 여기서 파일 실제 내용과 다시 맞춘다.
            _pileCardSet.Clear();
            foreach (var c in pile.Cards)
                _pileCardSet.Add(c);

            var cardGroups = new System.Collections.Generic.Dictionary<string, (CardModel card, int count)>();
            foreach (var card in pile.Cards)
            {
                string key = CachedGroupKey(card);
                if (cardGroups.TryGetValue(key, out var existing))
                    cardGroups[key] = (existing.card, existing.count + 1);
                else
                    cardGroups[key] = (card, 1);
            }

            System.Collections.Generic.List<(CardModel card, int count)> displayGroups;
            if (_shuffledOrder == null || _shuffledOrder.Count == 0)
            {
                displayGroups = new System.Collections.Generic.List<(CardModel card, int count)>(cardGroups.Values);
                switch (_settings?.CardSortMode ?? CardSortMode.Random)
                {
                    case CardSortMode.Alphabetical:
                        displayGroups.Sort((a, b) => string.Compare(a.card.Title, b.card.Title, StringComparison.OrdinalIgnoreCase));
                        break;
                    case CardSortMode.Cost:
                        displayGroups.Sort((a, b) =>
                        {
                            int cmp = GetSortEnergy(a.card).CompareTo(GetSortEnergy(b.card));
                            return cmp != 0 ? cmp : string.Compare(a.card.Title, b.card.Title, StringComparison.OrdinalIgnoreCase);
                        });
                        break;
                    default:
                        var random = new System.Random();
                        for (int i = displayGroups.Count - 1; i > 0; i--)
                        {
                            int j = random.Next(i + 1);
                            (displayGroups[i], displayGroups[j]) = (displayGroups[j], displayGroups[i]);
                        }
                        break;
                }
                _shuffledOrder = displayGroups;
            }
            else
            {
                displayGroups = new System.Collections.Generic.List<(CardModel card, int count)>();
                var remainingCards = new System.Collections.Generic.Dictionary<string, (CardModel card, int count)>(cardGroups);

                foreach (var oldGroup in _shuffledOrder)
                {
                    string key = CachedGroupKey(oldGroup.card);
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
                if (g.count > 0) desiredKeys.Add(CachedGroupKey(g.card));
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
                string key = CachedGroupKey(group.card);

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

        private const string RetainIconPath = "res://images/powers/retain_hand_power.png";
        private const string SlyIconPath = "res://images/packed/combat_ui/discard_pile.png";
        private const string EtherealIconPath = "res://images/powers/slippery_power.png";
        private const string ReplayIconPath = "res://images/packed/modifiers/binary.png";

        // 전투 중 후천적으로 얻은 키워드(다른 모델이 임시로 부여했거나 단발성 Retain/Sly)가 있는지.
        // 인챈트/카드 자체 효과로 붙은 키워드는 여기 포함 안 됨 (그건 이미 인챈트 아이콘으로 표시됨).
        internal static bool HasTemporaryKeyword(CardModel card) => GetTemporaryKeywordIconPath(card) != null;

        // 어떤 키워드가 원인인지에 따라 다른 아이콘 경로를 반환. 여러 개가 겹치면 보존 > 교활 > 휘발성 > 재사용 순.
        internal static string GetTemporaryKeywordIconPath(CardModel card)
        {
            // card.Keywords는 호출마다 파워/유물 훅을 전부 순회하는 무거운 연산이라 한 번만 계산해서 재사용한다.
            var local = card.GetKeywordsWithSources(KeywordSources.Local);
            var all = card.Keywords;

            bool grantedByEffect(CardKeyword kw) =>
                (all.Contains(kw) && !local.Contains(kw))                       // Global (파워 등이 전투 중 임시 부여)
                || (local.Contains(kw) && AppliedKeywordTrackerPatch.WasAppliedByEffect(card, kw)); // CardCmd.ApplyKeyword로 부여

            if (card.ShouldRetainThisTurn && !all.Contains(CardKeyword.Retain))
                return RetainIconPath;
            if (grantedByEffect(CardKeyword.Retain))
                return RetainIconPath;

            if (card.IsSlyThisTurn && !all.Contains(CardKeyword.Sly))
                return SlyIconPath;
            if (grantedByEffect(CardKeyword.Sly))
                return SlyIconPath;

            if (grantedByEffect(CardKeyword.Ethereal))
                return EtherealIconPath;

            // Keyword가 아닌 별도 시스템(BaseReplayCount)이지만 같은 부류: Transfigure/SoldiersStew가
            // 전투 중에만 ++로 부여하고, 카드 자체 설계로 처음부터 갖고 있는 카드는 없음.
            if (card.BaseReplayCount > 0)
                return ReplayIconPath;

            return null;
        }

        private static int GetSortEnergy(CardModel card)
        {
            return card.EnergyCost.CostsX ? int.MaxValue : card.EnergyCost.GetWithModifiers(CostModifiers.All);
        }

        private static string GroupKey(CardModel card)
        {
            // 이 함수가 예외를 던지면 OnCardAdded/OnCardRemoved가 행 정리 로직에 도달하기 전에 중단되고
            // _pileDirty도 세팅되지 않아, 파일에서 이미 빠진 카드의 행이 패널에 영구히 남는다 (예: 방금
            // 생성된 토큰 카드가 아직 일부 필드가 완전히 준비되지 않은 짧은 순간에 호출되는 경우).
            // 다른 모든 렌더링 헬퍼처럼 여기도 방어적으로 감싸고, 실패 시에도 최소한 카드별로 구분되는
            // 키를 반환해 행이 유령처럼 남지 않게 한다.
            try
            {
                string enchantmentKey = card.Enchantment != null ? card.Enchantment.GetType().Name : "none";
                string afflictionKey = card.Affliction != null ? card.Affliction.Id.Entry : "none";
                bool hasTemporaryKeyword = HasTemporaryKeyword(card);
                int energy = card.EnergyCost.CostsX ? int.MinValue : card.EnergyCost.GetWithModifiers(CostModifiers.All);
                int star = card.HasStarCostX ? int.MinValue : card.GetStarCostWithModifiers();
                return $"{card.Title}|{card.IsUpgraded}|{enchantmentKey}|{afflictionKey}|{hasTemporaryKeyword}|{energy}|{star}";
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[CardListPanel] GroupKey failed for '{card.Title}': {ex}");
                return $"{card.Title}|error|{card.GetHashCode()}";
            }
        }

        private string CachedGroupKey(CardModel card)
        {
            if (_groupKeyCache.TryGetValue(card, out var cached))
                return cached;
            string key = GroupKey(card);
            _groupKeyCache[card] = key;
            return key;
        }

        // Looks up res://sts2decktracker/effects/<affliction id>.tscn, e.g. Hexed -> hexed.tscn.
        // Not every affliction has an effect scene built yet, so a cached null is expected/normal.
        internal static PackedScene GetAfflictionEffectScene(AfflictionModel affliction)
        {
            string key = affliction.Id.Entry.ToLowerInvariant();
            if (_afflictionEffectCache.TryGetValue(key, out var cached))
                return cached;

            string path = $"res://sts2decktracker/effects/{key}.tscn";
            PackedScene scene = ResourceLoader.Exists(path) ? ResourceLoader.Load<PackedScene>(path) : null;
            _afflictionEffectCache[key] = scene;
            return scene;
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

                var colorMode = _settings?.CardColorMode ?? CardColorMode.Full;
                var (titleColor, titleOutlineColor) = CardColorHelper.GetTitleColors(card, colorMode);

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

                // 오른쪽 끝부터 배지(인챈트/임시 키워드)를 차례로 배치하는 커서. 배지를 하나 놓을 때마다 왼쪽으로 당김.
                float badgeCursorRight = cardImageWidth - 4;
                int badgeIconSize = cardHeight - 6;

                if (card.Enchantment != null)
                {
                    try
                    {
                        string enchantPath = card.Enchantment.IntendedIconPath;
                        var enchantIcon = string.IsNullOrEmpty(enchantPath) ? null : ResourceLoader.Load<Texture2D>(enchantPath);
                        if (enchantIcon != null)
                        {
                            var enchantIconRect = new TextureRect
                            {
                                Texture = enchantIcon,
                                Position = new Vector2(badgeCursorRight - badgeIconSize, 2),
                                CustomMinimumSize = new Vector2(badgeIconSize, badgeIconSize),
                                ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional,
                                StretchMode = TextureRect.StretchModeEnum.KeepAspect,
                                MouseFilter = Control.MouseFilterEnum.Ignore
                            };
                            enchantIconRect.Modulate = new Color(1.5f, 1.3f, 1.8f, 1.0f);
                            clipContainer.AddChild(enchantIconRect);
                            badgeCursorRight -= badgeIconSize + 2;
                        }
                    }
                    catch (Exception ex)
                    {
                        GD.PrintErr($"[CardListPanel] Error adding enchantment icon: {ex.Message}");
                    }
                }

                string tempKeywordIconPath = GetTemporaryKeywordIconPath(card);
                if (tempKeywordIconPath != null)
                {
                    try
                    {
                        var tempKeywordIcon = ResourceLoader.Exists(tempKeywordIconPath)
                            ? ResourceLoader.Load<Texture2D>(tempKeywordIconPath)
                            : null;
                        if (tempKeywordIcon != null)
                        {
                            var tempKeywordIconRect = new TextureRect
                            {
                                Texture = tempKeywordIcon,
                                Position = new Vector2(badgeCursorRight - badgeIconSize, 2),
                                CustomMinimumSize = new Vector2(badgeIconSize, badgeIconSize),
                                ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional,
                                StretchMode = TextureRect.StretchModeEnum.KeepAspect,
                                MouseFilter = Control.MouseFilterEnum.Ignore
                            };
                            clipContainer.AddChild(tempKeywordIconRect);
                            badgeCursorRight -= badgeIconSize + 2;
                        }
                    }
                    catch (Exception ex)
                    {
                        GD.PrintErr($"[CardListPanel] Error adding temporary keyword icon: {ex.Message}");
                    }
                }

                float reservedForBadges = (cardImageWidth - 4) - badgeCursorRight;
                if (reservedForBadges > 0)
                {
                    nameLabel.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
                    nameLabel.CustomMinimumSize = new Vector2(cardImageWidth - reservedForBadges - cardHeight, 0);
                    nameLabel.ClipText = true;
                }

                if (card.Affliction != null)
                {
                    try
                    {
                        var afflictionScene = GetAfflictionEffectScene(card.Affliction);
                        if (afflictionScene != null)
                        {
                            var afflictionInstance = afflictionScene.Instantiate<Control>();
                            afflictionInstance.MouseFilter = Control.MouseFilterEnum.Ignore;
                            clipContainer.AddChild(afflictionInstance);

                            afflictionInstance.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
                            // Keep the text on top of the effect instead of getting drawn over.
                            clipContainer.MoveChild(labelRow, clipContainer.GetChildCount() - 1);
                        }
                    }
                    catch (Exception ex)
                    {
                        GD.PrintErr($"[CardListPanel] Error adding affliction effect: {ex.Message}");
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

        // CardStateBridge는 전역으로 발동하므로 _pileCardSet으로 걸러서, 이 패널이 보여주는
        // 파일에 속한 카드가 바뀔 때만 dirty를 세운다.
        private void OnCardStateChanged(CardModel card)
        {
            _groupKeyCache.Remove(card);
            if (_pileCardSet.Contains(card))
                _pileDirty = true;
        }

        // 카드 한 장의 추가/제거를 그 카드가 속한 그룹의 카운트만 증감시키는 증분 방식으로 처리한다.
        // _shuffledOrder/_rowsByKey는 그룹 수만큼만 순회한다.
        private void OnCardAdded(CardModel card)
        {
            _pileCardSet.Add(card);

            if (_currentPile == null || _shuffledOrder == null || _shuffledOrder.Count == 0)
            {
                _pileDirty = true;
                return;
            }

            string key = CachedGroupKey(card);
            int idx = FindShuffledOrderIndex(key);
            if (idx >= 0)
            {
                var (existingCard, count) = _shuffledOrder[idx];
                int newCount = count + 1;
                _shuffledOrder[idx] = (existingCard, newCount);
                if (_rowsByKey.TryGetValue(key, out var row) && row.CountLabel != null)
                    row.CountLabel.Text = newCount.ToString();
            }
            else
            {
                _shuffledOrder.Add((card, 1));
                var row = BuildCardRow(card, 1);
                if (row != null)
                {
                    _cardList.AddChild(row.Root);
                    _rowsByKey[key] = row;
                    if (row.ClipContainer != null && GodotObject.IsInstanceValid(row.ClipContainer))
                    {
                        var hoverTips = new System.Collections.Generic.List<IHoverTip> { new CardHoverTip(card) };
                        hoverTips.AddRange(card.HoverTips);
                        _cardHoverData.Add((card, row.ClipContainer, hoverTips));
                    }
                }
            }
            UpdateDynamicHeight();
        }

        private void OnCardRemoved(CardModel card)
        {
            _pileCardSet.Remove(card);

            if (_pileType == PileType.Draw)
                TopCardTracker.Untrack(card);

            if (_currentPile == null || _shuffledOrder == null || _shuffledOrder.Count == 0)
            {
                _pileDirty = true;
                return;
            }

            string key = CachedGroupKey(card);
            int idx = FindShuffledOrderIndex(key);
            if (idx < 0)
            {
                // 증분 상태가 실제 파일과 어긋난 경우를 대비한 안전망 — 전체 재계산으로 복구.
                _pileDirty = true;
                return;
            }

            var (existingCard, count) = _shuffledOrder[idx];
            if (count <= 1)
            {
                _shuffledOrder.RemoveAt(idx);
                if (_rowsByKey.TryGetValue(key, out var row))
                {
                    if (row.ClipContainer != null)
                        _cardHoverData.RemoveAll(t => t.clip == row.ClipContainer);
                    if (GodotObject.IsInstanceValid(row.Root))
                    {
                        _cardList.RemoveChild(row.Root);
                        row.Root.QueueFree();
                    }
                    _rowsByKey.Remove(key);
                }
            }
            else
            {
                int newCount = count - 1;
                _shuffledOrder[idx] = (existingCard, newCount);
                if (_rowsByKey.TryGetValue(key, out var row) && row.CountLabel != null)
                    row.CountLabel.Text = newCount.ToString();
            }
            UpdateDynamicHeight();
        }

        private int FindShuffledOrderIndex(string key)
        {
            for (int i = 0; i < _shuffledOrder.Count; i++)
                if (CachedGroupKey(_shuffledOrder[i].card) == key)
                    return i;
            return -1;
        }

        // 파워 감지
        private void SubscribeCreaturePowerEvents(MegaCrit.Sts2.Core.Entities.Creatures.Creature creature)
        {
            if (creature == null || _subscribedCreature == creature) return;
            UnsubscribeCreaturePowerEvents();
            _subscribedCreature = creature;
            _subscribedCreature.PowerApplied += OnCreaturePowerApplied;
            _subscribedCreature.PowerIncreased += OnCreaturePowerIncreased;
            _subscribedCreature.PowerDecreased += OnCreaturePowerDecreased;
            _subscribedCreature.PowerRemoved += OnCreaturePowerRemoved;
        }

        private void UnsubscribeCreaturePowerEvents()
        {
            if (_subscribedCreature == null) return;
            _subscribedCreature.PowerApplied -= OnCreaturePowerApplied;
            _subscribedCreature.PowerIncreased -= OnCreaturePowerIncreased;
            _subscribedCreature.PowerDecreased -= OnCreaturePowerDecreased;
            _subscribedCreature.PowerRemoved -= OnCreaturePowerRemoved;
            _subscribedCreature = null;
        }

        // 코스트/키워드 계산 훅을 오버라이드하는 파워일 때만 캐시 전체를 지운다.
        private void OnCreaturePowerApplied(PowerModel power) => MaybeInvalidateGroupKeyCache(power);
        private void OnCreaturePowerIncreased(PowerModel power, int change, bool silent) => MaybeInvalidateGroupKeyCache(power);
        private void OnCreaturePowerDecreased(PowerModel power, bool silent) => MaybeInvalidateGroupKeyCache(power);
        private void OnCreaturePowerRemoved(PowerModel power) => MaybeInvalidateGroupKeyCache(power);

        private static readonly System.Collections.Generic.Dictionary<Type, bool> _powerAffectsGroupKeyCache = new();

        private static bool PowerAffectsGroupKey(PowerModel power)
        {
            var type = power.GetType();
            if (_powerAffectsGroupKeyCache.TryGetValue(type, out var cached))
                return cached;

            bool affects = OverridesAbstractModelMethod(type, nameof(AbstractModel.TryModifyEnergyCostInCombat))
                || OverridesAbstractModelMethod(type, nameof(AbstractModel.TryModifyEnergyCostInCombatLate))
                || OverridesAbstractModelMethod(type, nameof(AbstractModel.TryModifyStarCost))
                || OverridesAbstractModelMethod(type, nameof(AbstractModel.TryModifyKeywordsInCombat));

            _powerAffectsGroupKeyCache[type] = affects;
            return affects;
        }

        private static bool OverridesAbstractModelMethod(Type type, string methodName)
        {
            var method = type.GetMethod(methodName);
            return method != null && method.DeclaringType != typeof(AbstractModel);
        }

        private void MaybeInvalidateGroupKeyCache(PowerModel power)
        {
            if (!PowerAffectsGroupKey(power))
                return; // 바리케이드처럼 코스트/키워드와 무관한 파워는 캐시를 건드릴 필요가 없다.
            _groupKeyCache.Clear();
            _pileDirty = true;
        }

        // CardCostHelper.GetEnergyCostColor는 파일 종류와 무관하게 현재 Energy/Stars 자원량과 코스트를
        // 비교해 "자원 부족(빨강)" 색을 결정한다. 카드/파워 이벤트로는 안 잡히므로 자원 변화 자체를 구독한다.
        private void SubscribeEnergyEvents(MegaCrit.Sts2.Core.Entities.Players.PlayerCombatState combatState)
        {
            if (combatState == null || _subscribedCombatState == combatState) return;
            UnsubscribeEnergyEvents();
            _subscribedCombatState = combatState;
            _subscribedCombatState.EnergyChanged += OnResourceChanged;
            _subscribedCombatState.StarsChanged += OnResourceChanged;
        }

        private void UnsubscribeEnergyEvents()
        {
            if (_subscribedCombatState == null) return;
            _subscribedCombatState.EnergyChanged -= OnResourceChanged;
            _subscribedCombatState.StarsChanged -= OnResourceChanged;
            _subscribedCombatState = null;
        }

        private void OnResourceChanged(int newValue, int oldValue) => _pileDirty = true;

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
    }
}
