using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;
using System.Reflection;
using System.IO;
using System.Runtime.CompilerServices;
#if PLUGIN
using MessagePack;
using AIChara;
using ExtensibleSaveFormat;
using IllusionUtility.GetUtility;
using KKABMX.Core;
using KoiClothesOverlayX;
using KoiSkinOverlayX;
using KKAPI.Maker;
using Studio;
using UnityEngine.UI;
using CharaCustom;
using KK_Plugins.MaterialEditor;
using System.Runtime.InteropServices;
using MessagePack.Internal;
using HarmonyLib;
using Manager;
#endif
#if HS2
using WearCustom;
#elif AI
using AIWearCustom;
#endif

namespace CharaLoader
{
    public class OrdinalIgnoreCaseComparer : IEqualityComparer<string>
    {
        public bool Equals(string x, string y)
        {
            return string.Compare(x, y, StringComparison.OrdinalIgnoreCase) == 0;
        }

        public int GetHashCode(string obj)
        {
            return obj.GetHashCode();
        }
    }

    public static class LoaderTools
    {
#if PLUGIN
        
        public static ReCharaLoaderPlugin LoaderIns => ReCharaLoaderPlugin.Instance;
        public static ReCharaLoaderStudio LoaderStudio => LoaderIns.StudioView;
#else
        public static ReCharaLoaderStudio LoaderStudio => ReCharaLoaderStudio.Instance;
#endif

        public static string GetRelativePath(string fileFullPath)
        {
            return fileFullPath.Substring(
                LoaderStudio.CurrentDataInfo.FullName.Length + 1);
        }

        public static string GetFileFullPath(string relativePath)
        {
            return Path.Combine(
                LoaderStudio.CurrentDataInfo.FullName, relativePath);
        }

#if PLUGIN
        public static WaitForEndOfFrame WaitForEndOfFrame
            = KKAPI.Utilities.CoroutineUtils.WaitForEndOfFrame;
#else
        public static WaitForEndOfFrame WaitForEndOfFrame = new WaitForEndOfFrame();
#endif
        public static void OnDragClamp(PointerEventData eventData, RectTransform rectTrans)
        {
            Vector3 pos = rectTrans.position + (Vector3)eventData.delta;
            float w = rectTrans.rect.width / 2;
            float h = rectTrans.rect.height / 2;
            pos.x = Mathf.Clamp(pos.x, w, Screen.width - w);
            pos.y = Mathf.Clamp(pos.y, h, Screen.height - h);
            rectTrans.position = pos;
        }

        public static void OnDragClamp(Vector2 offset, RectTransform rectTrans)
        {
            var pos = (Vector3)offset;
            float w = rectTrans.rect.width / 2;
            float h = rectTrans.rect.height / 2;
            pos.x = Mathf.Clamp(pos.x, w, Screen.width - w);
            pos.y = Mathf.Clamp(pos.y, h, Screen.height - h);
            rectTrans.position = pos;
        }

        public static void OnDragLocalClamp(PointerEventData eventData, RectTransform sourceRectTrans, RectTransform destRectTrans)
        {
            Vector3 pos = destRectTrans.localPosition + (Vector3)eventData.delta;

            float w = -((sourceRectTrans.rect.width - destRectTrans.rect.width) / 2);
            float h = -((sourceRectTrans.rect.height - destRectTrans.rect.height) / 2);
            pos.x = Mathf.Clamp(pos.x, w, w * -1);
            pos.y = Mathf.Clamp(pos.y, h, h * -1);
            destRectTrans.localPosition = pos;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Big2LittleInt32(int big)
        {
            return ((big >> 24) & 0xff)
                | ((big >> 8) & 0xFF00)
                | ((big << 8) & 0xFF0000)
                | (big << 24);
        }
    }

    public static class PngTools
    {
        public const long PNG_IDTT = 727905341920923785;
        public const int PNG_END_IDTT = 1145980233;

        

