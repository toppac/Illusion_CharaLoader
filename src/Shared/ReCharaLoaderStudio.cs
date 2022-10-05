#if PLUGIN
using KKAPI.Studio;
using KKAPI.Utilities;
using Studio;
#endif
using Common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if AI
using Properties = AI_CharaLoader.Properties;
using System.Threading.Tasks;
#elif HS2
using Properties = HS2_CharaLoader.Properties;
using System.Threading.Tasks;
#endif

namespace CharaLoader
{
    public class ReCharaLoaderStudio : MonoBehaviour
    {
        public const string Idtt = "CharaLoader";

        internal Transform MainPanel { get; private set; }
        internal bool IsFemale { get; private set; } = true;

        public DirectoryInfo CurrentDataInfo => IsFemale ? FemaleDir : MaleDir;

        public List<string> CharaPathArr { get; private set; } = new List<string>();

        private bool _autoRefresh = true;
        private bool _showLDiv = true;

        internal Transform ItemContent;
        internal Transform FolderContent;
        private Transform _refreshButton;

        private Thread _ratedDuty;

        private readonly TreeView tvp = new TreeView();
        private readonly LoopList<CharaComa, string> vll = new LoopList<CharaComa, string>();

#if PLUGIN
        internal bool QuickLoad { get; private set; }

        internal GameObject NodePrefab { get; private set; }
        internal GameObject ItemPrefab { get; private set; }
        internal GameObject MiniPrefab { get; private set; }
        internal Canvas LoaderCanvas { get; private set; }

        public KKAPI.GameMode GMod => KKAPI.KoikatuAPI.GetCurrentGameMode();
        internal static DirectoryInfo FemaleDir => ReCharaLoaderPlugin.FemaleBaseDir;
        internal static DirectoryInfo MaleDir => ReCharaLoaderPlugin.MaleBaseDir;
#else
        public GameObject MiniPrefab;
        public GameObject NodePrefab;
        public GameObject ItemPrefab;
        public GameObject CanvasPrefab;
        public Canvas LoaderCanvas;
        public string FemaleFolder;
        public string MaleFolder;

        internal DirectoryInfo FemaleDir;
        internal DirectoryInfo MaleDir;
#endif

#if HS2
        private CameraControl_Ver2 HSceneCam;
#endif
        

#if !PLUGIN
        private bool _autoHidePanel = true;

        public static ReCharaLoaderStudio Instance;   

        public void Awake()
        {
            if (Instance) return;
            Instance = this; 
        }

        public void Start()
        {

            LoaderCanvas = Instantiate(CanvasPrefab).GetComponent<Canvas>();
            MiniPrefab = Instantiate(MiniPrefab);
            DrawGui();
            InitBookMark();
        }
#endif

        internal BookMarkTool CurrentBookMark => IsFemale ? _fBmt : _mBmt;

        private BookMarkTool _fBmt;
        private BookMarkTool _mBmt;

#if PLUGIN
        internal void LoadGui()
        {
            InitObj();
            DrawPanel();
            // Stat game setting button State.
            SetHideButtonState(null, null);
            SetPanelOffset();
            InitBookMark();
            InitItemComa();

            if (GMod == KKAPI.GameMode.Studio)
                CreateMenuButton(false);
#if HS2
            else if (GMod == KKAPI.GameMode.Maker || GMod == KKAPI.GameMode.MainGame || HSceneTools.HSceneLoaded)
#else
            else if (GMod == KKAPI.GameMode.Maker || GMod == KKAPI.GameMode.MainGame)
#endif
            {
                CloseAction = () =>
                {
                    if (LoaderCanvas != null)
                    {
                        if (LoaderCanvas.gameObject.activeSelf)
                            LoaderCanvas.gameObject.SetActive(false);
                        else
                            LoaderCanvas.gameObject.SetActive(true);
                    }
                };
            }
        }
#endif
        internal void DrawGui()
        {
#if !PLUGIN
            DrawPanel();
            //SetPanelOffset();
            InitItemComa();
#endif

            LoadView();
            DrawTreeView();
        }

#if PLUGIN
        private void InitObj()
        {
            var ab = AssetBundle.LoadFromMemory(Properties.Resources.cloader);

            var lc = ab.LoadAsset<GameObject>("CharaLoaderLoop");
            LoaderCanvas = Instantiate(lc).gameObject.GetComponent<Canvas>();
            Destroy(lc);
            var ip = ab.LoadAsset<GameObject>("ChItemBtnLoop");
            ItemPrefab = Instantiate(ip.gameObject);
            Destroy(ip);

            var mp = ab.LoadAsset<GameObject>("CMiniIcon");
            MiniPrefab = Instantiate(mp.gameObject);
            Destroy(mp);

            var np = ab.LoadAsset<GameObject>("CNodeItem");
            NodePrefab = Instantiate(np.gameObject);
            Destroy(np);

            ab.Unload(false);
        }
#endif

