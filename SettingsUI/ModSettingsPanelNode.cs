using System;
using Godot;
using MegaCrit.Sts2.Core.Localization.Fonts;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using HarmonyLib;

namespace sts2decktracker
{
	public partial class ModSettingsPanelNode : MarginContainer
	{
		private ArrowInputRow _drawPileX, _drawPileY, _discardPileX, _discardPileY, _cardSize, _fadeDelay;
		private NSlider _idleOpacitySlider, _activeOpacitySlider;
		private TextureButton _draggableTickbox, _showCardTooltipTickbox, _rememberCustomPositionTickbox;
		private ModSettings _settings;
		private VBoxContainer _vbox;

		public override void _Ready()
		{
			AddThemeConstantOverride("margin_left", 20);
			AddThemeConstantOverride("margin_right", 20);
			AddThemeConstantOverride("margin_top", 16);
			AddThemeConstantOverride("margin_bottom", 16);

			_vbox = new VBoxContainer();
			_vbox.AddThemeConstantOverride("separation", 10);
			AddChild(_vbox);

			var title = new Label();
			title.AddThemeFontSizeOverride("font_size", 28);
			title.HorizontalAlignment = HorizontalAlignment.Center;
			title.Text = "Deck Tracker Settings";
			_vbox.AddChild(title);
			title.ApplyLocaleFontSubstitution(FontType.Regular, "font");

			_vbox.AddChild(new HSeparator());

			_drawPileX = new ArrowInputRow();
			_drawPileY = new ArrowInputRow();
			_discardPileX = new ArrowInputRow();
			_discardPileY = new ArrowInputRow();
			_cardSize = new ArrowInputRow();
			_fadeDelay = new ArrowInputRow();
			_vbox.AddChild(_drawPileX);
			_vbox.AddChild(_drawPileY);
			_vbox.AddChild(_discardPileX);
			_vbox.AddChild(_discardPileY);
			_vbox.AddChild(_cardSize);
			_vbox.AddChild(_fadeDelay);

			_vbox.AddChild(new HSeparator());

			var sliderScene = GD.Load<PackedScene>("res://scenes/ui/volume_slider.tscn");
			_idleOpacitySlider = AddSliderRow("Idle Opacity %", sliderScene);
			_activeOpacitySlider = AddSliderRow("Active Opacity %", sliderScene);

			_vbox.AddChild(new HSeparator());

			_draggableTickbox = AddTickboxRow("Drag CardList", "Set card panel draggable");
			_showCardTooltipTickbox = AddTickboxRow("Show Card", "Show card when you hover the card image");
			_rememberCustomPositionTickbox = AddTickboxRow("Remember CardList Position", "Remember card panel position when you enable Drag CardList.");

			_vbox.AddChild(new HSeparator());

			var buttonsRow = new HBoxContainer();
			buttonsRow.AddThemeConstantOverride("separation", 16);
			_vbox.AddChild(buttonsRow);

			var applyButton = BuildGameButton("Apply");
			applyButton.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			buttonsRow.AddChild(applyButton);

			var resetButton = BuildGameButton("Reset to Defaults");
			resetButton.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			buttonsRow.AddChild(resetButton);

			_settings = ModSettings.Load();
			RefreshValues();
			
			_drawPileX.SetupTooltip("X cordinate of draw pile panel.\nCard panel width calculated as card size * 11(12) + 8(16) + margin 20.\nThe number in () is for star cost");
			_drawPileY.SetupTooltip("Y cordinate of draw pile panel");
			_discardPileX.SetupTooltip("X cordinate of discard pile panel.\nCard panel width calculated as card size * 11(12) + 8(16) + margin 10.\nThe number in () is for star cost");
			_discardPileY.SetupTooltip("Y cordinate of discard pile panel");
			_cardSize.SetupTooltip("Card size is used to determine the value of other elements");
			_fadeDelay.SetupTooltip("Transition time from active to idle state");

			_drawPileX.ValueChanged += v => _settings.DrawPileX = (int)v;
			_drawPileY.ValueChanged += v => _settings.DrawPileY = (int)v;
			_discardPileX.ValueChanged += v => _settings.DiscardPileX = (int)v;
			_discardPileY.ValueChanged += v => _settings.DiscardPileY = (int)v;
			_cardSize.ValueChanged += v => _settings.CardSize = (int)v;
			_fadeDelay.ValueChanged += v => _settings.IdleDelaySeconds = v;
			_idleOpacitySlider.ValueChanged += v => _settings.IdleOpacity = (float)v / 100f;
			_activeOpacitySlider.ValueChanged += v => _settings.ActiveOpacity = (float)v / 100f;

			applyButton.Released += _ => OnApplyPressed();
			resetButton.Released += _ => OnResetPressed();

			_draggableTickbox.Toggled += v => _settings.Draggable = v;
			_showCardTooltipTickbox.Toggled += v => _settings.ShowCardTooltip = v;
			_rememberCustomPositionTickbox.Toggled += v => _settings.RememberCustomPosition = v;
		}

