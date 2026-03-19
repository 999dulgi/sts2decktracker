using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Godot;

namespace sts2decktracker
{
	/// <summary>
	/// Handles in-game settings UI injection into the Modding Screen
	/// </summary>
	public static class ModSettingsUI
	{
		private static object _currentPanel = null;
		private static ModSettings _currentSettings = null;
		private static System.Collections.Generic.Dictionary<string, object> _uiElements = new System.Collections.Generic.Dictionary<string, object>();

		/// <summary>
		/// Called when a mod is selected in the Modding Screen
		/// </summary>
		public static void RefreshForSelection(object infoContainer, object mod)
		{
			try
			{
				// Check if this is our mod
				if (!IsThisMod(mod))
				{
					// Hide our panel if it exists and is valid
					if (_currentPanel != null && IsNodeValid(_currentPanel))
					{
						SetVisible(_currentPanel, false);
					}
					return;
				}

				// Always reload settings from file to get latest values
				_currentSettings = ModSettings.Load();
			
				// Create panel if it doesn't exist or is no longer valid
				if (_currentPanel == null || !IsNodeValid(_currentPanel))
				{
					_currentPanel = CreateSettingsPanel(infoContainer);
				}
				else
				{
					// Update panel values with reloaded settings
					UpdatePanelValues();
				}

				// Show panel
				if (_currentPanel != null)
				{
					SetVisible(_currentPanel, true);
				}
			}
			catch (Exception ex)
			{
				GD.PrintErr($"[ModSettingsUI] Error in RefreshForSelection: {ex.Message}");
				GD.PrintErr($"[ModSettingsUI] Stack trace: {ex.StackTrace}");
			}
		}

		private static bool IsThisMod(object mod)
		{
			if (mod == null)
			{
				return false;
			}

			try
			{
				var modType = mod.GetType();
				var bindingFlags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
 
				string fieldName = "assembly";
				var field = modType.GetField(fieldName, bindingFlags);
				if (field != null)
				{
					var value = field.GetValue(mod);
					string valueStr = value?.ToString() ?? "";
						GD.Print($"[ModSettingsUI] Found field '{fieldName}': {valueStr}");
						if (valueStr.Contains("sts2decktracker") || valueStr.Contains("Slay the Spire 2 Deck Tracker"))
						{
							GD.Print($"[ModSettingsUI] Mod matched on field '{fieldName}'!");
							return true;
						}
				}
			}
			catch (Exception ex)
			{
				GD.PrintErr($"[ModSettingsUI] Error checking mod: {ex.Message}");
				GD.PrintErr($"[ModSettingsUI] Stack trace: {ex.StackTrace}");
			}

			return false;
		}

		private static object CreateSettingsPanel(object infoContainer)
		{
			// Get Godot types using reflection
			var vboxType = AccessTools.TypeByName("Godot.VBoxContainer");
			var hboxType = AccessTools.TypeByName("Godot.HBoxContainer");
			var buttonType = AccessTools.TypeByName("Godot.Button");
			var labelType = AccessTools.TypeByName("Godot.Label");
			var scrollContainerType = AccessTools.TypeByName("Godot.ScrollContainer");
			var stringNameType = AccessTools.TypeByName("Godot.StringName");

			// Use the infoContainer directly as the target
			object targetContainer = infoContainer;

			// Create main VBoxContainer with absolute positioning
			var mainPanel = Activator.CreateInstance(vboxType);
			SetPosition(mainPanel, new Vector2(20, 100));
			SetCustomMinimumSize(mainPanel, new Vector2(500, 400));
			
			// Add title
			var titleLabel = Activator.CreateInstance(labelType);
			SetText(titleLabel, "Deck Tracker Settings");
			AddThemeFontSizeOverride(titleLabel, CreateStringName("font_size"), 18);
			AddChild(mainPanel, titleLabel);

			// Add spacing
			AddChild(mainPanel, CreateSpacer(10));