        private void InitBookMark()
        {
            bool flag = false;
            if (_fBmt == null && FemaleDir.Exists)
            {
                flag = true;
                _fBmt = new BookMarkTool(
                    FemaleDir.FullName,
                    Endorse.FemaleBookMarkFileName);
            }
            if (_mBmt == null && MaleDir.Exists)
            {
                flag = true;
                _mBmt = new BookMarkTool(
                    MaleDir.FullName,
                    Endorse.MaleBookMarkFileName);
            }
#if !PLUGIN
            if (flag) BlockClear();
#endif
#if PLUGIN
            if (flag)
            {
                if (ReCharaLoaderPlugin.CleanupBmt.Value)
                {
                    BepInEx.ThreadingHelper.Instance
                        .StartAsyncInvoke(CleanupBookMark);
                    return;
                }
            }
            BlockClear();
#endif
        }

        private void BlockClear()
        {
            MainPanel.Find("LBlock").gameObject.SetActive(false);
        }
#if PLUGIN

        public Action CleanupBookMark()
        {
            Action result = BlockClear;
            try
            {
                Parallel.Invoke(
                    () => { _fBmt?.Cleaner(); },
                    () => { _mBmt?.Cleaner(); });
            }
            catch
            {
                return result;
            }
            return result;
        }
#endif

        private void DrawTreeView()
        {
            tvp.Content = FolderContent;
            tvp.NodeObj = NodePrefab;
            tvp.DrawGui(CurrentDataInfo);
        }

        private void SetPanelOffset()
        {
            var offsetY = 140;
            var OffsetX = 200;
            var mp = MainPanel as RectTransform;
            mp.sizeDelta = new Vector2(mp.sizeDelta.x, mp.sizeDelta.y + offsetY);
            var lDiv = FolderContent.parent.parent.parent as RectTransform;
            lDiv.sizeDelta = new Vector2(lDiv.sizeDelta.x, lDiv.sizeDelta.y + offsetY);
            var mDiv = ItemContent.parent.parent.parent as RectTransform;
            mDiv.sizeDelta = new Vector2(mDiv.sizeDelta.x, mDiv.sizeDelta.y + offsetY);
            var rDiv = mDiv.parent.Find("RDiv") as RectTransform;
            rDiv.sizeDelta = new Vector2(rDiv.sizeDelta.x, rDiv.sizeDelta.y + offsetY);
            var lH = rDiv.parent.Find("LHide") as RectTransform;
            lH.sizeDelta = new Vector2(lH.sizeDelta.x, lH.sizeDelta.y + offsetY);
            var rH = lH.parent.Find("RHide") as RectTransform;
            rH.sizeDelta = new Vector2(rH.sizeDelta.x, rH.sizeDelta.y + offsetY);
            var cb = mp.Find("Body/ClickBlock") as RectTransform;
            cb.sizeDelta = new Vector2(
                cb.sizeDelta.x - OffsetX, cb.sizeDelta.y + offsetY);
            var lb = mp.Find("LBlock") as RectTransform;
            lb.sizeDelta = new Vector2(lb.sizeDelta.x, lb.sizeDelta.y + offsetY);
        }

