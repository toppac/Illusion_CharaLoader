using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace CharaLoader
{
    public partial class ReCharaLoaderPlugin : BaseUnityPlugin
    {
        internal static new ManualLogSource Logger { get; private set; }
        internal static ConfigEntry<float> PanelSacle { get; private set; }
        internal static ConfigEntry<bool> CleanupBmt { get; private set; }
        internal static ConfigEntry<bool> AutoHidePanel { get; private set; }
        internal static ConfigEntry<KeyboardShortcut> HotKey { get; private set; }

        private void PluginConfig()
        {
            Logger = base.Logger;

            PanelSacle = Config.Bind("Window", "Window scale", 1.0f,
                new ConfigDescription("Window: scale with scaler factor.",
                new AcceptableValueRange<float>(0.5f, 4f)));

            AutoHidePanel = Config.Bind("window", "Auto Hide Window", true);

            CleanupBmt = Config.Bind("Config", "Clean up BookMark", true,
                "Clean up duplicate and invalid bookMark items.\rRun cleanup at studio startup.");

            HotKey = Config.Bind("Config", "HotKey",
                new KeyboardShortcut(KeyCode.C, KeyCode.LeftAlt));
        }

        private void ConfigEventBinding(bool unload)
        {
            if (unload)
            {
                PanelSacle.SettingChanged -= StudioView.ReScacleGui;
                AutoHidePanel.SettingChanged -= StudioView.SetHideButtonState;
                OnHotKeyDowned -= CallStudioGui;
                return;
            }
            PanelSacle.SettingChanged += StudioView.ReScacleGui;
            AutoHidePanel.SettingChanged += StudioView.SetHideButtonState;
            OnHotKeyDowned += CallStudioGui;

            //HotKey. += HotKeySettingChanged;
        }

        private void CallStudioGui(object sender, EventArgs e)
        {
            StudioView?.ToggleGui();
        }
    }
}
