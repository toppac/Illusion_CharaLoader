#if AI || HS2
using AIChara;
using Studio;
#endif
using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using UniRx;
using UniRx.Toolkit;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CharaLoader
{
    public enum DetailLevel
    {
        None,
        Basic,
        Preview,
    }

    public sealed class CharaItem
    {
        public const int PNG_MAX_LENGTH = 10485760;

        // Sex == 0 ? Male : Female
        public bool IsPng { get; private set; } = false;
        public byte[] Datas { get; private set; }

        public byte Sex = byte.MaxValue;
        public bool EvoVersion;
        public string CharaName;

        public CharaItem() { }

        public CharaItem(byte sex, string chaName)
        {
            Sex = sex;
            CharaName = chaName;
        }

        private static readonly ArrayPool<byte> _poolArr = ArrayPool<byte>.Shared;

        public bool GetPngData(string fileFullPath)
        {
            if (!File.Exists(fileFullPath)) return false;
            using (var fs = File.OpenRead(fileFullPath))
            {
                using (var br = new BinaryReader(fs))
                {
                    var dataPos = (int)PngTools.GetPngSize(br);

#if DEBUG
                    if (dataPos > PNG_MAX_LENGTH) {
                        UnityEngine.Debug.Log($"Over 6MB: {dataPos} \r{Path.GetFileName(fileFullPath)}");
                        return false;
                    }
#endif

                    if (dataPos == 0) return false;
                    return GetPngData(br, dataPos);
                }
            }
        }

        //[MethodImpl(MethodImplOptions.Synchronized)]
        public bool GetPngData(BinaryReader br, int dataLength)
        {
            if (dataLength > PNG_MAX_LENGTH) return false;
            Datas = _poolArr.Rent(dataLength);

            try
            {
                if (br.BaseStream.Read(Datas, 0, dataLength) == dataLength)
                    IsPng = true;
                else DropDatas();
            }
            catch { DropDatas(); }
            return IsPng;
        }

        public void DropDatas()
        {
            if (Datas != null)
            {
                _poolArr.Return(Datas);
                Datas = null;
            }
        }

        public void Clear()
        {
            DropDatas();
            CharaName = null;
            IsPng = false;
        }
    }

    public class VirtualItem<T> where T : class
    {
        public T ItemData { get; protected set; }
        public int ObjIndex { get; protected set; }
        public int DataIndex { get; protected set; }
        public Vector2 InitPos { get; protected set; }
        public DetailLevel BindState { get; protected set; } = DetailLevel.None;
        public RectTransform RectTrans => _rectTrans;
        protected GameObject Obj;
        private RectTransform _rectTrans;

        public virtual void Init(GameObject obj)
        {
            Obj = obj;
            _rectTrans = Obj.transform as RectTransform;
        }

        public virtual void Init(GameObject obj, int objIndex)
        {
            Init(obj);
            ObjIndex = objIndex;
        }

        public virtual void Init(GameObject obj, int objIndex, Vector2 pos)
        {
            Init(obj, objIndex);
            InitPos = pos;
        }

        public virtual void Refresh()
        {
            BindState = DetailLevel.None;
        }

        public virtual void DelayRefresh()  { }

        public void SetItemData(T data)
        {
            ItemData = data;
            Refresh();
        }

        public void SetItemData(T data, int dataIndex)
        {
            DataIndex = dataIndex;
            SetItemData(data);
        }

        public void SetInitPos()
        {
            RectTrans.anchoredPosition = InitPos;
        }

        public void ReSetInitPos()
        {
            InitPos = RectTrans.anchoredPosition;
        }

        public void ReSetInitPos(Vector2 pos)
        {
            InitPos = pos;
        }

        public virtual bool TryParseData(T item)
        {
            return false;
        }

        public virtual void FullClear()
        {
            if (Obj != null) GameObject.Destroy(Obj);
        }
    }

     public class ItemComa<T> : VirtualItem<T> where T : class
    {
#if PLUGIN
        public static ReCharaLoaderStudio Loader => LoaderTools.LoaderStudio;
#else
        public static ReCharaLoaderStudio Loader => ReCharaLoaderStudio.Instance;
#endif
        public static readonly Color InActive
            = new Color(0.70f, 0.70f, 0.70f, 0.4f);

        public static readonly Color Star = new Color(1, 0.64f, 0);
        public static readonly Color UnStar = new Color(0.556f, 0.556f, 0.556f);

        public static readonly Color Junked = new Color(1, 0.8f, 0.8f, 1);

        public static readonly Color UnCard = new Color(1, 1, 0.6f, 0.5f);
        public static readonly Color EvoCard = new Color(1, 0.72f, 0.4f, 1);

        public static readonly Color ClearBlue = new Color(0.36f, 0.62f, 1, 0.4f);

        public static readonly Texture2D DefTex2D
            = new Texture2D(2, 2, TextureFormat.RGB24, false);
    }

    internal sealed class CharaComa : ItemComa<string>
    {
        private GameObject Cover;
        private Text NameText;
        private RawImage CoverRawImage;
        private RawImage BG;

        //private Button LoadBtn;

        private Button StarBtn;
        private Image StarImg;

        //private AspectRatioFitter AspectRF;

        //private ClickListener SaveBtnEvn;

        private UIEventListener CharaEvn;
        private CanvasGroup CanGroup;

        public bool IsJunk { get; private set; }
        public bool IsStart { get; private set; } = false;

        private bool _ForceRefresh;

        private long _LastUpdateTime;

        private IDisposable _DropImageDatas;

        public CharaItem CharaItem => _charaData;

        private readonly Texture2D CoverTex2D = new Texture2D(2, 2, TextureFormat.RGB24, true);

        public static HashSet<string> RecycleItems { get; private set; } = new HashSet<string>();

        private readonly CharaItem _charaData = new CharaItem();

        public override void Init(GameObject obj, int objIndex)
        {
            base.Init(obj, objIndex);

            var chara = obj.transform;
            CanGroup = obj.AddComponent<CanvasGroup>();

            NameText = chara.Find("Text").GetComponent<Text>();

            CoverRawImage = chara.Find("Raw").GetComponent<RawImage>();
            CoverRawImage.texture = DefTex2D;

            //AspectRF = CoverRawImage.GetComponent<AspectRatioFitter>();

            BG = chara.Find("BG").GetComponent<RawImage>();
            var bgEvn = BG.gameObject.AddComponent<ClickListener>();
            bgEvn.RClick += Returnable;

            CharaEvn = chara.Find("Padding").gameObject.AddComponent<UIEventListener>();

            var coverTrans = CharaEvn.transform.Find("Cover");
            Cover = coverTrans.gameObject;

            StarBtn = coverTrans.Find("Star").GetComponent<Button>();
            StarBtn.onClick.AddListener(StarItem);
            StarImg = StarBtn.GetComponent<Image>();

#if PLUGIN
            if (Loader.GMod == KKAPI.GameMode.Maker
                || KKAPI.Maker.MakerAPI.InsideMaker)
            {
                InitMakerCover(coverTrans);
                goto Aa;
            }
#endif
            var loadBtn = coverTrans.Find("LoadBtn").GetComponent<Button>();
            loadBtn.onClick.AddListener(LoadChara);

#if HS2
            if (HSceneTools.HSceneLoaded)
            {
                InitHScene(coverTrans);
                goto Aa;
            }
#endif

#if PLUGIN
            var replaceBtn = coverTrans.Find("ReplaceBtn").GetComponent<Button>();
            replaceBtn.onClick.AddListener(ReplaceChara);

            if (CharaTools.StudioCharaListUtilType == null)
            {
                coverTrans.Find("Panel").gameObject.SetActive(false);
                goto Aa;
            }

            var anatomyBtn = coverTrans.Find("Panel/AnatomyBtn");
            var anatomyEvn = anatomyBtn.gameObject.AddComponent<ClickListener>();
            anatomyEvn.LClick += LoadAnatomy;
            anatomyEvn.RClick += LoadBodyOverlay;

            var outfitBtn = anatomyBtn.parent.Find("OutfitBtn");
            var outfitEvn = outfitBtn.gameObject.AddComponent<ClickListener>();
            outfitEvn.LClick += LoadOutfit;
            outfitEvn.RClick += LoadClothOverlay;

            var bodyBtn = outfitBtn.parent.Find("BodyBtn");
            var bodyEvn = bodyBtn.gameObject.AddComponent<ClickListener>();
            bodyEvn.LClick += LoadBody;
            bodyEvn.RClick += LoadBodyBone;

            var faceBtn = bodyBtn.parent.Find("FaceBtn");
            var faceEvn = faceBtn.gameObject.AddComponent<ClickListener>();
            faceEvn.LClick += LoadFace;
            faceEvn.RClick += LoadFaceBone;

            var accBtn = faceBtn.parent.Find("AccBtn").GetComponent<Button>();
            accBtn.onClick.AddListener(LoadAcc);

            var clothBtn = accBtn.transform.parent.Find("ClothBtn").GetComponent<Button>();
            clothBtn.onClick.AddListener(LoadCloth);

            var hairBtn = clothBtn.transform.parent.Find("HairBtn").GetComponent<Button>();
            hairBtn.onClick.AddListener(LoadHair);

        Aa:
#endif
            CharaEvn.OnMouseExit += InActiveCover;
            //CharaEvn.LClick += ActiveCover;
            CharaEvn.MClick += OpenFolder;

            SetVisible(false);
            obj.SetActive(true);
        }

#if PLUGIN

        public void InitMakerCover(Transform coverTrans)
        {
            var loadEvn = coverTrans.Find("LoadBtn").gameObject.AddComponent<ClickListener>();
            loadEvn.LClick += (e) => { LoadChara(); };
            loadEvn.RClick += LoadCharaOnOption;

            var replaceBtn = coverTrans.Find("ReplaceBtn").GetComponent<Button>();
            replaceBtn.onClick.AddListener(LoadChaStates);
            replaceBtn.GetComponentInChildren<Text>().text = "States";

            var panel = coverTrans.Find("Panel");
            var anatomBtn = panel.Find("AnatomyBtn").GetComponent<Button>();
            anatomBtn.onClick.AddListener(LoadBodyKeepShader);
            anatomBtn.GetComponentInChildren<Text>().text = "KsBody";

            var outfitBtn = panel.Find("OutfitBtn").GetComponent<Button>();
            outfitBtn.onClick.AddListener(LoadFaceKeepShader);
            outfitBtn.GetComponentInChildren<Text>().text = "KsFace";

            var bodyBtn = panel.Find("BodyBtn");
            var bodyEvn = bodyBtn.gameObject.AddComponent<ClickListener>();
            bodyEvn.LClick += LoadBody;
            bodyEvn.RClick += LoadBodyBone;

            var faceEvn = panel.Find("FaceBtn").gameObject.AddComponent<ClickListener>();
            faceEvn.LClick += LoadFace;
            faceEvn.RClick += LoadFaceBone;

            var clothEvn = panel.Find("ClothBtn").gameObject.AddComponent<ClickListener>();
            clothEvn.LClick += (e) => { LoadCloth(); };
            clothEvn.RClick += LoadClothOverlay;

            var accBtn = panel.Find("AccBtn").GetComponent<Button>();
            accBtn.onClick.AddListener(LoadAcc);

            var hairBtn = panel.Find("HairBtn").GetComponent<Button>();
            hairBtn.onClick.AddListener(LoadHair);
        }

        public void InitHScene(Transform coverTrans)
        {
            coverTrans.Find("ReplaceBtn").gameObject.SetActive(false);
            coverTrans.Find("Panel").gameObject.SetActive(false);
        }

#endif

        public override void Refresh()
        {
            base.Refresh();
            Clear();
            if (ItemData == null)
            {
                _ForceRefresh = true;
                return;
            }
            DataBind();
            _ForceRefresh = false;
        }

        public override void DelayRefresh()
        {
            if (BindState == DetailLevel.Basic)
            {
#if PLUGIN
                BepInEx.ThreadingHelper.Instance.StartAsyncInvoke(GetCoverImage);
#else
                SetCoverImage();
#endif
            }
        }

#if !PLUGIN
        private void SetCoverImage()
        {
            BindState = DetailLevel.Preview;
            //UnityEngine.Debug.Log($"Main: {Thread.CurrentThread.ManagedThreadId}");

            var obs = Observable.ToAsync(() => _charaData.GetPngData(ItemData))()
                .SubscribeOnMainThread().Subscribe(SetCoverImage).AddTo(Obj);
            _DropImageDatas = obs;
        }

        private void SetCoverImage(bool isPng)
        {
            //UnityEngine.Debug.Log("Start Load Image.");
            if (_charaData.IsPng && _charaData.Datas != null)
            {
                //.Debug.Log(Thread.CurrentThread.ManagedThreadId);

                if (CoverTex2D.LoadImage(_charaData.Datas))
                {
                    CoverRawImage.texture = CoverTex2D;
                }
                
                CharaEvn.LClick += ActiveCover;
                CharaEvn.LClick += ActiveStarButton;
            }
            else BG.color = UnCard;

            _charaData.DropDatas();
            _LastUpdateTime = DateTime.Now.Ticks;
        }
#endif

        private void DataBind()
        {
#if !PLUGIN
            NameText.text = Path.GetFileName(ItemData);
            SetVisible(true);
            if (RecycleItems.Contains(ItemData)) {
                IsJunk = true;
                BG.color = Junked;
            }
            BindState = DetailLevel.Basic;

            if (_ForceRefresh || (TimeSpan.FromTicks(DateTime.Now.Ticks - _LastUpdateTime).TotalMilliseconds > 320d))
            {
                SetCoverImage();
            } 
#else
            SetVisible(true);
            if (RecycleItems.Contains(ItemData)) {
                IsJunk = true;
                BG.color = Junked;
            }
            BindState = DetailLevel.Basic;

            if (_ForceRefresh || (TimeSpan.FromTicks(DateTime.Now.Ticks - _LastUpdateTime).TotalMilliseconds > 320d))
            {
                BepInEx.ThreadingHelper.Instance.StartAsyncInvoke(GetCoverImage);
            } else {
                NameText.text = Path.GetFileName(ItemData); 
            }
#endif
        }

#if PLUGIN
        private Action GetCoverImage()
        {
            BindState = DetailLevel.Preview;
            ReadFileInfo.LoadFile(ItemData, _charaData);
            return SetBindData;
        }

        private void SetBindData()
        {
            if (_charaData.CharaName != null)
            {
                CharaEvn.LClick += ActiveCover;
                CharaEvn.LClick += ActiveStarButton;

                if (_charaData.Sex == 0) BG.color = ClearBlue;
                if (_charaData.EvoVersion)  BG.color = EvoCard;
                NameText.text = _charaData.CharaName;

                if (_charaData.IsPng && _charaData.Datas != null &&
                    CoverTex2D.LoadImage(_charaData.Datas))
                    CoverRawImage.texture = CoverTex2D;
                else CoverRawImage.texture = DefTex2D;
            } else {
                NameText.text = Path.GetFileName(ItemData);
                BG.color = UnCard;
            }
            _charaData.DropDatas();
            _LastUpdateTime = DateTime.Now.Ticks;
        }
#endif

        public override bool TryParseData(string cardPath)
        {
#if PLUGIN
            if (ReadFileInfo.LoadFile(cardPath, _charaData)) return true;
            _charaData.Clear();
            return false;
#elif IL
            var cf = new ChaFile();
            if (cf.LoadFile(cardPath, 0))
            {
                _charaData.Sex = cf.parameter.sex;
                _charaData.CharaName = cf.parameter.fullname;
                _charaData.PngData = cf.pngData;
                return true;
            }
#else
            if (_charaData.GetPngData(cardPath)) return true;
            return false;
#endif
        }

        private void SetVisible(bool visible)
        {
            CanGroup.alpha = visible ? 1 : 0;
            CanGroup.interactable = visible;
            CanGroup.blocksRaycasts = visible;
        }

        private void Clear()
        {
            _DropImageDatas?.Dispose();
            _DropImageDatas = null;
            SetVisible(false);
            _charaData.Clear();
            CharaEvn.LClick -= ActiveCover;
            CharaEvn.LClick -= ActiveStarButton;

            IsJunk = false;
            BG.color = InActive;
            NameText.text = string.Empty;
            CoverRawImage.texture = DefTex2D;
        }

#if PLUGIN
        public override void FullClear()
        {
            _charaData.CharaName = null;
            base.FullClear();
            CharaItem.Clear();
            if (CoverTex2D != null)
                GameObject.Destroy(CoverTex2D);
        }
#endif

        private void ActiveStarButton(PointerEventData e)
        {
            if (Loader.CurrentBookMark == null) return;
            if (Loader.CurrentBookMark.Find(LoaderTools.GetRelativePath(ItemData)))
            {
                StarImg.color = Star;
                IsStart = true;
            }
            else
            {
                StarImg.color = UnStar;
                IsStart = false;
            }
        }

        private void StarItem()
        {
            if (Loader.CurrentBookMark == null) return;
            if (IsStart)
            {
                Loader.CurrentBookMark.Remove(
                    LoaderTools.GetRelativePath(ItemData));
                StarImg.color = UnStar;
            }
            else
            {
#if PLUGIN
                Loader.CurrentBookMark.Add(
                    _charaData.CharaName,
                    LoaderTools.GetRelativePath(ItemData), null);
#else
                Loader.CurrentBookMark.Add(
                    Path.GetFileName(ItemData),
                    LoaderTools.GetRelativePath(ItemData));
#endif
                StarImg.color = Star;
            }
        }

        private void OpenFolder(PointerEventData e)
        {
            Process.Start("explorer.exe", $"/e,/select, {ItemData}");
        }

        private void Returnable(PointerEventData e)
        {
            if (BG.color == UnCard) return;
            if (IsJunk)
            {
                RecycleItems.Remove(ItemData);
                BG.color = InActive;
                IsJunk = false;
                return;
            }

            if (RecycleItems.Add(ItemData))
            {
                IsJunk = true;
                BG.color = Junked;
#if DEBUG
                UnityEngine.Debug.Log("Re count: " + RecycleItems.Count);
#endif
            }
        }

        private void ActiveCover(PointerEventData e)
        {
            Cover.SetActive(true);
        }

        private void InActiveCover(PointerEventData e)
        {
            Cover.SetActive(false);
        }

#if PLUGIN

        private void LoadAnatomy(PointerEventData e)
        {
            CharaTools.LoadAnatomy(ItemData);
        }

        private void LoadFace(PointerEventData e)
        {
            if (Loader.GMod == KKAPI.GameMode.Maker)
            {
                if (Loader.QuickLoad)
                {
                    MakerTools.Instance?.LoadReplaceParts(ItemData, (int)LOpt.Face);
                    return;
                }
                MakerTools.Instance?.LoadCustom(ItemData, (int)LOpt.Face);
                return;
            }
            CharaTools.LoadFace(ItemData);
        }

        private void LoadFaceBone(PointerEventData e)
        {
            if (Loader.GMod == KKAPI.GameMode.Maker)
            {
                if (Loader.QuickLoad)
                {
                    goto Aa;
                }
                MakerTools.Instance?.LoadCustom(ItemData, (int)LOpt.FaceBone);
                return;
            }
        Aa:
            var cf = new ChaFile();
            cf.LoadFile(ItemData, 0, true, true);
            CharaTools.GetAbmxData(cf, _charaData.Sex, true, false);
        }

        private void LoadBody(PointerEventData e)
        {
            if (Loader.GMod == KKAPI.GameMode.Maker)
            {
                if (Loader.QuickLoad)
                {
                    MakerTools.Instance?.LoadReplaceParts(ItemData, (int)LOpt.Body);
                    return;
                }
                MakerTools.Instance?.LoadCustom(ItemData, (int)LOpt.Body);
                return;
            }
            CharaTools.LoadBody(ItemData);
        }

        private void LoadBodyBone(PointerEventData e)
        {
            if (Loader.GMod == KKAPI.GameMode.Maker)
            {
                if (Loader.QuickLoad)
                {
                    goto Aa;
                }
                MakerTools.Instance?.LoadCustom(ItemData, (int)LOpt.BodyBone);
                return;
            }
        Aa:
            var cf = new ChaFile();
            cf.LoadFile(ItemData, 0, true, true);
            CharaTools.GetAbmxData(cf, _charaData.Sex, false, true);
        }

        private void LoadBodyOverlay(PointerEventData e)
        {
            if (Loader.GMod == KKAPI.GameMode.Maker)
            {
                MakerTools.Instance?.LoadCustom(ItemData, (int)LOpt.SkinOver);
                return;
            }
            var cf = new ChaFile();
            cf.LoadFile(ItemData, 0, true, true);
            CharaTools.LoadBodyTex(cf, _charaData.Sex);
        }

        private void LoadClothOverlay(PointerEventData e)
        {
            if (Loader.GMod == KKAPI.GameMode.Maker)
            {
                MakerTools.Instance?.LoadCustom(ItemData, (int)LOpt.ClothOver);
                return;
            }
            var cf = new ChaFile();
            cf.LoadFile(ItemData, 0, true, true);
            CharaTools.LoadClothTex(cf, _charaData.Sex);
        }

        private void LoadOutfit(PointerEventData e)
        {
            CharaTools.LoadOutfit(ItemData);
        }

        private void LoadHair()
        {
            if (Loader.GMod == KKAPI.GameMode.Maker)
            {
                if (Loader.QuickLoad)
                {
                    MakerTools.Instance?.LoadReplaceParts(ItemData, (int)LOpt.Hair);
                    return;
                }
                MakerTools.Instance?.LoadCustom(ItemData, (int)LOpt.Hair);
                return;
            }
            CharaTools.LoadHair(ItemData);
        }

        private void LoadCloth()
        {
            if (Loader.GMod == KKAPI.GameMode.Maker)
            {
                if (Loader.QuickLoad)
                {
                    MakerTools.Instance?.LoadReplaceParts(ItemData, (int)LOpt.Cloth);
                    return;
                }
                MakerTools.Instance?.LoadCustom(ItemData, (int)LOpt.Cloth);
                return;
            }
            CharaTools.LoadCloth(ItemData);
        }

        private void LoadAcc()
        {
            if (Loader.GMod == KKAPI.GameMode.Maker)
            {
                MakerTools.Instance?.LoadReplaceParts(ItemData, (int)LOpt.Accessory);
                return;
            }
            CharaTools.LoadAcc(ItemData);
        }

        private void ReplaceChara()
        {
            var array = Singleton<GuideObjectManager>.Instance.selectObjectKey
                .Select(v => Studio.Studio.GetCtrlInfo(v) as OCIChar)
                .Where(v => v != null)
                .Where(v => v.oiCharInfo.sex == _charaData.Sex).ToArray();

            var length = array.Length;
            for (var index = 0; index < length; ++index)
                array[index].ChangeChara(ItemData);

            if (length > 0)
            {
                Loader.HideOrShowPanel();
            }
        }

        private void LoadChaStates()
        {
            MakerTools.Instance?.LoadCustom(ItemData, (int)LOpt.ChaParam);
        }

        private void LoadFaceKeepShader()
        {
            MakerTools.Instance?.LoadCustom(ItemData, (int)LOpt.Face);
        }

        private void LoadBodyKeepShader()
        {
            MakerTools.Instance?.LoadCustom(ItemData, (int)LOpt.Body);
        }

        private void LoadCharaOnOption(PointerEventData e)
        {
            MakerTools.Instance?.LoadAllOption(ItemData);
        }

#endif

        private void LoadChara()
        {
#if PLUGIN
            if (Loader.GMod == KKAPI.GameMode.Maker)
            {
                MakerTools.Instance?.LoadAllOption(ItemData);
                return;
            }

#if HS2
            if (HSceneTools.HSceneIns)
            {
                Loader.StartCoroutine(HSceneTools.ChangeCharacter(ItemData, _charaData.Sex));
                Loader.HideOrShowPanel();
                return;
            }
#endif
            Singleton<Studio.Studio>.Instance.AddFemale(ItemData);
#endif
            Loader.HideOrShowPanel();
        }
    }

    public class LoopList<TItem, TData> where TItem : VirtualItem<TData>, new() where TData : class
    {
        private PanelOffset _border;

        /// <summary>
        /// ViewPort per row max item: 4
        /// </summary>
        private int RowNum = 4;

        private float ItemWidth = 100;
        private float ItemHeight = 100;

        private float _viewHeight;
        private float _itemScale = 1;

        private int ItemNum = 16;
        public int ViewItemNum = 16;

        private int _topLeftIndex = 0;

        private int _lineCount = 0;
        private int _dataCount = 0;

        private int _endPos = 0;
        private int _readCount;
        private int _dataPos;

        private int HeadDataId = 0;
        private int TailDataId = 0;

        private float _dragY = 1;
        private bool _isUp = true;
        private bool _isInit = false;

        private RectTransform _contentRect;
        private RectTransform _viewRect;

        private readonly LinkedList<RectTransform> _itemsRect = new LinkedList<RectTransform>();

        private readonly Dictionary<GameObject, VirtualItem<TData>> _itemsView = new Dictionary<GameObject, VirtualItem<TData>>();

        private IList<TData> _dataList;

        public ref PanelOffset Border => ref _border;
        public int EndPos => _endPos;

        public GameObject ItemPrefab { get; private set; }
        public ScrollRect ScrollComp { get; private set; }

        public IList<TData> DataList => _dataList;

        //public event EventHandler OnScrollViewSwaped;

        public void InitView(GameObject ItemObj, ScrollRect scrollComp, RectTransform ViewRectTrans, PanelOffset border)
        {
            if (_isInit) return;
            ItemPrefab = ItemObj;
            ScrollComp = scrollComp;
            _contentRect = ScrollComp.content;

            if (ViewRectTrans == null)
            {
                ViewRectTrans = ScrollComp.transform as RectTransform;
            }
            _viewRect = ViewRectTrans;
            _viewHeight = ViewRectTrans.rect.height;

            if (border.Equals(PanelOffset.Zero))
            {
                _border = new PanelOffset(4, 4, 4, 4, new Vector2(4, 4));
            }
            _border = border;

            SetItemSize(4);
            ItemNum = _lineCount * RowNum;

            for (var i = 0; i < ItemNum; i++)
            {
                var obj = UnityEngine.Object.Instantiate(ItemPrefab);
                var rectTrans = obj.GetComponent<RectTransform>();

                _itemsRect.AddLast(rectTrans);
                var vItem = new TItem();
                rectTrans.SetParent(_contentRect, false);
                SetItemPos(i, rectTrans);

                vItem.Init(obj, i, rectTrans.anchoredPosition);
                _itemsView.Add(obj, vItem);
            }

            _isInit = true;
        }

        private void SetItemSize(int rowNum)
        {
            var num = rowNum > 4 || rowNum < 1 ? 4 : rowNum;
            var rect = (ItemPrefab.transform as RectTransform).rect;

            //float scale = 4f / num;
            float scale = _viewRect.rect.width / (rect.width * num + _border.Space.x * (num - 1) + _border.Horizontal);

            _itemScale = scale;
            ItemWidth = (rect.width * scale);
            ItemHeight = (rect.height * scale);

            //_lineCount = (int)(_viewHeight / (ItemHeight + _border.Space.y + _border.Vertical)) + 2;
            _lineCount = Mathf.CeilToInt(_viewHeight / (ItemHeight + _border.Space.y + _border.Vertical)) + 1;
            RowNum = num;
        }

        private void SetContentHeight()
        {
            var size = _contentRect.sizeDelta;

            int dataCount;
            if (_dataPos + _readCount <= _endPos)
            {
                dataCount = _readCount;
            }
            else
            {
                dataCount = _endPos - _dataPos;
            }

            int lineCount;
            if (dataCount % RowNum > 0)
            {
                lineCount = dataCount / RowNum + 1;
            }
            else
            {
                lineCount = dataCount / RowNum;
            }

            size.y = (_border.Space.y + ItemHeight) * lineCount + _border.Vertical;
            _contentRect.sizeDelta = size;
        }

        private void SetItemPos(int index, RectTransform rectTrans)
        {
            var iPos = rectTrans.anchoredPosition;

            var iNum = index % RowNum;
            iPos.x = iNum * ItemWidth + iNum * _border.Space.x + _border.Left;

            var lineNum = index / RowNum;
            iPos.y = -(lineNum * ItemHeight + lineNum * _border.Space.y);
            iPos.y -= _border.Top;

            rectTrans.anchoredPosition = iPos;
            rectTrans.localScale = new Vector3(_itemScale, _itemScale, 1);
        }

        private float GetContentPosY() => _contentRect.anchoredPosition.y;

        private float GetItemPosY(bool firstItem)
        {
            RectTransform rectTrans;
            if (firstItem)
            {
                rectTrans = _itemsRect.First.Value;
                HeadDataId = _itemsView[rectTrans.gameObject].DataIndex;
                return rectTrans.anchoredPosition.y;
            }
            rectTrans = _itemsRect.Last.Value;
            TailDataId = _itemsView[rectTrans.gameObject].DataIndex;
            return rectTrans.anchoredPosition.y;
        }

        private void SwapItemView(bool next)
        {
            if (next)
            {
                var nId = _lineCount * RowNum + _topLeftIndex;
                for (var i = 0; i < RowNum; i++)
                {
                    var itemRect = _itemsRect.First.Value;

                    var iPos = itemRect.anchoredPosition;
                    iPos.y -= _lineCount * (ItemHeight + _border.Space.y);
                    itemRect.anchoredPosition = iPos;

                    _itemsRect.RemoveFirst();
                    _itemsRect.AddLast(itemRect);
                    SetItemData(_itemsView[itemRect.gameObject], nId + i);
                }
                return;
            }

            var pId = _topLeftIndex - 1;
            for (var j = 0; j < RowNum; j++)
            {
                var itemRect = _itemsRect.Last.Value;

                var iPos = itemRect.anchoredPosition;
                iPos.y += _lineCount * (ItemHeight + _border.Space.y);
                itemRect.anchoredPosition = iPos;

                _itemsRect.RemoveLast();
                _itemsRect.AddFirst(itemRect);
                SetItemData(_itemsView[itemRect.gameObject], pId - j);
            }
        }

        private void SetItemData(VirtualItem<TData> item, int index)
        {
            if (index > -1 && index < _endPos)
            {
                item.SetItemData(DataList[index], index);
                return;
            }
            item.SetItemData(null);
        }

        private IDisposable _obsTimer;

        public void ScrollValueChanged(Vector2 deltaPos)
        {
            if (!_isInit) return;
            if (_dragY == deltaPos.y) return;
            _isUp = _dragY > deltaPos.y;
            _dragY = deltaPos.y;

            var moveLength = Mathf.Clamp01(1 - deltaPos.y)
                * (_contentRect.sizeDelta.y - _viewRect.rect.height);

            bool swaped = false;

            for (; ; )
            {
                if (_isUp)
                {
                    var f1 = _viewHeight + GetItemPosY(false) + moveLength;
                    if (f1 > ItemHeight && TailDataId < _endPos - 1)
                    {

                        SwapItemView(true);
                        _topLeftIndex += RowNum;
                        swaped = true;
                        continue;
                    }
                }

                var f2 = -GetItemPosY(true) - moveLength;
                if (f2 > _border.Space.y && HeadDataId > 0)
                {

                    SwapItemView(false);
                    _topLeftIndex -= RowNum;
                    swaped = true;
                    continue;
                }
                break;
            }

            if (!swaped) return;
            _obsTimer?.Dispose();
            //OnScrollViewSwaped?.Invoke(null, null);
            _obsTimer = Observable.Timer(TimeSpan.FromMilliseconds(500d)).Subscribe(_ =>
            {
                foreach (var kv in _itemsView)
                    kv.Value.DelayRefresh();
            });
        }

        private void ClearItemData()
        {
            foreach (var item in _itemsView)
            {
                item.Value.SetItemData(null);
            }
            _itemsRect.Clear();
        }

        public void Clear()
        {
            _obsTimer?.Dispose();
            _obsTimer = null;
            _isInit = false;
            _isUp = true;
            _dragY = 1;
            _topLeftIndex = 0;

            ScrollComp.onValueChanged.RemoveAllListeners();
            ClearItemData();

            var size = _contentRect.sizeDelta;
            size.y = 0;
            _contentRect.sizeDelta = size;
        }

        public void FullClear()
        {
            _obsTimer?.Dispose();
            _obsTimer = null;
            _isInit = false;
            _isUp = true;
            _dragY = 1;
            _topLeftIndex = 0;
            _dataList = null;

            foreach (var kvItem in _itemsView)
            {
                kvItem.Value.FullClear();
                if (kvItem.Key)
                    GameObject.Destroy(kvItem.Key);
            }
            _itemsView.Clear();
            _itemsRect.Clear();
        }

        public void SetView(int rowNum)
        {
            if (!_isInit) return;
            Clear();
            SetItemSize(rowNum);

            var id = 0;
            foreach (var iv in _itemsView)
            {
                SetItemPos(id, iv.Value.RectTrans);
                iv.Value.ReSetInitPos();
                ++id;
            }

            SetView(_dataPos, _readCount);
        }

        public void SetView(List<TData> datas)
        {
            SetView(datas, 0, datas.Count);
        }

        public void SetView(List<TData> datas, int dataPos, int readCount)
        {
            Clear();
            var dataCount = datas.Count;
            if (datas == null || dataCount < 1) return;
            _dataCount = dataCount;
            _dataList = datas;
            SetView(dataPos, readCount);
        }

        public void SetView(int dataPos, int readCount)
        {
            _endPos = dataPos + readCount < _dataCount ? readCount + dataPos : _dataCount;

            _dataPos = dataPos;
            _readCount = readCount;
            _topLeftIndex = dataPos;

            foreach (var iv in _itemsView)
            {
                iv.Value.SetInitPos();
                _itemsRect.AddLast(iv.Value.RectTrans);

                if (dataPos < _endPos)
                {
                    iv.Value.SetItemData(DataList[dataPos], dataPos);
                    ++dataPos;
                }
            }

            SetContentHeight();
            ScrollComp.verticalNormalizedPosition = 1;
            _isInit = true;
            ScrollComp.onValueChanged.AddListener(ScrollValueChanged);
        }

        public IEnumerator JumpToDataIndex(int dataIndex)
        {
            if (_lineCount < 1) yield break;
            var LineNum = (dataIndex) / RowNum;
            var posY = Mathf.Abs(
                LineNum * (ItemHeight + _border.Space.y) + _border.Vertical);

            if (posY > _contentRect.rect.height - _viewHeight)
            {
                ScrollComp.verticalNormalizedPosition = 0;
                yield break;
            }
            var op = GetContentPosY();
            var delay = 0.1f;
            var passedTime = 0f;
            float currY;
            while (true)
            {
                yield return null;
                passedTime += Time.deltaTime;
                if (passedTime >= delay)
                {
                    _contentRect.anchoredPosition = new Vector2(0, posY);
                    yield break;
                }
                currY = Mathf.Lerp(op, posY, passedTime / delay);
                _contentRect.anchoredPosition = new Vector2(0, currY);
            }
        }

        public VirtualItem<TData> GetItemOfDataIndex(int dataIndex)
        {
            if (_endPos < dataIndex) return null;
            foreach (var v in _itemsView)
            {
                var iDat = v.Value?.ItemData;
                if (ReferenceEquals(iDat, DataList[dataIndex]))
                {
                    return v.Value;
                }
            }
            return null;
        }
    }

    public struct PanelOffset
    {
        /// <summary>
        ///  Left edge size.
        /// </summary>        
        public int Left { get; set; }

        /// <summary>
        /// Right edge size.
        /// </summary>        
        public int Right { get; set; }

        /// <summary>
        /// Top edge size.
        /// </summary>        
        public int Top { get; set; }

        /// <summary>
        /// Bottom edge size.
        /// </summary>        
        public int Bottom { get; set; }

        /// <summary>
        /// Shortcut for left + right. (Read Only)
        /// </summary>        
        public int Horizontal => Left + Right;

        /// <summary>
        /// Shortcut for top + bottom. (Read Only)
        /// </summary>        
        public int Vertical => Top + Bottom;

        /// <summary>
        /// Element around padding.
        /// </summary>
        public Vector2 Space { get; set; }

        public PanelOffset(
            int left, int right, int top, int bottom, Vector2 space)
        {
            Left = left;
            Right = right;
            Top = top;
            Bottom = bottom;
            Space = space;
        }

        public static PanelOffset Zero => new PanelOffset(0, 0, 0, 0, new Vector2(0, 0));
    }
}