		private static NButton BuildGameButton(string text)
		{
			var shader = GD.Load<Shader>("res://shaders/hsv.gdshader");
			var mat = new ShaderMaterial();
			mat.Shader = shader;
			mat.SetShaderParameter("h", 1.0f);
			mat.SetShaderParameter("s", 1.0f);
			mat.SetShaderParameter("v", 0.8f);

			var bg = new TextureRect();
			bg.Texture = GD.Load<Texture2D>("res://images/ui/reward_screen/reward_item_button.png");
			bg.Material = mat;
			bg.AnchorRight = 1.0f;
			bg.AnchorBottom = 1.0f;
			bg.GrowHorizontal = GrowDirection.Both;
			bg.GrowVertical = GrowDirection.Both;
			bg.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
			bg.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
			bg.MouseFilter = MouseFilterEnum.Ignore;

			var lbl = new Label();
			lbl.AnchorRight = 1.0f;
			lbl.AnchorBottom = 1.0f;
			lbl.GrowHorizontal = GrowDirection.Both;
			lbl.GrowVertical = GrowDirection.Both;
			lbl.HorizontalAlignment = HorizontalAlignment.Center;
			lbl.VerticalAlignment = VerticalAlignment.Center;
			lbl.AddThemeFontSizeOverride("font_size", 24);
			lbl.AddThemeColorOverride("font_color", new Color(1f, 0.965f, 0.886f, 1f));
			lbl.AddThemeColorOverride("font_outline_color", new Color(0.135f, 0.316f, 0.342f, 1f));
			lbl.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.063f));
			lbl.AddThemeConstantOverride("outline_size", 12);
			lbl.AddThemeConstantOverride("shadow_offset_x", 4);
			lbl.AddThemeConstantOverride("shadow_offset_y", 4);
			lbl.MouseFilter = MouseFilterEnum.Ignore;
			lbl.Text = text;
			lbl.ApplyLocaleFontSubstitution(FontType.Regular, "font");

			var btn = new NButton();
			btn.CustomMinimumSize = new Vector2(0, 50);
			btn.FocusMode = FocusModeEnum.Click;
			btn.AddChild(bg);
			btn.AddChild(lbl);

			btn.MouseEntered += () => mat.SetShaderParameter("v", 1.0f);
			btn.MouseExited += () => mat.SetShaderParameter("v", 0.8f);

