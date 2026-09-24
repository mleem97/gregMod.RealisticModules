using System;
using MelonLoader;
using UnityEngine;
using UnityEngine.UIElements;

namespace GregModMoreModules
{
    // gregCore F1-Hub + Settings-Hub. Nur aufrufen, wenn GregHost.HasCore
    // (eigene Methoden wegen JIT-Trennung ohne gregCore-DLL).
    internal static class GregCoreIntegration
    {
        private const string MenuId = "realisticmodules";
        private static gregCore.UI.GregPanel _panel;
        private static bool _registered;

        internal static void Register()
        {
            if (_registered) return;
            try
            {
                gregCore.Core.Mods.GregModRegistry.Register(
                    "gregMod.RealisticModules", "RealisticModules", "1.3.0",
                    new string[] { MenuId });

                gregCore.UI.GregMenuRegistry.RegisterMenu(MenuId, DefaultOptions());

                gregCore.UI.GregMenuRegistry.RegisterOpener(MenuId, TogglePanel);
                gregCore.UI.GregMenuRegistry.RegisterCloser(MenuId, HidePanel);

                RegisterSettingsTab();
                _registered = true;
                MelonLogger.Msg("[RealisticModules] F1 hub + settings tab registered.");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[RealisticModules] gregCore registration failed: " +
                                    ex.GetBaseException().Message);
            }
        }

        private static gregCore.UI.GregMenuOptions DefaultOptions()
        {
            return new gregCore.UI.GregMenuOptions
            {
                LockCamera = true,
                LockMovement = true,
                LockInteract = true,
                ShowCursor = true,
                Draggable = true,
                PanelWidth = 380f,
            };
        }