        public static long GetPngSize(BinaryReader br)
        {
            var result = 0L;
            var bs = br.BaseStream;
            long pos = bs.Position;
            try
            {
                if (br.ReadInt64() == PNG_IDTT)
                {
                    for (; ; )
                    {
                        var chunk = LoaderTools.Big2LittleInt32(br.ReadInt32());
                        var int_32 = br.ReadInt32();
                        bs.Position += chunk + 4;
                        if (int_32 == PNG_END_IDTT) break;
                    }
                    result = bs.Position - pos;
                }
            }
            finally
            {
                bs.Position = pos;
            }
            return result;
        }
    }

#if PLUGIN

    [Flags]
    public enum LOpt
    {
        NuN,
        Face,
        Body,
        Hair = 4,
        Cloth = 8,
        ChaParam = 16,
        ClothOver = 32,
        SkinOver = 64,
        FaceBone = 128,
        BodyBone = 256,
        Accessory = 512
    }

    public static class ReadFileInfo
    {
        public static bool LoadFile(string fileFullPath, CharaItem item)
        {
            if (!File.Exists(fileFullPath)) return false;
            bool result;
            using (var fs = new FileStream(
                fileFullPath, FileMode.Open, FileAccess.Read))
            {
                result = LoadFile(fs, item);
            }
            return result;
        }

        public static bool LoadFile(Stream st, CharaItem item)
        {
            bool result;
            using (var br = new BinaryReader(st))
            {
                result = LoadFile(br, item);
            }
            return result;
        }

        public static bool LoadFile(BinaryReader br, CharaItem item)
        {
            var dataPos = (int)PngTools.GetPngSize(br);

            if (dataPos != 0)
                item.GetPngData(br, dataPos);
             else 
                br.BaseStream.Position = 0;

            try
            {
                if (br.ReadInt32() > 100) return false;
                if (br.ReadString() != "【AIS_Chara】") return false;

                // productNo
                if (new Version(br.ReadString())
                    > ChaFileDefine.ChaFileVersion) return false;

                // language
                br.ReadInt32();
                // userID
                br.ReadString();
                // dataID
                br.ReadString();

                var count = br.ReadInt32();
                var blockH = MessagePackSerializer
                    .Deserialize<BlockHeader>(br.ReadBytes(count));

                // num = skip block info
                br.ReadInt64();
                var pos = br.BaseStream.Position;

                var info = blockH.SearchInfo(ChaFileParameter.BlockName);
                if (new Version(info.version)
                    > ChaFileDefine.ChaFileParameterVersion)
                {
                    item.EvoVersion = true;
                    return true;
                }
                br.BaseStream.Seek(pos + info.pos, SeekOrigin.Begin);
                var para = br.ReadBytes((int)info.size);
                var cfp = MessagePackSerializer.Deserialize<ChaFileParameter>(para);
                item.Sex = cfp.sex;
                item.CharaName = cfp.fullname;
            }
            catch
            {
                return false;
            }
            return true;
        }

        //public static string? GuessToken(BinaryReader binaryReader)
    }

    public static class CharaTools
    {
        public static Type StudioCharaListUtilType {
            get
            {
#if HS2
                return _studioCharaListUtilType ?? (
                    _studioCharaListUtilType = Type.GetType("WearCustom.StudioCharaListUtil, " +
                        "HS2WearCustom, Version=0.4.0.0, " +
                        "Culture=neutral, PublicKeyToken=null")
                    );
#elif AI
                return _studioCharaListUtilType ?? (
                    _studioCharaListUtilType = Type.GetType("AIWearCustom.StudioCharaListUtil, " +
                    "AIWearCustom, Version=1.0.0.0, " +
                    "Culture=neutral, PublicKeyToken=null")
                    );
#endif
            }
        }

        private static Type _studioCharaListUtilType;

        private static readonly bool[] anatomy = { true, true, true, false, false };
        private static readonly bool[] outfit = { false, false, false, true, true };
        private static readonly bool[] body = { true, false, false, false, false };
        private static readonly bool[] face = { false, true, false, false, false };
        private static readonly bool[] hair = { false, false, true, false, false };
        private static readonly bool[] clothes = { false, false, false, true, false };
        private static readonly bool[] accessories = { false, false, false, false, true };