        private void DrawPanel()
        {
#if PLUGIN
            LoaderCanvas.gameObject.SetActive(false);
#endif
            MainPanel = LoaderCanvas.transform.Find(Idtt);

            FolderContent = MainPanel
                .Find("Body/Viewport/Content/LDiv/FolderTree/Viewport/Content");
            ItemContent = MainPanel
                .Find("Body/Viewport/Content/MDiv/ItemBrowser/Viewport/Content");

            MainPanel.gameObject.AddComponent<CanvasGroup>();
            FolderContent.parent.parent
                .GetComponent<Image>().color = new Color(1, 1, 1, 0);

            var title = MainPanel.Find("HeadHandle");
            var titleEvn = title.gameObject.AddComponent<DragEventListener>();
            titleEvn.Init(MainPanel as RectTransform);
            titleEvn.OnClick += (e) => { LoaderCanvas.sortingOrder = 101; };
            /*var titleEvn = title.gameObject.AddComponent<UIEventListener>();
            titleEvn.LClick += (e) => { LoaderCanvas.sortingOrder = 101; };
            titleEvn.Drag += (e) => {
                (MainPanel as RectTransform).anchoredPosition += e.delta;
            };*/

            _refreshButton = title.Find("RefreshBtn");
            var refreshEvn = _refreshButton
                .gameObject.AddComponent<ClickListener>();
            refreshEvn.LClick += (e) => RefreshLoader(true);
            refreshEvn.RClick += (e) =>
            {
                _autoRefresh = !_autoRefresh;
                if (_autoRefresh)
                {
                    _refreshButton.GetComponent<Image>()
                        .color = new Color(0.7f, 0.77f, 1);
                    return;
                }
                _refreshButton.GetComponent<Image>().color = Color.white;
            };

            var close = title.Find("CloseBtn").GetComponent<Button>();
            close.onClick.AddListener(CloseGui);

            MiniPrefab.AddComponent<CanvasGroup>();
            HideOrShowPanel(MiniPrefab);
            MiniPrefab.transform.SetParent(LoaderCanvas.transform, false);
            var miniIconEvn = MiniPrefab.AddComponent<MiniDragListener>();
            miniIconEvn.OnClick += (e) => { HideOrShowPanel(true); };
            /*miniIconEvn.Drag += (e) => {
                LoaderTools.OnDragClamp(e, MiniPrefab.transform as RectTransform);
            };*/

            var MiniBtnEvn = title.Find("MiniBtn").gameObject
                .AddComponent<ClickListener>();
            MiniBtnEvn.LClick += (e) =>
            {
                (MiniPrefab.transform as RectTransform)
                    .position = Input.mousePosition;
                HideOrShowPanel(true);
            };
            MiniBtnEvn.RClick += (e) =>
            {
#if PLUGIN
                ReCharaLoaderPlugin.AutoHidePanel.Value
                    = !ReCharaLoaderPlugin.AutoHidePanel.Value;
#else
                _autoHidePanel = !_autoHidePanel;
#endif
                SetHideButtonState(null, null);
            };

#if PLUGIN
            if (GMod != KKAPI.GameMode.Studio)
            {
                var quickLoadBtn = title.Find("QuickLoad").GetComponent<Button>();
                
                var colors = quickLoadBtn.colors;
                colors.pressedColor = Color.white;
                colors.disabledColor = Color.white;
                colors.highlightedColor = Color.white;
                quickLoadBtn.colors = colors;

                quickLoadBtn.onClick.AddListener(() =>
                {
                    QuickLoad = !QuickLoad;
                    var img = quickLoadBtn.GetComponent<RawImage>();
                    if (QuickLoad)
                    {
                        img.color = new Color(7f, 0.768f, 1, 1);
                        return;
                    }
                    img.color = Color.white;
                });

                quickLoadBtn.gameObject.SetActive(true);
            }

#endif

            var genderBtn = title.Find("GenderBtn").GetComponent<Button>();
            genderBtn.onClick.AddListener(GenderToggle);

            var lHide = MainPanel.transform
                .Find("Body/Viewport/Content/LHide/LHBtn").GetComponent<Button>();
            lHide.onClick.AddListener(LeftDivHide);

            var refreshDirBtn = FolderContent.transform.parent.parent.parent
                .Find("RefreshDirBtn").GetComponent<Button>();
            refreshDirBtn.onClick.AddListener(RefreshTreeView);

            var recycleBinEvn = refreshDirBtn.transform.parent
                .Find("RecycleBinBtn").gameObject.AddComponent<ClickListener>();
            recycleBinEvn.LClick += LoadRecycleBinButton;
            recycleBinEvn.RClick += (e) => SetDialogActive(true);

            var dialog = MainPanel.Find("Body/ClickBlock/Dialog");
            var dialogEvn = dialog.gameObject.AddComponent<UIEventListener>();
            dialogEvn.Drag += (e) =>
            {
                LoaderTools.OnDragLocalClamp(
                    e, dialog.parent as RectTransform, dialog as RectTransform);
            };
            dialog.Find("ApplyBtn").GetComponent<Button>()
                .onClick.AddListener(CleanRecycleBinButton);
            dialog.Find("CancelBtn").GetComponent<Button>()
                .onClick.AddListener(() => { SetDialogActive(false); });
        }

