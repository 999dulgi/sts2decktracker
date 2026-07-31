using System;
using Godot;
using HarmonyLib;

namespace sts2decktracker
{
	[HarmonyPatch]
	public static class ModdingScreenSettingsPatch
	{
		static System.Reflection.MethodInfo TargetMethod()
		{
			var type = AccessTools.TypeByName("MegaCrit.Sts2.Core.Nodes.Screens.ModdingScreen.NModInfoContainer");
			var modType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Modding.Mod");
			return AccessTools.Method(type, "Fill", new[] { modType });
		}

		static void Postfix(object __instance, object mod)
		{
			try
			{
				ModdingScreenSettingsUi.RefreshForSelection(__instance, mod);
			}
			catch (Exception ex)
			{
				GD.PrintErr($"[ModdingScreenSettingsPatch] Failed to inject settings UI: {ex.Message}");
				GD.PrintErr($"[ModdingScreenSettingsPatch] Stack trace: {ex.StackTrace}");
			}
		}
	}

	public static class ModdingScreenSettingsUi
	{
		private static ModSettingsPanelNode _currentPanel = null;

		public static void RefreshForSelection(object infoContainer, object mod)
		{
			try
			{
				var container = (Node)infoContainer;

				if (!IsThisMod(mod))
				{
					SetDefaultNodesVisible(container, true);
					if (IsPanelValid() && _currentPanel.IsInsideTree())
						_currentPanel.Visible = false;
					return;
				}

				SetDefaultNodesVisible(container, false);

				if (!IsPanelValid() || !_currentPanel.IsInsideTree())
				{
					_currentPanel = null;
					_currentPanel = CreateSettingsPanel(container);
				}
				else
				{
					_currentPanel.Refresh();
					_currentPanel.Visible = true;
				}
			}
			catch (Exception ex)
			{
				GD.PrintErr($"[ModdingScreenSettingsUi] Error in RefreshForSelection: {ex.Message}");
				GD.PrintErr($"[ModdingScreenSettingsUi] Stack trace: {ex.StackTrace}");
			}
		}

		private static bool IsThisMod(object mod)
		{
			if (mod == null)
				return false;
			try
			{
				var modType = mod.GetType();
				var bindingFlags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

				// 게임 버전에 따라 Mod.assemblies(List<Assembly>, 신버전) 또는
				// Mod.assembly(Assembly, 구버전) 중 존재하는 필드를 사용한다.
				var assembliesField = modType.GetField("assemblies", bindingFlags);
				if (assembliesField?.GetValue(mod) is System.Collections.IEnumerable assemblies)
				{
					foreach (var assembly in assemblies)
					{
						string val = assembly?.ToString() ?? "";
						if (val.Contains("sts2decktracker") || val.Contains("Slay the Spire 2 Deck Tracker"))
							return true;
					}
				}

				var assemblyField = modType.GetField("assembly", bindingFlags);
				if (assemblyField != null)
				{
					string val = assemblyField.GetValue(mod)?.ToString() ?? "";
					if (val.Contains("sts2decktracker") || val.Contains("Slay the Spire 2 Deck Tracker"))
						return true;
				}
			}
			catch (Exception ex)
			{
				GD.PrintErr($"[ModdingScreenSettingsUi] Error checking mod: {ex.Message}");
			}
			return false;
		}

		private static bool IsPanelValid() =>
			_currentPanel != null && GodotObject.IsInstanceValid(_currentPanel);

		private static void SetDefaultNodesVisible(Node container, bool visible)
		{
			foreach (var name in new[] { "ModTitle", "ModImage", "ModDescription" })
				container.GetNodeOrNull(name)?.Set("visible", visible);
		}

		private static ModSettingsPanelNode CreateSettingsPanel(Node container)
		{
			var scroll = new ScrollContainer
			{
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
				SizeFlagsVertical = Control.SizeFlags.ExpandFill
			};
			scroll.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

			var panel = new ModSettingsPanelNode { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
			scroll.AddChild(panel);

			container.AddChild(scroll);
			return panel;
		}
	}
}
