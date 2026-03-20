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
    public partial class TopCardPanel : Panel
    {
        private ModSettings _settings;
        private CardPile _currentPile = null;
        private MegaCrit.Sts2.Core.Entities.Players.Player _currentPlayer = null;
        private bool _combatStartLogged = false;
        private float _targetOpacity = 0.3f;
        private float _currentOpacity = 0.3f;
        private const float OpacityTransitionSpeed = 5.0f;
        private float _timeSinceLastChange = 0f;
        private float _idleDelaySeconds = 2.0f;

        private Label _headerLabel;
        private HBoxContainer _cardRow;
        private static Font _KreonRegularFont;
        private static Font KreonRegular => _KreonRegularFont ??= ResourceLoader.Load<Font>("res://fonts/kreon_regular.ttf");

        public void SetSettings(ModSettings settings)
        {
            _settings = settings;
            _targetOpacity = settings?.IdleOpacity ?? 0.3f;
            _currentOpacity = _targetOpacity;
            _idleDelaySeconds = settings?.IdleDelaySeconds ?? 2.0f;
            Modulate = new Color(1, 1, 1, _currentOpacity);
        }

        public override void _Ready()
        {
            MouseFilter = MouseFilterEnum.Ignore;
            ZIndex = 100;

            var emptyStyle = new StyleBoxEmpty();
            AddThemeStyleboxOverride("panel", emptyStyle);

            var margin = new MarginContainer();
            margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            margin.AddThemeConstantOverride("margin_left", 10);
            margin.AddThemeConstantOverride("margin_right", 10);
            margin.AddThemeConstantOverride("margin_top", 10);
            margin.AddThemeConstantOverride("margin_bottom", 10);
            AddChild(margin);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 5);
            margin.AddChild(vbox);

            _headerLabel = new Label
            {
                Text = "▲ NEXT",
                HorizontalAlignment = HorizontalAlignment.Left,
                MouseFilter = MouseFilterEnum.Ignore
            };
            _headerLabel.AddThemeFontSizeOverride("font_size", _settings?.CardNameFontSize ?? 24);
            _headerLabel.AddThemeColorOverride("font_color", StsColors.gold);
            _headerLabel.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 1f));
            _headerLabel.AddThemeConstantOverride("outline_size", 10);
            _headerLabel.AddThemeFontOverride("font", KreonRegular);
            _headerLabel.ApplyLocaleFontSubstitution(FontType.Bold, "font");
            vbox.AddChild(_headerLabel);

            _cardRow = new HBoxContainer();
            _cardRow.AddThemeConstantOverride("separation", 2);
            vbox.AddChild(_cardRow);

            Visible = false;
        }

        private void OnPileContentsChanged()
        {
            if (_currentPile == null) return;
            TopCardTracker.PruneCards(_currentPile);
            Refresh();
            _targetOpacity = _settings?.ActiveOpacity ?? 1.0f;
            _timeSinceLastChange = 0f;
        }

        public void Refresh()
        {
            foreach (Node child in _cardRow.GetChildren())
            {
                _cardRow.RemoveChild(child);
                child.QueueFree();
            }

            CardModel topCard = null;
            if (_currentPile != null)
            {
                foreach (var card in _currentPile.Cards)
                {
                    if (TopCardTracker.IsIntendedTop(card))
                    {
                        topCard = card;
                        break;
                    }
                }
            }

            if (topCard == null)
            {
                Visible = false;
                return;
            }

            Visible = true;

            try
            {
                int cardHeight = _settings?.CardHeight ?? 32;
                int cardWidth = _settings?.CardWidth ?? 200;
                int cardImageWidth = _settings?.CardImageWidth ?? 175;

                var clipContainer = new Control
                {
                    CustomMinimumSize = new Vector2(cardImageWidth, cardHeight),
                    Size = new Vector2(cardImageWidth, cardHeight),
                    ClipContents = true,
                    MouseFilter = Control.MouseFilterEnum.Ignore
                };

                var portrait = topCard.Portrait;
                if (portrait != null)
                {
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
                }

                if (topCard.Enchantment != null)
                {
                    try
                    {
                        var enchantIconPath = topCard.Enchantment.IntendedIconPath;
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
                        GD.PrintErr($"[TopCardPanel] Error adding enchantment icon: {ex.Message}");
                    }
                }

                int countFontSize = _settings?.CardCountFontSize ?? 28;

                var nameLabel = new Label
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Position = new Vector2(countFontSize + 2, 0),
                    Size = new Vector2(cardImageWidth, cardHeight)
                };
                nameLabel.Text = topCard.Title;
                nameLabel.AddThemeFontSizeOverride("font_size", _settings?.CardNameFontSize ?? 24);

                Color titleColor;
                Color titleOutlineColor;
                if (topCard.CurrentUpgradeLevel == 0)
                {
                    titleColor = StsColors.cream;
                    titleOutlineColor = GetTitleOutlineColorByRarity(topCard.Rarity);
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
                clipContainer.AddChild(nameLabel);

                _cardRow.AddChild(clipContainer);

                try
                {
                    var energyIcon = topCard.EnergyIcon;
                    if (energyIcon != null)
                    {
                        string costText;
                        bool showIcon = true;

                        if (topCard.EnergyCost.CostsX)
                        {
                            costText = "X";
                        }
                        else
                        {
                            int costWithModifiers = topCard.EnergyCost.GetWithModifiers(CostModifiers.All);
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
                            costLabel.AddThemeFontSizeOverride("font_size", _settings?.EnergyCostFontSize ?? 28);
                            costLabel.AddThemeColorOverride("font_color", StsColors.cream);
                            costLabel.AddThemeColorOverride("font_outline_color", topCard.Pool.EnergyOutlineColor);
                            costLabel.AddThemeConstantOverride("outline_size", 10);
                            costLabel.AddThemeFontOverride("font", KreonRegular);
                            costLabel.ApplyLocaleFontSubstitution(FontType.Bold, "font");
                            energyCostContainer.AddChild(costLabel);

                            _cardRow.AddChild(energyCostContainer);
                        }
                    }
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"[TopCardPanel] Error loading energy icon: {ex.Message}");
                }

                try
                {
                    int starCost = topCard.GetStarCostWithModifiers();
                    if (topCard.HasStarCostX || starCost >= 0)
                    {
                        var starIcon = ResourceLoader.Load<Texture2D>("res://images/packed/sprite_fonts/star_icon.png");
                        if (starIcon != null)
                        {
                            string starCostText = topCard.HasStarCostX ? "X" : starCost.ToString();
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
                            starCostLabel.AddThemeFontSizeOverride("font_size", _settings?.EnergyCostFontSize ?? 28);
                            starCostLabel.AddThemeColorOverride("font_color", StsColors.cream);
                            starCostLabel.AddThemeColorOverride("font_outline_color", topCard.Pool.EnergyOutlineColor);
                            starCostLabel.AddThemeConstantOverride("outline_size", 10);
                            starCostLabel.AddThemeFontOverride("font", KreonRegular);
                            starCostLabel.ApplyLocaleFontSubstitution(FontType.Bold, "font");
                            starCostContainer.AddChild(starCostLabel);

                            _cardRow.AddChild(starCostContainer);
                        }
                    }
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"[TopCardPanel] Error loading star icon: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[TopCardPanel] Error building card row: {ex.Message}");
            }
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
                Visible = false;
                return;
            }

            if (_currentPlayer == null)
            {
                var combatState = CombatManager.Instance.DebugOnlyGetState();
                if (combatState == null) return;

                _currentPlayer = LocalContext.GetMe(combatState);
                if (_currentPlayer?.PlayerCombatState == null) return;
            }

            var drawPile = _currentPlayer.PlayerCombatState.DrawPile;
            if (drawPile == null) return;

            if (!_combatStartLogged)
                _combatStartLogged = true;

            if (_currentPile != drawPile)
            {
                if (_currentPile != null)
                    _currentPile.ContentsChanged -= OnPileContentsChanged;

                _currentPile = drawPile;
                _currentPile.ContentsChanged += OnPileContentsChanged;
                Refresh();
            }

            _timeSinceLastChange += (float)delta;
            if (_timeSinceLastChange >= _idleDelaySeconds)
                _targetOpacity = _settings?.IdleOpacity ?? 0.3f;
        }
    }
}
