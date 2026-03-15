using Godot;
using System;
using System.Linq;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Localization.Fonts;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Helpers.Models;

namespace sts2modtest
{
	public partial class CardListPanel : Panel
	{
		private VBoxContainer _cardList;
		private Label _titleLabel;
		private int _lastCardCount = -1;
		private bool _combatStartLogged = false;
		private PileType _pileType = PileType.Draw;
		private ModSettings _settings;
		private System.Collections.Generic.List<(CardModel card, int count)> _shuffledOrder = null;
		private System.Collections.Generic.HashSet<string> _lastCardKeys = new System.Collections.Generic.HashSet<string>();
		private float _targetOpacity = 0.3f;
		private float _currentOpacity = 0.3f;
		private const float OpacityTransitionSpeed = 5.0f;
		private float _timeSinceLastChange = 0f;
		private float _idleDelaySeconds = 2.0f;

		public void SetPileType(PileType pileType)
		{
			_pileType = pileType;
		}

		public void SetSettings(ModSettings settings)
		{
			_settings = settings;
			// Force refresh to apply new settings
			_lastCardCount = -1;
			// Set initial opacity to idle state
			_targetOpacity = settings?.IdleOpacity ?? 0.3f;
			_currentOpacity = _targetOpacity;
			_idleDelaySeconds = settings?.IdleDelaySeconds ?? 2.0f;
			Modulate = new Color(1, 1, 1, _currentOpacity);
		}

	public override void _Ready()
	{
		CustomMinimumSize = new Vector2(250, 400);
		Size = new Vector2(250, 400);
		
		ZIndex = 100;
		
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
		
		// Force initial update
		_lastCardCount = -1;
	}

	public override void _Process(double delta)
	{
		// Smoothly transition opacity
		if (Math.Abs(_currentOpacity - _targetOpacity) > 0.01f)
		{
			_currentOpacity = Mathf.Lerp(_currentOpacity, _targetOpacity, OpacityTransitionSpeed * (float)delta);
			Modulate = new Color(1, 1, 1, _currentOpacity);
		}

		// Check if combat is active
		if (CombatManager.Instance == null || !CombatManager.Instance.IsInProgress)
		{
			_combatStartLogged = false;
			return;
		}

		// Get the combat state
		var combatState = CombatManager.Instance.DebugOnlyGetState();
		if (combatState == null)
		{
			return;
		}

		// Get the first player
		var player = combatState.Players[0];
		if (player?.PlayerCombatState == null)
		{
			return;
		}

		// Get draw pile
		var drawPile = player.PlayerCombatState.DrawPile;
		if (drawPile == null)
		{
			if (!_combatStartLogged)
			{
				GD.PrintErr("[CardListPanel] DrawPile is null!");
				_combatStartLogged = true;
			}
			return;
		}

		// Get the appropriate pile based on type
		var pile = _pileType == PileType.Draw ? drawPile : player.PlayerCombatState.DiscardPile;
		if (pile == null)
		{
			return;
		}

		if (!_combatStartLogged)
		{
			_combatStartLogged = true;
		}

		// Only update if card count changed
		int currentCount = pile.Cards.Count;
		if (currentCount != _lastCardCount)
		{
			_lastCardCount = currentCount;
			UpdateCardList(_cardList, pile);
			// Set to active opacity when cards change
			_targetOpacity = _settings?.ActiveOpacity ?? 1.0f;
			_timeSinceLastChange = 0f;
		}
		else
		{
			// Track time since last change
			_timeSinceLastChange += (float)delta;
			
			// Return to idle opacity after delay or when pile is empty
			if (_timeSinceLastChange >= _idleDelaySeconds || currentCount == 0)
			{
				_targetOpacity = _settings?.IdleOpacity ?? 0.3f;
			}
		}
	}