			return btn;
		}

		private NSlider AddSliderRow(string label, PackedScene sliderScene, string tooltip = "")
		{			
			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 8);
			
			var lbl = new Label();
			lbl.CustomMinimumSize = new Vector2(150, 0);
			lbl.SizeFlagsVertical = SizeFlags.ShrinkCenter;
			lbl.AddThemeFontSizeOverride("font_size", 18);
			lbl.Text = label;
			lbl.MouseFilter = MouseFilterEnum.Stop;
			row.AddChild(lbl);
			lbl.ApplyLocaleFontSubstitution(FontType.Regular, "font");
			
			if (tooltip != "")
			{	
				var tip = new HoverTip();
				object box = tip;
				AccessTools.Property(typeof(HoverTip), nameof(HoverTip.Title)).SetValue(box, label);
				AccessTools.Property(typeof(HoverTip), nameof(HoverTip.Description)).SetValue(box, tooltip);
				lbl.MouseEntered += () => 
				{
					var nHoverTipSet = NHoverTipSet.CreateAndShow(lbl, (HoverTip)box);
					nHoverTipSet.GlobalPosition = lbl.GlobalPosition + new Vector2(100.0f, 0.0f);
				};
				lbl.MouseExited += () =>
				{
					NHoverTipSet.Remove(lbl);
				};

			}

			var slider = sliderScene.Instantiate<NSlider>();
			slider.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			slider.MinValue = 0;
			slider.MaxValue = 100;
			row.AddChild(slider);

			var valueLbl = new Label();
			valueLbl.CustomMinimumSize = new Vector2(40, 0);
			valueLbl.SizeFlagsVertical = SizeFlags.ShrinkCenter;
			valueLbl.AddThemeFontSizeOverride("font_size", 18);
			valueLbl.HorizontalAlignment = HorizontalAlignment.Right;
			valueLbl.Text = "0%";
			row.AddChild(valueLbl);
			valueLbl.ApplyLocaleFontSubstitution(FontType.Regular, "font");

			slider.ValueChanged += v => valueLbl.Text = $"{(int)v}%";

			_vbox.AddChild(row);
			return slider;
		}

		private TextureButton AddTickboxRow(string label, string tooltip = "")
		{
			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 8);

			var ticked = GD.Load<Texture2D>("res://images/atlases/ui_atlas.sprites/checkbox_ticked.tres");
			var unticked = GD.Load<Texture2D>("res://images/atlases/ui_atlas.sprites/checkbox_unticked.tres");

			var tickbox = new TextureButton();
			tickbox.ToggleMode = true;
			tickbox.TextureNormal = unticked;
			tickbox.TexturePressed = ticked;
			tickbox.TextureHover = unticked;
			tickbox.IgnoreTextureSize = true;
			tickbox.StretchMode = TextureButton.StretchModeEnum.KeepAspectCentered;
			tickbox.CustomMinimumSize = new Vector2(40, 40);
			tickbox.Modulate = new Color(1.0f, 1.0f, 1.0f, 1f);
			tickbox.FocusMode = Control.FocusModeEnum.None;
			row.AddChild(tickbox);

			var lbl = new Label { Text = label };
			lbl.VerticalAlignment = VerticalAlignment.Center;
			lbl.AddThemeFontSizeOverride("font_size", 18);
			lbl.ApplyLocaleFontSubstitution(FontType.Regular, "font");
			lbl.MouseFilter = MouseFilterEnum.Stop;
			row.AddChild(lbl);

			if (tooltip != "")
			{	
				var tip = new HoverTip();
				object box = tip;
				AccessTools.Property(typeof(HoverTip), nameof(HoverTip.Title)).SetValue(box, label);
				AccessTools.Property(typeof(HoverTip), nameof(HoverTip.Description)).SetValue(box, tooltip);
				lbl.MouseEntered += () => 
				{
					var nHoverTipSet = NHoverTipSet.CreateAndShow(lbl, (HoverTip)box);
					nHoverTipSet.GlobalPosition = lbl.GlobalPosition + new Vector2(100.0f, 0.0f);
				};
				lbl.MouseExited += () =>
				{
					NHoverTipSet.Remove(lbl);
				};

			}

			_vbox.AddChild(row);
			return tickbox;
		}

		public void Refresh()
		{
			_settings = ModSettings.Load();
			RefreshValues();
		}

		private void RefreshValues()
		{
			_drawPileX.Setup("Draw Pile X", _settings.DrawPileX, 0, 1920);
			_drawPileY.Setup("Draw Pile Y", _settings.DrawPileY, 0, 1080);
			_discardPileX.Setup("Discard Pile X", _settings.DiscardPileX, 0, 1920);
			_discardPileY.Setup("Discard Pile Y", _settings.DiscardPileY, 0, 1080);
			_cardSize.Setup("Card Size", _settings.CardSize, 12, 48);
			_fadeDelay.Setup("Fade Time (s)", _settings.IdleDelaySeconds, 0f, 10f, step: 0.1f);
			_idleOpacitySlider.Value = _settings.IdleOpacity * 100;
			_activeOpacitySlider.Value = _settings.ActiveOpacity * 100;
			_draggableTickbox.ButtonPressed = _settings.Draggable;
			_showCardTooltipTickbox.ButtonPressed = _settings.ShowCardTooltip;
			_rememberCustomPositionTickbox.ButtonPressed = _settings.RememberCustomPosition;
		}

		private void OnApplyPressed()
		{
			_settings.Save();
			DeckTrackerInjectionPatch.ApplySettings(_settings);
		}

		private void OnResetPressed()
		{
			_settings.ResetToDefaults();
			_settings.Save();
			RefreshValues();
			DeckTrackerInjectionPatch.ApplySettings(_settings);
		}
	}
}