			// Draw Pile X Position
			var drawXRow = CreateAdjustRow("Draw Pile X", () => _currentSettings.DrawPileX, (value) =>
			{
				_currentSettings.DrawPileX = value;
			}, 1, 0, 1920, "drawX_input");
			AddChild(mainPanel, drawXRow);

			// Draw Pile Y Position
			var drawYRow = CreateAdjustRow("Draw Pile Y", () => _currentSettings.DrawPileY, (value) =>
			{
				_currentSettings.DrawPileY = value;
			}, 1, 0, 1080, "drawY_input");
			AddChild(mainPanel, drawYRow);

			// Discard Pile X Position
			var discardXRow = CreateAdjustRow("Discard Pile X", () => _currentSettings.DiscardPileX, (value) =>
			{
				_currentSettings.DiscardPileX = value;
			}, 1, -1920, 0, "discardX_input");
			AddChild(mainPanel, discardXRow);

			// Discard Pile Y Position
			var discardYRow = CreateAdjustRow("Discard Pile Y", () => _currentSettings.DiscardPileY, (value) =>
			{
				_currentSettings.DiscardPileY = value;
			}, 1, 0, 1080, "discardY_input");
			AddChild(mainPanel, discardYRow);

			AddChild(mainPanel, CreateSpacer(5));

			// Card Size (controls card height and all text sizes)
			var cardSizeRow = CreateAdjustRow("Card Size", () => _currentSettings.CardSize, (value) =>
			{
				_currentSettings.CardSize = value;
			}, 2, 12, 48, "cardSize_input");
			AddChild(mainPanel, cardSizeRow);

			AddChild(mainPanel, CreateSpacer(5));

			// Idle Opacity (transparency when no cards change) - displayed as percentage
			var idleOpacityRow = CreateAdjustRow("Idle Opacity %", () => (int)(_currentSettings.IdleOpacity * 100), (value) =>
			{
				_currentSettings.IdleOpacity = value / 100f;
			}, 1, 0, 100, "idleOpacity_input");
			AddChild(mainPanel, idleOpacityRow);

			// Active Opacity (opacity when cards are drawn/discarded) - displayed as percentage
			var activeOpacityRow = CreateAdjustRow("Active Opacity %", () => (int)(_currentSettings.ActiveOpacity * 100), (value) =>
			{
				_currentSettings.ActiveOpacity = value / 100f;
			}, 1, 0, 100, "activeOpacity_input");
			AddChild(mainPanel, activeOpacityRow);

			// Idle Delay (seconds to wait before fading to idle opacity) - displayed in seconds
			var idleDelayRow = CreateAdjustRow("Fade Delay (sec)", () => (int)(_currentSettings.IdleDelaySeconds * 10), (value) =>
			{
				_currentSettings.IdleDelaySeconds = value / 10f;
			}, 1, 0, 100, 10, "F1", "idleDelay_input");
			AddChild(mainPanel, idleDelayRow);

			AddChild(mainPanel, CreateSpacer(10));

			// Apply button
			var applyButton = Activator.CreateInstance(buttonType);
			SetText(applyButton, "Apply Settings");
			SetCustomMinimumSize(applyButton, new Vector2(200, 40));
			ConnectPressed(applyButton, () =>
			{
				_currentSettings.Save();
				DeckTrackerInjectionPatch.ApplySettings(_currentSettings);
			});
			AddChild(mainPanel, applyButton);

			var resetButton = Activator.CreateInstance(buttonType);
			SetText(resetButton, "Reset to Defaults");
			SetCustomMinimumSize(resetButton, new Vector2(200, 40));
			ConnectPressed(resetButton, () =>
			{
				_currentSettings.ResetToDefaults();
				_currentSettings.Save();
				DeckTrackerInjectionPatch.ApplySettings(_currentSettings);
				UpdatePanelValues();
			});
			AddChild(mainPanel, resetButton);

			// Add panel to container
			AddChild(targetContainer, mainPanel);

			return mainPanel;
		}