	private void UpdateCardList(VBoxContainer container, CardPile pile)
	{
		if (container == null || pile == null)
		{
			return;
		}

		// Clear existing children
		foreach (Node child in container.GetChildren())
		{
			container.RemoveChild(child);
			child.QueueFree();
		}

		// If pile is empty, just return without showing anything
		if (pile.Cards.Count == 0)
		{
			return;
		}

		// Group cards by name, upgrade status, and enchantment
		var cardGroups = new System.Collections.Generic.Dictionary<string, (CardModel card, int count)>();
		var currentCardKeys = new System.Collections.Generic.HashSet<string>();
		
		foreach (var card in pile.Cards)
		{
			// Create unique key: name + upgrade status + enchantment
			string enchantmentKey = card.Enchantment != null ? card.Enchantment.GetType().Name : "none";
			string key = $"{card.Title}|{card.IsUpgraded}|{enchantmentKey}";
			currentCardKeys.Add(key);
			
			if (cardGroups.TryGetValue(key, out var existing))
			{
				cardGroups[key] = (existing.card, existing.count + 1);
			}
			else
			{
				cardGroups[key] = (card, 1);
			}
		}

		// Check if card composition changed (new cycle or cards added/removed)
		bool compositionChanged = !currentCardKeys.SetEquals(_lastCardKeys);
		
		System.Collections.Generic.List<(CardModel card, int count)> displayGroups;
		
		if (compositionChanged || _shuffledOrder == null)
		{
			// Composition changed - shuffle and save new order
			displayGroups = cardGroups.Values.ToList();
			var random = new System.Random();
			for (int i = displayGroups.Count - 1; i > 0; i--)
			{
				int j = random.Next(i + 1);
				var temp = displayGroups[i];
				displayGroups[i] = displayGroups[j];
				displayGroups[j] = temp;
			}
			_shuffledOrder = displayGroups;
			_lastCardKeys = currentCardKeys;
		}
		else
		{
			// Same composition - use saved order
			displayGroups = _shuffledOrder;
		}

		// Display unique cards with count
		foreach (var group in displayGroups)
		{
			var card = group.card;
			var count = group.count;
			try
			{
				// Get card portrait texture
				var portrait = card.Portrait;
				if (portrait != null)
				{
					// Get dimensions from settings (with defaults if settings not set)
					int cardHeight = _settings?.CardHeight ?? 32;
					int cardWidth = _settings?.CardWidth ?? 200;
					int cardImageWidth = _settings?.CardImageWidth ?? 175;
					
					// Create parent container for card image and energy icon
					var cardRowContainer = new HBoxContainer
					{
						CustomMinimumSize = new Vector2(cardWidth, cardHeight)
					};
					cardRowContainer.AddThemeConstantOverride("separation", 2);
					
					// Use Control with ClipContents to crop the image
					var clipContainer = new Control
					{
						CustomMinimumSize = new Vector2(cardImageWidth, cardHeight),
						Size = new Vector2(cardImageWidth, cardHeight),
						ClipContents = true
					};
					
					var textureRect = new TextureRect
					{
						Texture = portrait,
						// Position image so 1/4 from top is visible (move up by 1/4 of height)
						Position = new Vector2(0, -portrait.GetHeight() / 4),
						Size = new Vector2(cardImageWidth, portrait.GetHeight() * (float)cardImageWidth / portrait.GetWidth()),
						ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
						StretchMode = TextureRect.StretchModeEnum.KeepAspect
					};
					
					clipContainer.AddChild(textureRect);
					
					// Add enchantment icon if card is enchanted (inside card image, right side)
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
									// Enchantment icon with glow effect (no background)
									// Position on right side of card image, scaled with card width
									int enchantIconSize = cardHeight - 6;  // Icon size based on card height
									var enchantIconRect = new TextureRect
									{
										Texture = enchantIcon,
										Position = new Vector2(cardImageWidth - enchantIconSize - 4, 2),
										CustomMinimumSize = new Vector2(enchantIconSize, enchantIconSize),
										ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional,
										StretchMode = TextureRect.StretchModeEnum.KeepAspect
									};
									// Apply purple glow effect via modulate
									enchantIconRect.Modulate = new Color(1.5f, 1.3f, 1.8f, 1.0f); // Bright purple-ish glow
									
									clipContainer.AddChild(enchantIconRect);
								}
							}
						}
						catch (Exception ex)
						{
							GD.PrintErr($"[CardListPanel] Error adding enchantment icon: {ex.Message}");
						}
					}
					