        private void CleanRecycleBinButton()
        {
            CleanRecycleBin();
            SetDialogActive(false);
            RefreshLoader(false);
        }

        private void CleanRecycleBin()
        {
            if (!CharaComa.RecycleItems.Any()) return;
            try
            {
                foreach (var fp in CharaComa.RecycleItems)
                    if (File.Exists(fp)) File.Delete(fp);
            }
            finally
            {
                CharaComa.RecycleItems.Clear();
            }
        }

        private void SetDialogActive(bool active)
        {
            MainPanel.Find("Body/ClickBlock").gameObject.SetActive(active);
        }

        private void LoadRecycleBinButton(PointerEventData e)
        {
            tvp.ClearInfo();
            tvp.LoadOption = LoadType.RecycleBin;
            LoadView(tvp.LoadOption);
        }

        internal void HideOrShowPanel()
        {
#if PLUGIN
            if (ReCharaLoaderPlugin.AutoHidePanel.Value)
#else
            if (_autoHidePanel)

#endif
            {
                HideOrShowPanel(true);
                MiniPrefab.GetComponent<RectTransform>()
                    .position = Input.mousePosition;
            }
        }

        private void HideOrShowPanel(bool activeMiniIcon)
        {
            HideOrShowPanel(MainPanel.gameObject);
            if (activeMiniIcon) HideOrShowPanel(MiniPrefab.gameObject);
        }

        private void HideOrShowPanel(GameObject obj)
        {
            var canvas = obj.GetComponent<CanvasGroup>();
            if (canvas.interactable)
            {
                canvas.alpha = 0;
                canvas.interactable = false;
                canvas.blocksRaycasts = false;
                return;
            }
            canvas.alpha = 1;
            canvas.interactable = true;
            canvas.blocksRaycasts = true;
        }

        internal void SetHideButtonState(object o, EventArgs e)
        {
            if (!LoaderCanvas) return;
            var miniBtnImg = MainPanel
                .Find("HeadHandle/MiniBtn").GetComponent<Image>();
#if PLUGIN
            if (ReCharaLoaderPlugin.AutoHidePanel.Value)
#else
            if (_autoHidePanel)
#endif
            {
                miniBtnImg.color = new Color(0.7f, 0.77f, 1);
                return;
            }
            miniBtnImg.color = Color.white;
        }

        private void CloseGui()
        {
#if PLUGIN
            CloseAction?.Invoke();
#else
            LoaderCanvas.gameObject.SetActive(false);
#endif
        }

