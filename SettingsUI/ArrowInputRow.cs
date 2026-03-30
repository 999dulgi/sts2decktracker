using System;
using System.Globalization;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Localization.Fonts;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.HoverTips;

namespace sts2decktracker
{
	public partial class ArrowInputRow : HBoxContainer
	{
		public event Action<float> ValueChanged;

		private Label _label;
		private NMegaLineEdit _input;
		private NGoldArrowButton _leftArrow;
		private NGoldArrowButton _rightArrow;

		private float _value;
		private float _min = float.MinValue;
		private float _max = float.MaxValue;
		private float _step = 1f;
		private string _format = "0";
		private string _tooltip = "";

		public float Value
		{
			get => _value;
			set
			{
				_value = Math.Clamp(value, _min, _max);
				if (_input != null)
					_input.Text = _value.ToString(_format, CultureInfo.InvariantCulture);
			}
		}

		public override void _Ready()
		{
			CustomMinimumSize = new Vector2(0, 40);
			AddThemeConstantOverride("separation", 5);

			_label = new Label();
			_label.CustomMinimumSize = new Vector2(150, 0);
			_label.SizeFlagsVertical = SizeFlags.ShrinkCenter;
			_label.AddThemeFontSizeOverride("font_size", 18);
			_label.MouseFilter = MouseFilterEnum.Stop;
			AddChild(_label);
			_label.ApplyLocaleFontSubstitution(FontType.Regular, "font");

			_leftArrow = BuildArrowButton(isLeft: true);
			AddChild(_leftArrow);

			_input = new NMegaLineEdit();
			_input.CustomMinimumSize = new Vector2(80, 0);
			_input.SizeFlagsVertical = SizeFlags.ShrinkCenter;
			_input.Text = _value.ToString(_format, CultureInfo.InvariantCulture);
			_input.Alignment = HorizontalAlignment.Center;
			AddChild(_input);

			_rightArrow = BuildArrowButton(isLeft: false);
			AddChild(_rightArrow);

			_input.TextChanged += OnTextChanged;
			_leftArrow.Released += _ => OnArrowPressed(-_step);
			_rightArrow.Released += _ => OnArrowPressed(_step);
		}

		private static NGoldArrowButton BuildArrowButton(bool isLeft)
		{
			var texturePath = isLeft
				? "res://images/atlases/ui_atlas.sprites/settings_tiny_left_arrow.tres"
				: "res://images/atlases/ui_atlas.sprites/settings_tiny_right_arrow.tres";

			var shader = GD.Load<Shader>("res://shaders/hsv.gdshader");
			var mat = new ShaderMaterial();
			mat.Shader = shader;
			mat.SetShaderParameter("h", 1.0f);
			mat.SetShaderParameter("s", 1.0f);
			mat.SetShaderParameter("v", 0.9f);

			var texRect = new TextureRect();
			texRect.Name = "TextureRect";
			texRect.Material = mat;
			texRect.AnchorLeft = 0.5f;
			texRect.AnchorTop = 0.5f;
			texRect.AnchorRight = 0.5f;
			texRect.AnchorBottom = 0.5f;
			texRect.OffsetLeft = -16f;
			texRect.OffsetTop = -16f;
			texRect.OffsetRight = 16f;
			texRect.OffsetBottom = 16f;
			texRect.GrowHorizontal = GrowDirection.Both;
			texRect.GrowVertical = GrowDirection.Both;
			texRect.PivotOffset = new Vector2(16, 16);
			texRect.MouseFilter = MouseFilterEnum.Ignore;
			texRect.Texture = GD.Load<Texture2D>(texturePath);
			texRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
			texRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;

			var button = new NGoldArrowButton();
			button.CustomMinimumSize = new Vector2(40, 40);
			button.AddChild(texRect);
			return button;
		}

		public void Setup(string labelText, float initialValue, float min, float max, float step = 1f)
		{
			_min = min;
			_max = max;
			_step = step;
			_format = step < 0.01f ? "0.00" : step < 1f ? "0.0" : "0";
			if (_label != null)
				_label.Text = labelText;
			Value = initialValue;
		}

		public void SetupTooltip(string tooltip = "")
		{
			_tooltip = tooltip;
			var tip = new HoverTip();
			object box = tip;
			AccessTools.Property(typeof(HoverTip), nameof(HoverTip.Title)).SetValue(box, _label.Text);
			AccessTools.Property(typeof(HoverTip), nameof(HoverTip.Description)).SetValue(box, _tooltip);
			_label.MouseEntered += () => 
			{
				var nHoverTipSet = NHoverTipSet.CreateAndShow(_label, (HoverTip)box);
				nHoverTipSet.GlobalPosition = _label.GlobalPosition + new Vector2(100.0f, 0.0f);
			};
			_label.MouseExited += () =>
			{
				NHoverTipSet.Remove(_label);
			};
		}

		private void OnArrowPressed(float delta)
		{
			Value = RoundToStep(_value + delta);
			ValueChanged?.Invoke(_value);
		}

		private void OnTextChanged(string text)
		{
			if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
			{
				float clamped = Math.Clamp(RoundToStep(parsed), _min, _max);
				string formatted = clamped.ToString(_format, CultureInfo.InvariantCulture);
				if (_input.Text != formatted)
					_input.Text = formatted;
				_value = clamped;
				ValueChanged?.Invoke(_value);
			}
		}

		private float RoundToStep(float value)
		{
			if (_step <= 0f) return value;
			return MathF.Round(value / _step) * _step;
		}
	}
}