		private static object CreateToggleRow(string labelText, Func<bool> getValue, Action<bool> setValue)
		{
			var hboxType = AccessTools.TypeByName("Godot.HBoxContainer");
			var labelType = AccessTools.TypeByName("Godot.Label");
			var buttonType = AccessTools.TypeByName("Godot.Button");

			var row = Activator.CreateInstance(hboxType);

			// Label
			var label = Activator.CreateInstance(labelType);
			SetText(label, labelText);
			SetCustomMinimumSize(label, new Vector2(200, 0));
			AddChild(row, label);

			// Toggle button
			var button = Activator.CreateInstance(buttonType);
			SetText(button, getValue() ? "ON" : "OFF");
			SetCustomMinimumSize(button, new Vector2(80, 0));
			
			ConnectPressed(button, () =>
			{
				bool newValue = !getValue();
				setValue(newValue);
				SetText(button, newValue ? "ON" : "OFF");
			});

			AddChild(row, button);

			return row;
		}

		private static object CreateAdjustRow(string labelText, Func<int> getValue, Action<int> setValue, int step, int min, int max, string uiKey = null)
		{
			var hboxType = AccessTools.TypeByName("Godot.HBoxContainer");
			var labelType = AccessTools.TypeByName("Godot.Label");
			var buttonType = AccessTools.TypeByName("Godot.Button");
			var lineEditType = AccessTools.TypeByName("Godot.LineEdit");

			var row = Activator.CreateInstance(hboxType);

			// Label
			var label = Activator.CreateInstance(labelType);
			SetText(label, labelText);
			SetCustomMinimumSize(label, new Vector2(200, 0));
			AddChild(row, label);

			// LineEdit for direct input
			var lineEdit = Activator.CreateInstance(lineEditType);
			SetText(lineEdit, getValue().ToString());
			SetCustomMinimumSize(lineEdit, new Vector2(60, 0));
			
			// Store in dictionary if key provided
			if (!string.IsNullOrEmpty(uiKey))
			{
				_uiElements[uiKey] = lineEdit;
			}
			
			// Connect text changed event
			ConnectTextChanged(lineEdit, (text) =>
			{
				if(text == null) {
					setValue(0);
					SetText(lineEdit, "0");
				}
				else if (int.TryParse(text, out int newValue))
				{
					newValue = Math.Clamp(newValue, min, max);
					setValue(newValue);
					SetText(lineEdit, newValue.ToString());
				}
				else
				{
					SetText(lineEdit, getValue().ToString());
				}
			});

			

			// Minus button
			var minusButton = Activator.CreateInstance(buttonType);
			SetText(minusButton, "-");
			SetCustomMinimumSize(minusButton, new Vector2(40, 0));
			ConnectPressed(minusButton, () =>
			{
				int newValue = Math.Max(min, getValue() - step);
				setValue(newValue);
				SetText(lineEdit, newValue.ToString());
			});
			AddChild(row, minusButton);

			AddChild(row, lineEdit);

			// Plus button
			var plusButton = Activator.CreateInstance(buttonType);
			SetText(plusButton, "+");
			SetCustomMinimumSize(plusButton, new Vector2(40, 0));
			ConnectPressed(plusButton, () =>
			{
				int newValue = Math.Min(max, getValue() + step);
				setValue(newValue);
				SetText(lineEdit, newValue.ToString());
			});
			AddChild(row, plusButton);

			return row;
		}

		private static object CreateAdjustRow(string labelText, Func<int> getValue, Action<int> setValue, int step, int min, int max, int divisor, string format = "F1", string uiKey = null)
		{
			var hboxType = AccessTools.TypeByName("Godot.HBoxContainer");
			var labelType = AccessTools.TypeByName("Godot.Label");
			var buttonType = AccessTools.TypeByName("Godot.Button");
			var lineEditType = AccessTools.TypeByName("Godot.LineEdit");

			var row = Activator.CreateInstance(hboxType);

			// Label
			var label = Activator.CreateInstance(labelType);
			SetText(label, labelText);
			SetCustomMinimumSize(label, new Vector2(200, 0));
			AddChild(row, label);