        private void LeftDivHide()
        {
            _showLDiv = MiniPanel(MainPanel,
                FolderContent.parent.parent.parent, _showLDiv, false, 2);
        }

        internal void RefreshLoader()
        {
            if (!_autoRefresh) return;
            RefreshLoader(true);
        }

        private void RefreshLoader(bool reloadTreeView)
        {
            if (reloadTreeView) RefreshTreeView();
            switch (tvp.LoadOption)
            {
                case LoadType.Normal:
                    LoadView(tvp.CurrentPath);
                    break;
                case LoadType.Root:
                    LoadView(tvp.LoadOption);
                    break;
                case LoadType.All:
                    break;
                case LoadType.Book:
                    LoadView(tvp.LoadOption);
                    break;
                case LoadType.RecycleBin:
                    LoadView(tvp.LoadOption);
                    break;
                default:
                    break;
            }
        }

        private void RefreshTreeView() => tvp.GetDir(CurrentDataInfo);

        internal IEnumerator LoaddingAnime(bool reset)
        {
            if (_ratedDuty == null) yield break;
            yield return LoaddingAnime(() =>
            {
                if (_ratedDuty.IsAlive) return true;
                return false;
            }, reset);
        }

        internal IEnumerator LoaddingAnime(Func<bool> func, bool reset)
        {
            while (func())
            {
                _refreshButton.Rotate(200 * Time.deltaTime * Vector3.forward);
                yield return LoaderTools.WaitForEndOfFrame;
            }
            if (reset) ResetRefreshButton(); 
            yield break;
        }

        internal void ResetRefreshButton()
        {
            _refreshButton.eulerAngles = Vector3.zero;
        }

        private void InitItemComa()
        {
            var scrollComp = ItemContent.parent.parent.GetComponent<ScrollRect>();
            var rectTran = scrollComp.transform.parent as RectTransform;

            var right = (int)(scrollComp.verticalScrollbar.transform as RectTransform).rect.width;

            var offset = new PanelOffset(6, right, 0, 0, new Vector2(8, 8));
            vll.InitView(ItemPrefab, scrollComp, rectTran, offset);
        }

        internal void LoadView()
        {
            LoadView(LoadType.Normal, CurrentDataInfo);
        }

        internal void LoadView(LoadType loadType)
        {
            LoadView(loadType, CurrentDataInfo);
        }

        internal void LoadView(string dirPath)
        {
            LoadView(LoadType.Normal, new DirectoryInfo(dirPath));
        }

        internal void LoadView(LoadType loadType, DirectoryInfo dirInfo)
        {
            FreeDutyCycle();
            if (!dirInfo.Exists) return;
            //StartCoroutine(LoaddingAnime(false));
            var thd = new Thread(new ThreadStart(() => { LoadCharaPathArr(loadType, dirInfo);  }))
            { IsBackground = true };

            StartCoroutine(StartNewTask(thd));
        }

        private IEnumerator StartNewTask(Thread thread)
        {
            yield return FreeDutyCycleWaiting();
            thread.Start();
            _ratedDuty = thread;
            yield return StartLoadding(true, SetViewItems);
        }

        private IEnumerator StartLoadding(bool reset, Action callBack)
        {
            yield return LoaddingAnime(reset);
            callBack?.Invoke();
        }

        private void LoadCharaPathArr(LoadType loadType, DirectoryInfo dirInfo)
        {
            switch (loadType)
            {
                case LoadType.Normal:
                    LoadCharaPathArr(dirInfo);
                    break;
                case LoadType.Book:
                    LoadBookMarkItems();
                    break;
                case LoadType.RecycleBin:
                    LoadRecycleBinItems();
                    break;
                default:
                    return;
            }
        }

