using Godot;
using System;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Localization.Fonts;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Helpers.Models;
using MegaCrit.Sts2.Core.Context;

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

        public void SetPileType(PileType pileType)
        {
            _pileType = pileType;
        }

        public void SetSettings(ModSettings settings)
        {
            _settings = settings;
            _targetOpacity = settings?.IdleOpacity ?? 0.3f;
            _currentOpacity = _targetOpacity;
            _idleDelaySeconds = settings?.IdleDelaySeconds ?? 2.0f;
            Modulate = new Color(1, 1, 1, _currentOpacity);
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
            CustomMinimumSize = new Vector2(250, 400);
            Size = new Vector2(250, 400);
            MouseFilter = MouseFilterEnum.Ignore;
            SetAnchorsPreset(LayoutPreset.TopLeft);

            var emptyStyle = new StyleBoxEmpty();
            AddThemeStyleboxOverride("panel", emptyStyle);

            var mainContainer = new VBoxContainer();
            mainContainer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            mainContainer.AddThemeConstantOverride("separation", 5);
            AddChild(mainContainer);

            var marginContainer = new MarginContainer();
            marginContainer.AddThemeConstantOverride("margin_left", 10);
            marginContainer.AddThemeConstantOverride("margin_right", 10);
            marginContainer.AddThemeConstantOverride("margin_top", 10);
            marginContainer.AddThemeConstantOverride("margin_bottom", 10);
            mainContainer.AddChild(marginContainer);

            var innerContainer = new VBoxContainer();
            innerContainer.AddThemeConstantOverride("separation", 8);
            marginContainer.AddChild(innerContainer);

            _cardList = new VBoxContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill,
                Theme = new Theme()
            };
            _cardList.AddThemeConstantOverride("separation", 3);
            innerContainer.AddChild(_cardList);

            SetMouseIgnoreRecursive(this);
        }

        private void OnPileContentsChanged()
        {
            if (_currentPile != null)
            {
                if (_pileType == PileType.Draw)
                    TopCardTracker.PruneCards(_currentPile);
                UpdateCardList(_cardList, _currentPile);
                _targetOpacity = _settings?.ActiveOpacity ?? 1.0f;
                _timeSinceLastChange = 0f;
            }
        }

        public override void _Process(double delta)
        {
            if (Math.Abs(_currentOpacity - _targetOpacity) > 0.01f)
            {
                _currentOpacity = Mathf.Lerp(_currentOpacity, _targetOpacity, OpacityTransitionSpeed * (float)delta);
                Modulate = new Color(1, 1, 1, _currentOpacity);
            }

            if (CombatManager.Instance == null || !CombatManager.Instance.IsInProgress)
            {
                if (_currentPile != null)
                {
                    _currentPile.ContentsChanged -= OnPileContentsChanged;
                    _currentPile = null;
                }
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
                if (_currentPile != null)
                    _currentPile.ContentsChanged -= OnPileContentsChanged;

                _currentPile = pile;
                _currentPile.ContentsChanged += OnPileContentsChanged;
                UpdateCardList(_cardList, _currentPile);
            }

            _timeSinceLastChange += (float)delta;
            if (_timeSinceLastChange >= _idleDelaySeconds || (_currentPile != null && _currentPile.IsEmpty))
                _targetOpacity = _settings?.IdleOpacity ?? 0.3f;
        }

        private void UpdateCardList(VBoxContainer container, CardPile pile)
        {
            if (container == null || pile == null)
                return;

            foreach (Node child in container.GetChildren())
            {
                container.RemoveChild(child);
                child.QueueFree();
            }

            if (pile.Cards.Count == 0)
                return;

            var cardGroups = new System.Collections.Generic.Dictionary<string, (CardModel card, int count)>();
            var currentCardKeys = new System.Collections.Generic.HashSet<string>();

            foreach (var card in pile.Cards)
            {
                string enchantmentKey = card.Enchantment != null ? card.Enchantment.GetType().Name : "none";
                string key = $"{card.Title}|{card.IsUpgraded}|{enchantmentKey}";
                currentCardKeys.Add(key);

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
                    var temp = displayGroups[i];
                    displayGroups[i] = displayGroups[j];
                    displayGroups[j] = temp;
                }
                _shuffledOrder = displayGroups;
            }
            else
            {
                displayGroups = new System.Collections.Generic.List<(CardModel card, int count)>();
                var remainingCards = new System.Collections.Generic.Dictionary<string, (CardModel card, int count)>(cardGroups);

                foreach (var oldGroup in _shuffledOrder)
                {
                    string key = $"{oldGroup.card.Title}|{oldGroup.card.IsUpgraded}|{(oldGroup.card.Enchantment != null ? oldGroup.card.Enchantment.GetType().Name : "none")}";
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

            foreach (var group in displayGroups)
            {
                var card = group.card;
                var count = group.count;

                if (count == 0)
                    continue;

                try
                {
                    var portrait = card.Portrait;
                    if (portrait != null)
                    {
                        int cardHeight = _settings?.CardHeight ?? 32;
                        int cardWidth = _settings?.CardWidth ?? 200;
                        int cardImageWidth = _settings?.CardImageWidth ?? 175;

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
                            Position = new Vector2(0, -portrait.GetHeight() / 4),
                            Size = new Vector2(cardImageWidth, portrait.GetHeight() * (float)cardImageWidth / portrait.GetWidth()),
                            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                            StretchMode = TextureRect.StretchModeEnum.KeepAspect,
                            MouseFilter = Control.MouseFilterEnum.Ignore
                        };
                        clipContainer.AddChild(textureRect);

                        if (card.Enchantment != null)
                        {
                            try
                            {
                                var enchantIconPath = card.Enchantment.IntendedIconPath;
                                if (!string.IsNullOrEmpty(enchantIconPath))
                                {
                                    var enchantIcon = ResourceLoader.Load<Texture2D>(enchantIconPath);
                                    if (enchantIcon != null)
                                    {
                                        int enchantIconSize = cardHeight - 6;
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
                            }
                            catch (Exception ex)
                            {
                                GD.PrintErr($"[CardListPanel] Error adding enchantment icon: {ex.Message}");
                            }
                        }

                        var countLabel = new Label
                        {
                            Text = count.ToString(),
                            VerticalAlignment = VerticalAlignment.Center,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Position = new Vector2(0, 0),
                            Size = new Vector2(cardHeight, cardHeight)
                        };
                        int countFontSize = _settings?.CardCountFontSize ?? 28;
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
                        clipContainer.AddChild(countLabel);

                        var nameLabel = new Label();
                        nameLabel.Text = GetCardDisplayName(card);
                        int nameFontSize = _settings?.CardNameFontSize ?? 24;
                        nameLabel.AddThemeFontSizeOverride("font_size", nameFontSize);

                        Color titleColor;
                        Color titleOutlineColor;

                        if (card.Enchantment != null)
                        {
                            titleColor = new Color(0.85f, 0.6f, 1f, 1f);
                            titleOutlineColor = new Color(0.3f, 0.05f, 0.45f, 1f);
                        }
                        else if (card.CurrentUpgradeLevel == 0)
                        {
                            titleColor = StsColors.cream;
                            titleOutlineColor = GetTitleOutlineColorByRarity(card.Rarity);
                        }
                        else
                        {
                            titleColor = StsColors.green;
                            titleOutlineColor = StsColors.cardTitleOutlineSpecial;
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
                        nameLabel.VerticalAlignment = VerticalAlignment.Center;
                        nameLabel.Size = new Vector2(cardImageWidth, cardHeight);
                        nameLabel.Position = new Vector2(countFontSize + 2, 0);
                        clipContainer.AddChild(nameLabel);
                        cardRowContainer.AddChild(clipContainer);

                        try
                        {
                            var energyIcon = card.EnergyIcon;
                            if (energyIcon != null)
                            {
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

                                if (showIcon)
                                {
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
                                    cardRowContainer.AddChild(energyCostContainer);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            GD.PrintErr($"[CardListPanel] Error loading energy icon: {ex.Message}");
                        }

                        try
                        {
                            int starCost = card.GetStarCostWithModifiers();
                            if (card.HasStarCostX || starCost >= 0)
                            {
                                var starIcon = ResourceLoader.Load<Texture2D>("res://images/packed/sprite_fonts/star_icon.png");
                                if (starIcon != null)
                                {
                                    string starCostText = card.HasStarCostX ? "X" : starCost.ToString();
                                    bool showStarIcon = card.HasStarCostX || starCost >= 0;

                                    if (showStarIcon)
                                    {
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
                                        cardRowContainer.AddChild(starCostContainer);
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            GD.PrintErr($"[CardListPanel] Error loading star icon: {ex.Message}");
                        }

                        container.AddChild(cardRowContainer);
                    }
                    else
                    {
                        var label = new Label();
                        label.Text = GetCardDisplayName(card);
                        label.AddThemeFontSizeOverride("font_size", 12);
                        container.AddChild(label);
                    }
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"[CardListPanel] Error loading card portrait: {ex.Message}");
                    var label = new Label();
                    label.Text = GetCardDisplayName(card);
                    label.AddThemeFontSizeOverride("font_size", 12);
                    container.AddChild(label);
                }
            }

            SetMouseIgnoreRecursive(container);
        }

        private static string GetCardDisplayName(CardModel card)
        {
            return card.IsUpgraded ? $"{card.Title}" : card.Title;
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