        // F8 settings hub (same values as the F1 panel).
        private static void RegisterSettingsTab()
        {
            try
            {
                greg.UI.Settings.GregSettingsHub.RegisterTab("realisticmodules.settings",
                    "RealisticModules",
                    (Action<gregCore.UI.GregPanelBuilder>)(b =>
                    {
                        b.AddToggle("Mod active", ModConfig.Enabled, v =>
                        {
                            ApplyEnabled(v);
                        });
                        b.AddToggle("Strict port compatibility", ModConfig.StrictCompatibility, v =>
                        {
                            ModConfig.SetStrict(v);
                            Core.RefreshCompatibilityMode();
                        });
                        b.AddLabel(ModConfig.Enabled
                            ? "Catalog injects on next scene load if it was off."
                            : "Inactive: shop and registry stay vanilla until re-enabled.");
                        b.AddLabel("— Mass Insert —");
                        b.AddButton("Mass Insert (fill empty ports)", () =>
                        {
                            MassInsert.Start(replaceOccupied: false);
                        });
                        b.AddButton("Mass Insert + Replace matching", () =>
                        {
                            MassInsert.Start(replaceOccupied: true);
                        });
                        b.AddLabel("Fill: catalog modules → empty cages with the same sfpType. " +
                                   "Replace also swaps occupied cages when the connector type still matches " +
                                   "and no cable is attached.");
                    }));
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[RealisticModules] Settings tab failed: " +
                                    ex.GetBaseException().Message);
            }
        }

        internal static void TogglePanel()
        {
            try
            {
                EnsurePanel();
                if (_panel == null) return;
                if (_panel.IsVisible) HidePanel();
                else
                {
                    RebuildPanelContent();
                    _panel.Show();
                    gregCore.UI.GregMenuRegistry.SetOpen(MenuId, true);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[RealisticModules] Panel toggle failed: " + ex.Message);
            }
        }

        internal static void HidePanel()
        {
            try
            {
                if (_panel == null) return;
                _panel.Hide();
                gregCore.UI.GregMenuRegistry.SetOpen(MenuId, false);
            }
            catch { /* best-effort */ }
        }

        private static void EnsurePanel()
        {
            if (_panel != null) return;
            _panel = gregCore.UI.GregPanel.GetOrCreate(MenuId, "REALISTIC MODULES",
                DefaultOptions());
        }

        private static void RebuildPanelContent()
        {
            if (_panel == null) return;
            _panel.RebuildContent(root =>
            {
                root.Clear();

                var title = new Label("RealisticModules");
                title.style.fontSize = 15;
                title.style.unityFontStyleAndWeight = FontStyle.Bold;
                title.style.color = new Color(0.53f, 0.81f, 0.92f, 1f);
                title.style.marginBottom = 8;
                root.Add(title);

                var status = new Label(ModConfig.Enabled
                    ? "Status: active — catalog in shop"
                    : "Status: inactive — vanilla catalog only");
                status.style.color = ModConfig.Enabled
                    ? new Color(0.4f, 0.9f, 0.5f, 1f)
                    : new Color(0.9f, 0.55f, 0.3f, 1f);
                status.style.fontSize = 12;
                status.style.marginBottom = 12;
                root.Add(status);

                var enabledToggle = new Toggle("Mod active")
                {
                    value = ModConfig.Enabled,
                };
                enabledToggle.RegisterCallback<ChangeEvent<bool>>(
                    new Action<ChangeEvent<bool>>(_ =>
                    {
                        ApplyEnabled(enabledToggle.value);
                        status.text = ModConfig.Enabled
                            ? "Status: active — catalog in shop"
                            : "Status: inactive — vanilla catalog only";
                        status.style.color = ModConfig.Enabled
                            ? new Color(0.4f, 0.9f, 0.5f, 1f)
                            : new Color(0.9f, 0.55f, 0.3f, 1f);
                    }));
                enabledToggle.style.marginBottom = 8;
                root.Add(enabledToggle);

                var strictToggle = new Toggle("Strict port compatibility")
                {
                    value = ModConfig.StrictCompatibility,
                };
                strictToggle.RegisterCallback<ChangeEvent<bool>>(
                    new Action<ChangeEvent<bool>>(e =>
                    {
                        ModConfig.SetStrict(e.newValue);
                        Core.RefreshCompatibilityMode();
                    }));
                strictToggle.style.marginBottom = 12;
                root.Add(strictToggle);

                var massHeader = new Label("Mass Insert");
                massHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
                massHeader.style.color = new Color(0.53f, 0.81f, 0.92f, 1f);
                massHeader.style.marginBottom = 6;
                root.Add(massHeader);

                root.Add(MakeButton("Fill empty ports", () => MassInsert.Start(false)));
                root.Add(MakeButton("Fill + replace matching", () => MassInsert.Start(true)));

                var statusLine = new Label(MassInsert.IsRunning ? "Status: mass insert running…" : "");
                statusLine.style.fontSize = 11;
                statusLine.style.color = new Color(0.75f, 0.85f, 0.9f, 1f);
                statusLine.style.marginBottom = 6;
                root.Add(statusLine);

                var hint = new Label(
                    "Fill: spawns catalog modules into empty cages with the same sfpType. " +
                    "Replace also swaps occupied cages when the connector still matches and no cable is attached. " +
                    "Strict mode rejects modules whose form factor does not match the host port.");
                hint.style.whiteSpace = WhiteSpace.Normal;
                hint.style.fontSize = 11;
                hint.style.color = new Color(0.75f, 0.75f, 0.75f, 1f);
                root.Add(hint);
            });
        }

        private static Button MakeButton(string text, Action onClick)
        {
            var btn = new Button(onClick) { text = text };
            btn.style.height = 32;
            btn.style.marginBottom = 6;
            btn.style.backgroundColor = new Color(0.15f, 0.35f, 0.5f, 1f);
            btn.style.color = Color.white;
            btn.style.borderTopLeftRadius = 4;
            btn.style.borderTopRightRadius = 4;
            btn.style.borderBottomLeftRadius = 4;
            btn.style.borderBottomRightRadius = 4;
            return btn;
        }

        private static void ApplyEnabled(bool value)
        {
            bool was = ModConfig.Enabled;
            ModConfig.SetEnabled(value);
            if (was == value) return;

            if (!value)
            {
                // Soft disable: registry/shop injection stop immediately.
                // Already-extended sfpPrefabs stay until the next Awake.
                Core.DisableAtRuntime();
            }
            else
            {
                // Soft re-enable: rebuild registry immediately if a scene is loaded.
                try
                {
                    var mgm = Il2Cpp.MainGameManager.instance;
                    if (mgm != null)
                        Core.SetupRegistry(mgm);
                }
                catch { /* setup will run again on next Awake */ }
            }

            try
            {
                gregCore.UI.GregNotificationManager.Show(
                    value
                        ? "RealisticModules active — catalog re-armed (reload scene if shop was already open)."
                        : "RealisticModules inactive — vanilla catalog after reload.",
                    4f);
            }
            catch { /* notification best-effort */ }
        }
    }
}