        private void LoadBookMarkItems()
        {
            var result = CharaPathArr;
            result.Clear();
            foreach (var rp in CurrentBookMark.GetBookPath())
            {
                var fp = LoaderTools.GetFileFullPath(rp);
                if (File.Exists(fp))
                    result.Add(fp);
            }
#if DEBUG
            print("Book count: " + CharaPathArr.Count);
#endif
        }

        private void LoadRecycleBinItems()
        {
#if DEBUG
            print("Load count: " + CharaComa.RecycleItems.Count);
#endif
            var result = CharaPathArr;
            result.Clear();
            foreach (var item in CharaComa.RecycleItems)
            {
                result.Add(string.Copy(item));
            }
        }

        private void LoadCharaPathArr(DirectoryInfo dirInfo)
        {
            var result = CharaPathArr;
            result.Clear();
            foreach (var dir in dirInfo.GetFiles().OrderByDescending(x => x.LastWriteTime))
            {
                if (dir.Extension.ToLower() == ".png")
                {
                    result.Add(dir.FullName);
                }
            }
#if DEBUG
            //Debug.Log(CharaPathArr.Count);
#endif
        }

        private void SetViewItems()
        {
#if DEBUG
            //Debug.Log("SetView!");
#endif
            if (CharaPathArr.Count < 1)
            {
#if DEBUG
                //Debug.Log("Clear!");
#endif
                Clear();
                return;
            }
            try
            {
                vll.SetView(CharaPathArr);
            }
            catch
            {
                Clear();
            }

        }

        private void GenderToggle()
        {
            var genderText = MainPanel
                .Find("HeadHandle/GenderBtn/Text").GetComponent<Text>();

            if (IsFemale && FemaleDir.Exists)
            {
                genderText.text = "Female";
                IsFemale = false;
                LoadView();
                RefreshTreeView();
                return;
            }

            if (!MaleDir.Exists) return;
            genderText.text = "Male";
            IsFemale = true;
            LoadView();
            RefreshTreeView();
        }

        private bool MiniPanel(Transform mainTrans, Transform subTrans, bool isShow, bool flip, float offset)
        {
            var mainRect = mainTrans.GetComponent<RectTransform>();
            var subRect = subTrans.GetComponent<RectTransform>();
            float mainWidth = mainRect.rect.width;
            float subWidth = subRect.rect.width;
            var width = subWidth + offset;
            Vector3 posOffset = new Vector3(width / 2, 0, 0);
            if (isShow)
            {
                subRect.gameObject.SetActive(false);
                mainRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, mainWidth - width);
                mainRect.position = flip ? mainRect.position - posOffset : mainRect.position + posOffset;
                return false;
            }
            mainRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, mainWidth + width);
            mainRect.position = flip ? mainRect.position + posOffset : mainRect.position - posOffset;
            subRect.gameObject.SetActive(true);
            return true;
        }

        private void FreeDutyCycle()
        {
            if (_ratedDuty == null) return;
            if (_ratedDuty.ThreadState != ThreadState.Stopped
                && _ratedDuty.ThreadState != ThreadState.Aborted)
            {
#if DEBUG
                print($"Free: {_ratedDuty.ThreadState} | {_ratedDuty.IsAlive}");
#endif
                _ratedDuty.Abort();
            }
        }

        private IEnumerator FreeDutyCycleWaiting()
        {
            if (_ratedDuty == null) yield break;
            if (_ratedDuty.IsAlive)
            {
                _ratedDuty.Abort();
                while (_ratedDuty.IsAlive)
                {
                    yield return LoaderTools.WaitForEndOfFrame;
                }
            }
        }

        private void Clear()
        {
            FreeDutyCycle();
            vll.Clear();
        }