        public static void LoadAnatomy(string filePath)
        {
            CallWearCustom(filePath, anatomy);
        }

        public static void LoadOutfit(string filePath)
        {
            CallWearCustom(filePath, outfit);
        }

        public static void LoadBody(string filePath)
        {
            CallWearCustom(filePath, body);
        }

        public static void LoadFace(string filePath)
        {
            CallWearCustom(filePath, face);
        }

        public static void LoadHair(string filePath)
        {
            CallWearCustom(filePath, hair);
        }

        public static void LoadCloth(string filePath)
        {
            CallWearCustom(filePath, clothes);
        }

        public static void LoadAcc(string filePath)
        {
            CallWearCustom(filePath, accessories);
        }

        public static void CallWearCustom(string fileFullName, bool[] loadState)
        {
            if (string.IsNullOrEmpty(fileFullName)) return;

            var chara00 = GameObject.Find("StudioScene/Canvas Main Menu/02_Manipulate/00_Chara");

            var studioCharaListUtil = chara00.GetComponent<StudioCharaListUtil>();
            if (studioCharaListUtil == null)
            {
                StudioCharaListUtil.Install();
                studioCharaListUtil = chara00.GetComponent<StudioCharaListUtil>();
            }

            var replaceCharaHairOnly = StudioCharaListUtilType
                .GetField("replaceCharaHairOnly",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var replaceCharaHeadOnly = StudioCharaListUtilType
                .GetField("replaceCharaHeadOnly",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var replaceCharaBodyOnly = StudioCharaListUtilType
                .GetField("replaceCharaBodyOnly",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var replaceCharaClothesOnly = StudioCharaListUtilType
                .GetField("replaceCharaClothesOnly",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var replaceCharaAccOnly = StudioCharaListUtilType
                .GetField("replaceCharaAccOnly",
                BindingFlags.NonPublic | BindingFlags.Instance);

            var replaceFields = new[] { 
                replaceCharaBodyOnly,
                replaceCharaHeadOnly,
                replaceCharaHairOnly,
                replaceCharaClothesOnly,
                replaceCharaAccOnly
            };
            for (int i = 0; i < replaceFields.Length; i++)
            {
                replaceFields[i].SetValue(studioCharaListUtil, loadState[i]);
            }

            var charaFileSortField = StudioCharaListUtilType.GetField("charaFileSort", BindingFlags.NonPublic | BindingFlags.Instance);
            var changeMetod =
                StudioCharaListUtilType.GetMethod("ChangeChara", BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { }, null);

            var charaFileSort = charaFileSortField.GetValue(studioCharaListUtil) as CharaFileSort;
            charaFileSort.cfiList.Clear();
            var charaFileInfo = new CharaFileInfo(fileFullName, "Bobby")
            {
                node = new ListNode(),
                select = true
            };

            charaFileSort.cfiList.Add(charaFileInfo);
            charaFileSort.select = 0;
            changeMetod.Invoke(studioCharaListUtil, new object[] { });
        }

        public static OCIChar[] GetSelectedCharacters(int charSex)
        {
            return GuideObjectManager.Instance.selectObjectKey.Select(x =>
                Studio.Studio.GetCtrlInfo(x) as OCIChar).Where(x => x != null && x.oiCharInfo.sex == charSex).ToArray<OCIChar>();
        }

        // GetExtendedData ABMX Bone.
        public static void GetAbmxData(ChaFile chaFile, int chaSex, bool loadFace, bool loadBody)
        {
            if (loadFace || loadBody)
            {
                string ExtendedDataId = "KKABMPlugin.ABMData";
                var data = ExtendedSave.GetExtendedDataById(chaFile, ExtendedDataId);
                List<BoneModifier> boneMods;
                // List<BoneModifier> getMod = null;
                if (data == null) return;
                try
                {
                    switch (data.version)
                    {
                        case 2:
                            boneMods = LZ4MessagePackSerializer.Deserialize<List<BoneModifier>>((byte[])data.data["boneData"]);
                            break;
                        default:
                            throw new NotSupportedException($"Save Version {data.version} is not supported.");
                    }
                }
                catch (Exception ex)
                {
                    throw new NotSupportedException($"Load ABMX Bone: 0.\t{ex}");
                }

                if (boneMods == null) boneMods = new List<BoneModifier>();
                
                if (KKAPI.KoikatuAPI.GetCurrentGameMode() == KKAPI.GameMode.Studio)
                {
                    foreach (var oCIChar in GetSelectedCharacters(chaSex))
                        SetBone(boneMods, oCIChar.charInfo, loadFace, loadBody);
                }
                else if (MakerAPI.InsideAndLoaded)
                {
                    var chaCtrl = MakerAPI.GetCharacterControl();
                    if (!chaCtrl) return;
                    SetBone(boneMods, chaCtrl, loadFace, loadBody);
                }
            }
        }

        private static void SetBone(List<BoneModifier> bones, ChaControl chaCtrl, bool loadFace, bool loadBody)
        {
            var boneColl = chaCtrl.GetComponent<BoneController>();
            if (boneColl == null) boneColl = chaCtrl.gameObject.AddComponent<BoneController>();

            var chaBones = boneColl.Modifiers;
            for (int i = 0; i < chaBones.Count; i++)
            {
                BoneModifier mod = chaBones[i];
                mod.Reset();
            }

            if (loadFace && loadBody)
            {
                chaBones = bones;
                goto Aa;
            }

            // load face || load body
            Transform headRoot;
            if (chaCtrl.chaFile.parameter.sex == 0)
            {
                headRoot = boneColl.transform.FindLoop("cm_J_Head").transform;
            }
            else
                headRoot = boneColl.transform.FindLoop("cf_J_Head").transform;

            if (headRoot == null) return;
            var headBones = new HashSet<string>(headRoot.GetComponentsInChildren<Transform>().Select(x => x.name))
            {
                headRoot.name
            };
            if (loadFace)
            {
                chaBones.RemoveAll(x => headBones.Contains(x.BoneName));
                chaBones.AddRange(bones.Where(x => headBones.Contains(x.BoneName)));
                goto Aa;
            }

            if (loadBody)
            {
                var bodyBones = new HashSet<string>(boneColl.transform.FindLoop("BodyTop")
                    .GetComponentsInChildren<Transform>().Select(x => x.name).Except(headBones));

                chaBones.RemoveAll(x => bodyBones.Contains(x.BoneName));
                chaBones.AddRange(bones.Where(x => bodyBones.Contains(x.BoneName)));
            }

            Aa:
            boneColl.NeedsFullRefresh = true;
            boneColl.NeedsBaselineUpdate = true;
        }

        public static void LoadBodyTex(ChaFile chaFile, int chaSex)
        {
            string extBodyId = "KSOX";
            var bodyData = ExtendedSave.GetExtendedDataById(chaFile, extBodyId);

            if (bodyData != null)
            {
                try
                {
                    var selectChar = GetSelectedCharacters(chaSex);

                    foreach (var _oCIChar in selectChar)
                    {
                        var chaBodyComp = _oCIChar.charInfo.gameObject.GetComponent<KoiSkinOverlayController>();

                        if (chaBodyComp == null)
                        {
                            chaBodyComp = _oCIChar.charInfo.gameObject.AddComponent<KoiSkinOverlayController>();
                        }

                        if (bodyData.version <= 1)
                        {
                            chaBodyComp.CallPrivateMethod("ReadLegacyData", bodyData);
                        }
                        else
                        {
                            chaBodyComp.EnableInStudioSkin = true;
                            chaBodyComp.EnableInStudioIris = true;
                            chaBodyComp.OverlayStorage.Load(bodyData);
                            chaBodyComp.UpdateTexture(TexType.Unknown);
                        }

                    }
                }
                catch (Exception ex)
                {
                    throw new NotSupportedException($"Try Get KSOX Catch are: \t{ex}");
                }
            }
        }

        public static void LoadClothTex(ChaFile chaFile, int chaSex)
        {
            string extClothId = "KCOX";
            var clothData = ExtendedSave.GetExtendedDataById(chaFile, extClothId);

            Dictionary<KoiClothesOverlayX.CoordinateType, Dictionary<string, ClothesTexData>> clothTex = null;

            if (clothData != null && (clothData.version <= 1))
            {
                try
                {
                    clothTex = LZ4MessagePackSerializer.Deserialize<Dictionary<KoiClothesOverlayX.CoordinateType,
                        Dictionary<string, ClothesTexData>>>((byte[])clothData.data["Overlays"]);
                }
                catch (Exception ex)
                {
                    throw new NotSupportedException($"Try Get KCOX Catch are: \t{ex}");
                }
            }

            foreach (var _oCIChar in GetSelectedCharacters(chaSex))
            {
                var chaClothComp = _oCIChar.charInfo.gameObject.GetComponent<KoiClothesOverlayController>();
                if (chaClothComp == null)
                {
                    chaClothComp = _oCIChar.charInfo.gameObject.AddComponent<KoiClothesOverlayController>();
                }

                chaClothComp.CallPrivateMethod("RemoveAllOverlays");
                chaClothComp.EnableInStudio = true;
                if (clothTex == null)
                {
                    clothTex = new Dictionary<KoiClothesOverlayX.CoordinateType, Dictionary<string, ClothesTexData>>();
                }
                chaClothComp.SetPrivateField("_allOverlayTextures", clothTex);
                chaClothComp.StartCoroutine(chaClothComp.CallPrivateMethod<IEnumerator>("RefreshAllTexturesCo"));
            }
        }
    }


    internal class MakerTools
    {
        private Toggle[] _tgls;
        private CustomCharaWindow CusChaWin;
        public bool FullInit => _tgls != null && _tgls.Length >= 9;

        public static MakerTools Instance => GetMakerTools();

        private static MakerTools _instance;

        public static ChaControl GetChaCtrl
            => MakerAPI.GetCharacterControl();

        private static MakerTools GetMakerTools()
        {
            if (_instance != null && _instance._tgls != null)
            {
                return _instance;
            }    
            if (KKAPI.KoikatuAPI.GetCurrentGameMode() != KKAPI.GameMode.Maker)
                return null;
            var LoadSetGrid = GameObject.Find("CharaCustom/CustomControl/CanvasSub/SettingWindow/WinOption/SystemWin/O_Load/LoadSetting");
            if (LoadSetGrid == null) return null;

            var toggles = LoadSetGrid.transform.GetComponentsInChildren(typeof(Toggle), true).Cast<Toggle>().ToArray();
            if (toggles == null || toggles.Length < 9) return null;

            _instance = new MakerTools()
            {
                _tgls = toggles
            };
            
            return _instance;
        }

        public const LOpt LoadBaseAllCase = LOpt.Face | LOpt.Body | LOpt.Hair | LOpt.Cloth | LOpt.ChaParam;
        public const LOpt LoadPlaginsCase = LOpt.ClothOver | LOpt.SkinOver | LOpt.FaceBone | LOpt.BodyBone;
        public const LOpt LoadAllCase = LoadBaseAllCase | LoadPlaginsCase;

        public void Clear()
        {
            _tgls = null;
            _instance = null;
            CusChaWin = null;
        }

        /// <summary>
        /// 00: Face, 01: Body, 02: Hair, 03: Cloth, 04: ChaParam
        /// </summary>
        /// <param name="objName"></param>
        /// <param name="tglOn"></param>
        private void SetToggleValue(string objName, bool tglOn)
        {
            // Skip IL tgls
            SetToggleValue(objName, tglOn, 5);
        }

        private void SetToggleValue(string objName, bool tglOn, int startPos)
        {
            for (var i = startPos; i < _tgls.Length; i++)
            {
                if (_tgls[i].gameObject.name.ContainsCase(objName))
                {
                    // _tgls[i].m_IsOn
                    _tgls[i].isOn = tglOn;
                    break;
                }
            }
        }

        private void SetPluginToggle(int num)
        {
            SetToggleValue("API_Clothes", (num & 32) != 0);
            SetToggleValue("API_Skin", (num & 64) != 0);
            SetToggleValue("API_Face", (num & 128) != 0);
            SetToggleValue("API_Body", (num & 256) != 0);
        }

        private void SetBaseLoadToggle(int num)
        {
            _tgls[0].isOn = (num & 1) != 0;
            _tgls[1].isOn = (num & 2) != 0;
            _tgls[2].isOn = (num & 4) != 0;
            _tgls[3].isOn = (num & 8) != 0;
            _tgls[4].isOn = (num & 16) != 0;
        }

        private int GetBaseOption()
        {
            int num = 0;
            if (!FullInit) return num;
            if (_tgls[0].isOn)
                num |= (int)LOpt.Face;
            if (_tgls[1].isOn)
                num |= (int)LOpt.Body;
            if (_tgls[2].isOn)
                num |= (int)LOpt.Hair;
            if (_tgls[3].isOn)
                num |= (int)LOpt.Cloth;
            if (_tgls[4].isOn)
                num |= (int)LOpt.ChaParam;
            return num;
        }

        public void LoadBaseOpition(string fullPath)
        {
            ClickLoadButton(fullPath, GetBaseOption());
        }

        // IL: 1-5
        // Plugin: 6 - 9 {clothOver, SkinOver, faceBone, bodyBone}
        // //GetChaCtrl.chaFile.LoadFileLimited(fullPath, sex, lf, lb, lh, lp, lc)
        public void LoadCustom(string fullPath, int num)
        {
            if (!FullInit) return;
            SetBaseLoadToggle(num);
            SetPluginToggle(num);
            ClickLoadButton(fullPath, num);
        }

        public void LoadAllOption(string fullPath)
        {
            if (!FullInit) return;
            if (_tgls == null) return;

            for (var i = 0; i < _tgls.Length; i++)
            {
                if (!_tgls[i].isOn)
                    _tgls[i].isOn = true;
            }
            ClickLoadButton(fullPath, (int)LoadBaseAllCase);
        }
        
        public void ClickLoadButton(string fullPath, int num)
        {
            if (CusChaWin == null)
            {
                // O_SaveDelete || O_Load
                var customChara = GameObject.Find("CharaCustom/CustomControl/CanvasSub/SettingWindow/WinOption/SystemWin/O_Load");
                CusChaWin = customChara?.GetComponent<CustomCharaWindow>();
            }

            CusChaWin?.onClick03?.Invoke(new CustomCharaFileInfo
            {
                FullPath = fullPath,
                sex = byte.MaxValue
            }, num);
        }

        public void LoadReplaceParts(string fullPath, int num)
        {
            LoadReplaceParts(fullPath, num, true);
        }

        public void LoadReplaceParts(string fullPath, int num, bool setBaseTgls)
        {
            if (!FullInit) return;
            var chaCtrl = GetChaCtrl;
            var lf = (num & 1) != 0;
            var lb = (num & 2) != 0;
            var lh = (num & 4) != 0;
            var lc = (num & 8) != 0;
            var lp = (num & 16) != 0;
            var acc = (num & 512) != 0;
            
            if (setBaseTgls)
            {
                _tgls[0].isOn = lf;
                _tgls[1].isOn = lb;
                _tgls[2].isOn = lh;
                _tgls[3].isOn = lc;
                _tgls[4].isOn = lp;
            }

            SetPluginToggle(num);

            byte[] accData = null;
            byte[] clothData = null;

            if (!acc)
                accData = MessagePackSerializer
                        .Serialize(GetChaCtrl.chaFile.coordinate.accessory);
            if (!lc)
                clothData = MessagePackSerializer
                        .Serialize(GetChaCtrl.chaFile.coordinate.clothes);

            chaCtrl.chaFile.LoadFileLimited(
                fullPath, byte.MaxValue, lf, lb, lh, lf && lb, lc || acc);

            if (!acc)
                chaCtrl.chaFile.coordinate.accessory = MessagePackSerializer
                        .Deserialize<ChaFileAccessory>(accData);
            if (!lc)
                chaCtrl.chaFile.coordinate.clothes = MessagePackSerializer
                        .Deserialize<ChaFileClothes>(clothData);

            chaCtrl.ChangeNowCoordinate(false, true);
            chaCtrl.Reload(!(lc || acc), !lf, !lh, !lb, true);

            var matCtrller = MaterialEditorPlugin.GetCharaController(GetChaCtrl);
            if (matCtrller != null && lc)
            {
                matCtrller.CustomClothesOverride = true;
                matCtrller.RefreshClothesMainTex();
            }
        }
    }

#if HS2
    public static class HSceneTools
    {
        public const string H_SCENE_NAME = "HScene";

        public static bool ProcEndInit => ProcBase.endInit;

        public static HScene HSceneIns => HSceneManager.instance.Hscene;

        public static bool HSceneLoaded => HSceneIns || KKAPI.SceneApi.GetLoadSceneName() == H_SCENE_NAME;

        public static HSceneFlagCtrl HFlagCtrl => HSceneIns.ctrlFlag;

        public static ChaControl[] SchaMales => HS2_HCharaSwitcher.HS2_HCharaSwitcher.chaMales;

        public static ChaControl[] SchaFemales => HS2_HCharaSwitcher.HS2_HCharaSwitcher.chaFemales;

        public static HS2_HCharaSwitcher.HS2_HCharaSwitcher HSwitch => HS2_HCharaSwitcher.HS2_HCharaSwitcher.instance;

        public static bool IsChangeAnim
            => HSceneIns.GetPrivateField<bool>("nowChangeAnim");

        public static Func<ChaControl, string, int, IEnumerator> ChangeFe;

        public static Func<ChaControl, string, int, IEnumerator> ChangeMa;

        public static IEnumerator ChangeCharacter(string filePath, byte sex)
        {
            if (!HSceneIns && !HSwitch
                && CharaTools.StudioCharaListUtilType == null) yield break;

            var flag = !ProcEndInit || IsChangeAnim || HFlagCtrl.nowOrgasm;
            if (flag) yield break;

            var idx = HSceneManager.instance.numFemaleClothCustom;
            if (idx < 2 && sex == 1)
            {
                var chaF = SchaFemales[idx];
                if (chaF == null) yield break;
                yield return GetChangeFe(chaF, filePath, idx);
                yield break;
            }
            if (idx > 1 && sex == 0)
            {
                var chaM = SchaMales[idx - 2];
                if (chaM == null) yield break;
                yield return GetChangeMa(chaM, filePath, idx - 2);
            }
        }

        public static IEnumerator GetChangeFe(
            ChaControl chara, string card, int id)
        {
            if (ChangeFe == null)
            {
                var meth = AccessTools.Method(
                    typeof(HS2_HCharaSwitcher.HS2_HCharaSwitcher),
                    "ChangeCharacterF", null, null)
                    ?? throw new ArgumentException("TryGetMethod not found");

                ChangeFe = (ma, path, idx) => (IEnumerator)meth
                    .Invoke(null, new object[] { ma, path, idx });
            }
            return ChangeFe(chara, card, id);
        }

        public static IEnumerator GetChangeMa(
            ChaControl chara, string card, int id)
        {
            if (ChangeMa == null)
            {
                var meth = AccessTools.Method(
                    typeof(HS2_HCharaSwitcher.HS2_HCharaSwitcher),
                    "ChangeCharacterM", null, null)
                    ?? throw new ArgumentException("TryGetMethod not found");

                ChangeMa = (ma, path, idx) => (IEnumerator)meth
                    .Invoke(null, new object[] { ma, path, idx });
            }
            return ChangeMa(chara, card, id);
        }
    }
#endif

#endif
}
