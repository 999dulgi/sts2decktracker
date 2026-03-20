using System;
using Godot;
using MegaCrit.Sts2.Core.Localization.Fonts;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;

namespace sts2decktracker
{
	public partial class ModSettingsPanelNode : MarginContainer
	{
		private ArrowInputRow _drawPileX, _drawPileY, _discardPileX, _discardPileY, _cardSize, _fadeDelay;
		private NSlider _idleOpacitySlider, _activeOpacitySlider;
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

			_drawPileX = new ArrowInputRow(); _vbox.AddChild(_drawPileX);
			_drawPileY = new ArrowInputRow(); _vbox.AddChild(_drawPileY);
			_discardPileX = new ArrowInputRow(); _vbox.AddChild(_discardPileX);
			_discardPileY = new ArrowInputRow(); _vbox.AddChild(_discardPileY);
			_cardSize = new ArrowInputRow(); _vbox.AddChild(_cardSize);
			_fadeDelay = new ArrowInputRow(); _vbox.AddChild(_fadeDelay);

			_vbox.AddChild(new HSeparator());

			var sliderScene = GD.Load<PackedScene>("res://scenes/ui/volume_slider.tscn");
			_idleOpacitySlider = AddSliderRow("Idle Opacity %", sliderScene);
			_activeOpacitySlider = AddSliderRow("Active Opacity %", sliderScene);

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

		private NSlider AddSliderRow(string label, PackedScene sliderScene)
		{
			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 8);

			var lbl = new Label();
			lbl.CustomMinimumSize = new Vector2(150, 0);
			lbl.SizeFlagsVertical = SizeFlags.ShrinkCenter;
			lbl.AddThemeFontSizeOverride("font_size", 18);
			lbl.Text = label;
			row.AddChild(lbl);
			lbl.ApplyLocaleFontSubstitution(FontType.Regular, "font");

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
			_fadeDelay.Setup("Fade Delay (s)", _settings.IdleDelaySeconds, 0f, 10f, step: 0.1f);
			_idleOpacitySlider.Value = _settings.IdleOpacity * 100;
			_activeOpacitySlider.Value = _settings.ActiveOpacity * 100;
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