			// LineEdit for direct input
			var lineEdit = Activator.CreateInstance(lineEditType);
			SetText(lineEdit, (getValue() / (float)divisor).ToString(format));
			SetCustomMinimumSize(lineEdit, new Vector2(60, 0));
			
			// Store in dictionary if key provided
			if (!string.IsNullOrEmpty(uiKey))
			{
				_uiElements[uiKey] = lineEdit;
			}
			
			// Connect text changed event
			ConnectTextChanged(lineEdit, (text) =>
			{
				if(text == null) {
					setValue(0);
					SetText(lineEdit, "0");
				}
				else if (float.TryParse(text, out float floatValue))
				{
					int newValue = (int)(floatValue * divisor);
					newValue = Math.Clamp(newValue, min, max);
					setValue(newValue);
					SetText(lineEdit, (newValue / (float)divisor).ToString(format));
				}
				else
				{
					SetText(lineEdit, (getValue() / (float)divisor).ToString(format));
				}
			});

			// Minus button
			var minusButton = Activator.CreateInstance(buttonType);
			SetText(minusButton, "-");
			SetCustomMinimumSize(minusButton, new Vector2(40, 0));
			ConnectPressed(minusButton, () =>
			{
				int newValue = Math.Max(min, getValue() - step);
				setValue(newValue);
				SetText(lineEdit, (newValue / (float)divisor).ToString(format));
			});
			AddChild(row, minusButton);

			AddChild(row, lineEdit);

			// Plus button
			var plusButton = Activator.CreateInstance(buttonType);
			SetText(plusButton, "+");
			SetCustomMinimumSize(plusButton, new Vector2(40, 0));
			ConnectPressed(plusButton, () =>
			{
				int newValue = Math.Min(max, getValue() + step);
				setValue(newValue);
				SetText(lineEdit, (newValue / (float)divisor).ToString(format));
			});
			AddChild(row, plusButton);

			return row;
		}

		private static object CreateSpacer(int height)
		{
			var controlType = AccessTools.TypeByName("Godot.Control");
			var spacer = Activator.CreateInstance(controlType);
			SetCustomMinimumSize(spacer, new Vector2(0, height));
			return spacer;
		}

		private static void UpdatePanelValues()
		{
			// Update all UI elements with current settings values
			if (_uiElements.TryGetValue("drawX_input", out var drawXInput))
				SetText(drawXInput, _currentSettings.DrawPileX.ToString());
			if (_uiElements.TryGetValue("drawY_input", out var drawYInput))
				SetText(drawYInput, _currentSettings.DrawPileY.ToString());
			if (_uiElements.TryGetValue("discardX_input", out var discardXInput))
				SetText(discardXInput, _currentSettings.DiscardPileX.ToString());
			if (_uiElements.TryGetValue("discardY_input", out var discardYInput))
				SetText(discardYInput, _currentSettings.DiscardPileY.ToString());
			if (_uiElements.TryGetValue("cardSize_input", out var cardSizeInput))
				SetText(cardSizeInput, _currentSettings.CardSize.ToString());
			if (_uiElements.TryGetValue("idleOpacity_input", out var idleOpacityInput))
				SetText(idleOpacityInput, ((int)(_currentSettings.IdleOpacity * 100)).ToString());
			if (_uiElements.TryGetValue("activeOpacity_input", out var activeOpacityInput))
				SetText(activeOpacityInput, ((int)(_currentSettings.ActiveOpacity * 100)).ToString());
			if (_uiElements.TryGetValue("idleDelay_input", out var idleDelayInput))
				SetText(idleDelayInput, (_currentSettings.IdleDelaySeconds).ToString("F1"));
		}

		// Reflection helper methods
		private static void SetText(object control, string text)
		{
			var textProperty = AccessTools.Property(control.GetType(), "Text");
			textProperty?.SetValue(control, text);
		}