					// Add card count label (separate from name)
					var countLabel = new Label
					{
						Text = count.ToString(),
						VerticalAlignment = VerticalAlignment.Center,
						Position = new Vector2(3, 0),
						Size = new Vector2(cardHeight, cardHeight)
					};
					int countFontSize = _settings?.CardCountFontSize ?? 28;
					countLabel.AddThemeFontSizeOverride("font_size", countFontSize);
					countLabel.AddThemeColorOverride("font_color", StsColors.gold); // Yellow/Gold
					countLabel.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.188f));
					countLabel.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 1)); // Black outline
					countLabel.AddThemeConstantOverride("shadow_offset_x", 2);
					countLabel.AddThemeConstantOverride("shadow_offset_y", 2);
					countLabel.AddThemeConstantOverride("outline_size", 10);
					countLabel.AddThemeConstantOverride("shadow_outline_size", 10);
					countLabel.ApplyLocaleFontSubstitution(FontType.Bold, "font");
					clipContainer.AddChild(countLabel);
					
					// Add card name label (without count)
					var nameLabel = new Label();
					string displayName = GetCardDisplayName(card);
					nameLabel.Text = displayName;
					int nameFontSize = _settings?.CardNameFontSize ?? 24;
					nameLabel.AddThemeFontSizeOverride("font_size", nameFontSize);
					
					// Apply title colors like NCard.UpdateTitleLabel
					Color titleColor;
					Color titleOutlineColor;
					
					if (card.CurrentUpgradeLevel == 0)
					{
						// Not upgraded - cream color with rarity-based outline
						titleColor = StsColors.cream;
						titleOutlineColor = GetTitleOutlineColorByRarity(card.Rarity);
					}
					else
					{
						// Upgraded - green color with special outline
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
					
					// Apply language-specific font substitution (same as game's card titles) - use Bold
					nameLabel.ApplyLocaleFontSubstitution(FontType.Bold, "font");
					
					// Center vertically, positioned after count
					nameLabel.VerticalAlignment = VerticalAlignment.Center;
					nameLabel.Position = new Vector2(countFontSize, 0);
					nameLabel.Size = new Vector2(cardImageWidth, cardHeight);
					
					clipContainer.AddChild(nameLabel);
					cardRowContainer.AddChild(clipContainer);
					
					// Add energy cost icon on the right side (outside card image)
					try
					{
						var energyIcon = card.EnergyIcon;
						if (energyIcon != null)
						{
							// Determine cost text based on card state (similar to NCard.UpdateEnergyCostVisuals)
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
								showIcon = costWithModifiers >= 0; // Hide icon for negative costs
							}
							
							if (showIcon)
							{
								// Get cost icon size from settings
								int iconSize = _settings?.CostIconSize ?? 30;
								
								// Energy cost container (for icon + number overlay)
								var energyCostContainer = new Control
								{
									CustomMinimumSize = new Vector2(iconSize, iconSize),
									Size = new Vector2(iconSize, iconSize)
								};
								
								// Energy icon background
								var energyIconRect = new TextureRect
								{
									Texture = energyIcon,
									CustomMinimumSize = new Vector2(iconSize, iconSize),
									ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional,
									StretchMode = TextureRect.StretchModeEnum.KeepAspect
								};
								energyCostContainer.AddChild(energyIconRect);
								
								// Energy cost number on top
								var costLabel = new Label
								{
									Text = costText,
									HorizontalAlignment = HorizontalAlignment.Center,
									VerticalAlignment = VerticalAlignment.Center,
									Size = new Vector2(iconSize, iconSize)
								};
								int costFontSize = _settings?.EnergyCostFontSize ?? 28;
								costLabel.AddThemeFontSizeOverride("font_size", costFontSize);
								
								// Apply theme colors like NCard.UpdateEnergyCostColor
								Color fontColor = StsColors.cream;
								Color outlineColor = card.Pool.EnergyOutlineColor;
								
								// Check if energy cost was just upgraded (green color)
								if (card.EnergyCost != null && !card.EnergyCost.CostsX && card.EnergyCost.WasJustUpgraded)
								{
									fontColor = StsColors.green;
									outlineColor = StsColors.energyGreenOutline;
								}
								// Apply cost color based on card state (if in combat)
								else if (_pileType == PileType.Hand && card.CombatState != null)
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
										case CardCostColor.InsufficientResources:
											fontColor = StsColors.red;
											outlineColor = StsColors.unplayableEnergyCostOutline;
											break;
									}
								}
								
								costLabel.AddThemeColorOverride("font_color", fontColor);
								costLabel.AddThemeColorOverride("font_outline_color", outlineColor);
								costLabel.AddThemeConstantOverride("shadow_offset_x", 2);
								costLabel.AddThemeConstantOverride("shadow_offset_y", 2);
								costLabel.AddThemeConstantOverride("outline_size", 10);
								costLabel.AddThemeConstantOverride("shadow_outline_size", 10);
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
					
					// Add star cost icon if card has star cost
					try
					{
						int starCost = card.GetStarCostWithModifiers();
						if (card.HasStarCostX || starCost >= 0)
						{
							// Load star icon
							var starIcon = ResourceLoader.Load<Texture2D>("res://images/packed/sprite_fonts/star_icon.png");
							if (starIcon != null)
							{
								string starCostText = card.HasStarCostX ? "X" : starCost.ToString();
								bool showStarIcon = card.HasStarCostX || starCost >= 0;
								
								if (showStarIcon)
								{
									// Get cost icon size from settings
									int iconSize = _settings?.CostIconSize ?? 30;
									
									var starCostContainer = new Control
									{
										CustomMinimumSize = new Vector2(iconSize, iconSize),
										Size = new Vector2(iconSize, iconSize)
									};
									
									// Star icon background
									var starIconRect = new TextureRect
									{
										Texture = starIcon,
										CustomMinimumSize = new Vector2(iconSize, iconSize),
										ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional,
										StretchMode = TextureRect.StretchModeEnum.KeepAspect
									};
									starCostContainer.AddChild(starIconRect);
									
									// Star cost number on top
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
					// Fallback to text if portrait is null
					var label = new Label();
					label.Text = GetCardDisplayName(card);
					label.AddThemeFontSizeOverride("font_size", 12);
					container.AddChild(label);
				}
			}
			catch (Exception ex)
			{
				GD.PrintErr($"[CardListPanel] Error loading card portrait: {ex.Message}");
				// Fallback to text
				var label = new Label();
				label.Text = GetCardDisplayName(card);
				label.AddThemeFontSizeOverride("font_size", 12);
				container.AddChild(label);
			}
		}
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