#if PLUGIN
        internal void ResetLoader()
        {
            if (!IsInitialized) return;
#if DEBUG
            ReCharaLoaderPlugin.Logger.LogMessage("Reset Loader!");
#endif
            FreeDutyCycle();
            CharaPathArr.Clear();
            _ratedDuty = null;
            _showLDiv = true;
            QuickLoad = false;
            _autoRefresh = true;
            _viewLoaded = false;
            CloseAction = null;
            tvp?.FullClear();
            vll?.FullClear();
            CharaComa.RecycleItems.Clear();
            MakerTools.Instance.Clear();
        }

        public bool IsInitialized => CloseAction != null;
        private GameObject _menuButton;
        private bool _viewLoaded;
        private Action CloseAction;

        private void CreateMenuButton(bool replaceButton)
        {
            // charLoader replace Female card button.
            // safe find frame button.
            var org = GameObject.Find("StudioScene/Canvas Main Menu/01_Add/Scroll View Add Group/Viewport/Content/Frame");
            if (org == null)
            {
                ReCharaLoaderPlugin.Logger.LogError("could not find Source Button");
                return;
            }

            // Parent = Scroll Content
            _menuButton = Instantiate(org, org.transform.parent);
            _menuButton.name = "ReCharaLoader";
            _menuButton.GetComponentInChildren<TMP_Text>().text = "ReCharaLoader";
            var menuBtn = _menuButton.GetComponent<Button>();

            if (replaceButton)
            {
                _menuButton.transform.SetSiblingIndex(0);
                org.transform.SetParent(null);
                GameObject.Find("StudioScene/Canvas Main Menu/01_Add/Scroll View Add Group/Viewport/Content/Chara Male").transform.SetParent(null);
            }

            var addBtnCtrlType = typeof(AddButtonCtrl);
            var commonInfoType = addBtnCtrlType.GetNestedType(
                "CommonInfo", BindingFlags.NonPublic);

            var commInfo = Activator.CreateInstance(commonInfoType);
            commonInfoType.GetField("obj")
                .SetValue(commInfo, LoaderCanvas.gameObject);
            commonInfoType.GetField("button").SetValue(commInfo, menuBtn);

            var addBtnCtrl = GameObject.Find("StudioScene/Canvas Main Menu/01_Add")
                .GetComponent<AddButtonCtrl>();
            var commInfoField = addBtnCtrlType.GetField("commonInfo",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var commInfoList = (Array)commInfoField.GetValue(addBtnCtrl);

            var length = commInfoList.Length;
            var newArr = (Array)Activator.CreateInstance(
                commInfoList.GetType(), length + 1);
            for (var i = 0; i < length; i++)
            {
                newArr.SetValue(commInfoList.GetValue(i), i);
            }

            newArr.SetValue(commInfo, length);
            commInfoField.SetValue(addBtnCtrl, newArr);

            menuBtn.onClick.ActuallyRemoveAllListeners();
            menuBtn.onClick.AddListener(ToggleGui);
            CloseAction = () => addBtnCtrl.OnClick(length);
        }

        internal void ToggleGui()
        {
#if HS2
            if (StudioAPI.StudioLoaded || KKAPI.Maker.MakerAPI.InsideAndLoaded || HSceneTools.HSceneIns)
#else
            if (StudioAPI.StudioLoaded || KKAPI.Maker.MakerAPI.InsideAndLoaded)
#endif
            {
                if (!IsInitialized) return;
                
                if (!_viewLoaded)
                {
                    _viewLoaded = true;
#if HS2
                    if (!HSceneCam && HSceneTools.HSceneIns)
                    {
                        HSceneCam = Camera.main.GetComponent<CameraControl_Ver2>();
                    }
                    if (HSceneCam != null)
                    {
                        BlockCamCtrl.Get(
                            LoaderCanvas.gameObject,
                            () => GlobalMethod.setCameraMoveFlag(HSceneCam,
                            false));
                    }
#endif
                    DrawGui();
                }

                CloseAction();
                ReScacleGui(null, null);
            }
        }

        internal void ReScacleGui(object o, EventArgs eventArgs)
        {
            if (!LoaderCanvas) return;
            LoaderCanvas.scaleFactor = ReCharaLoaderPlugin.PanelSacle.Value;
            MiniPrefab.transform.localScale = Vector3.one;
        }
#endif
    }
}