		private static void SetVisible(object control, bool visible)
		{
			if (control == null)
			{
				return;
			}

			try
			{
				var visibleProperty = AccessTools.Property(control.GetType(), "Visible");
				if (visibleProperty != null && visibleProperty.CanWrite)
				{
					visibleProperty.SetValue(control, visible);
				}
			}
			catch (Exception ex)
			{
				GD.PrintErr($"[ModSettingsUI] Failed to set visibility: {ex.Message}");
			}
		}

		private static void SetCustomMinimumSize(object control, Vector2 size)
		{
			var sizeProperty = AccessTools.Property(control.GetType(), "CustomMinimumSize");
			sizeProperty?.SetValue(control, size);
		}

		private static void SetPosition(object control, Vector2 position)
		{
			var positionProperty = AccessTools.Property(control.GetType(), "Position");
			positionProperty?.SetValue(control, position);
		}

		private static void AddThemeFontSizeOverride(object control, object name, int size)
		{
			var method = AccessTools.Method(control.GetType(), "AddThemeFontSizeOverride");
			method?.Invoke(control, new object[] { name, size });
		}

		private static void AddChild(object parent, object child)
		{
			var addChild = AccessTools.Method(parent.GetType(), "AddChild", new[] { child.GetType() })
				?? parent.GetType()
					.GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
					.FirstOrDefault(method =>
						string.Equals(method.Name, "AddChild", StringComparison.Ordinal)
						&& method.GetParameters().Length > 0);

			if (addChild == null)
			{
				GD.PrintErr($"[ModSettingsUI] AddChild method not found on {parent.GetType().Name}");
				return;
			}

			var parameters = addChild.GetParameters();
			var args = new object[parameters.Length];
			args[0] = child;
			
			// Fill optional parameters with defaults
			for (var index = 1; index < parameters.Length; index++)
			{
				args[index] = parameters[index].HasDefaultValue
					? parameters[index].DefaultValue
					: parameters[index].ParameterType.IsValueType
						? Activator.CreateInstance(parameters[index].ParameterType)
						: null;
			}

			addChild.Invoke(parent, args);
		}

		private static void ConnectPressed(object button, Action callback)
		{
			var pressed = button.GetType().GetEvent("Pressed");
			if (pressed == null || pressed.EventHandlerType == null)
			{
				GD.PrintErr($"[ModSettingsUI] Pressed event not found on {button.GetType().Name}");
				return;
			}

			var delegateCallback = Delegate.CreateDelegate(pressed.EventHandlerType, callback.Target, callback.Method);
			pressed.AddEventHandler(button, delegateCallback);
		}

		private static void ConnectTextChanged(object lineEdit, Action<string> callback)
		{
			var textChanged = lineEdit.GetType().GetEvent("TextChanged");
			if (textChanged == null || textChanged.EventHandlerType == null)
			{
				GD.PrintErr($"[ModSettingsUI] TextChanged event not found on {lineEdit.GetType().Name}");
				return;
			}

			var delegateCallback = Delegate.CreateDelegate(textChanged.EventHandlerType, callback.Target, callback.Method);
			textChanged.AddEventHandler(lineEdit, delegateCallback);
		}

		private static object CreateStringName(string text)
		{
			var stringNameType = AccessTools.TypeByName("Godot.StringName");
			return Activator.CreateInstance(stringNameType, new object[] { text });
		}

		private static bool IsNodeValid(object node)
		{
			if (node == null)
			{
				return false;
			}

			try
			{
				// Check if the node has IsInsideTree method (Godot nodes)
				var isInsideTreeMethod = node.GetType().GetMethod("IsInsideTree", BindingFlags.Instance | BindingFlags.Public);
				if (isInsideTreeMethod != null)
				{
					var result = isInsideTreeMethod.Invoke(node, null);
					return result is bool isInside && isInside;
				}

				// Fallback: assume valid if we can access a basic property
				var nameProperty = AccessTools.Property(node.GetType(), "Name");
				if (nameProperty != null)
				{
					var name = nameProperty.GetValue(node);
					return name != null;
				}

				return true;
			}
			catch
			{
				return false;
			}
		}
	}
}
