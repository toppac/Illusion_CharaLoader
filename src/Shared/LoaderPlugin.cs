using BepInEx;
using KKAPI.Studio;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CharaLoader
{
    public partial class ReCharaLoaderPlugin : BaseUnityPlugin
    {
        internal static DirectoryInfo FemaleBaseDir
            = new DirectoryInfo(UserData.Path + "chara/female");
        internal static DirectoryInfo MaleBaseDir
            = new DirectoryInfo(UserData.Path + "chara/male");

        public static ReCharaLoaderPlugin Instance { get; private set; }
        
        internal ReCharaLoaderStudio StudioView;

        internal event EventHandler OnHotKeyDowned;

        private void Awake()
        {
            if (Instance != null)
                return;
            Instance = this;
            PluginConfig();

            if (StudioAPI.InsideStudio)
            {
                StudioView = gameObject.AddComponent<ReCharaLoaderStudio>();
                if (StudioAPI.StudioLoaded)
                {
                    StudioView.LoadGui();
                }
                else
                {
                    StudioAPI.StudioLoadedChanged += (object o, EventArgs e) 
                        => StudioView.LoadGui();
                }
                // safe binding config event.
                return;
            }

            //KKAPI.Maker.MakerAPI.MakerFinishedLoading
            KKAPI.Maker.MakerAPI.MakerBaseLoaded += InitMainPlay;
            KKAPI.Maker.MakerAPI.MakerExiting += ClearMainPlay;
#if HS2
            SceneManager.sceneLoaded += On_SceneLoaded;
#endif
        }

        private void Update()
        {
            if (HotKey.Value.IsDown())
            {
                OnHotKeyDowned?.Invoke(null, null);
            }
        }

        private void InitMainPlay(object sender, EventArgs e)
        {
            StudioView = gameObject.GetOrAddComponent<ReCharaLoaderStudio>();
            if (StudioView)
            {
                ConfigEventBinding(false);
                if (!StudioView.LoaderCanvas)
                    StudioView.LoadGui();
            }
        }

        private void ClearMainPlay(object sender, EventArgs e)
        {
            if (StudioView)
            {
                ConfigEventBinding(true);
                StudioView.ResetLoader();
            }
        }

#if HS2
        private void On_SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;
            if (HSceneTools.HSceneLoaded)
            {
#if DEBUG
                Logger.LogMessage("Loaded HScene!");
#endif
                InitMainPlay(null, null);
                return;
            }
            var gmod = KKAPI.KoikatuAPI.GetCurrentGameMode();
            if (!(gmod == KKAPI.GameMode.Studio || gmod == KKAPI.GameMode.Maker))
            {
                ClearMainPlay(null, null);
            }
        }
#endif
            }
}
