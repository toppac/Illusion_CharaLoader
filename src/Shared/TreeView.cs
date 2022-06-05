using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace CharaLoader
{
    public enum LoadType
    {
        Normal,
        Root,
        All,
        Book,
        RecycleBin
    }

    internal class TreeView
    {
        public LoadType LoadOption { get; set; } = LoadType.Root;
        public GameObject NodeObj { get; set; }
        public Transform Content { get; set; }
        public string CurrentPath => _currentPath;

        protected Image ActiveNodeImg { get; set; }
        private readonly List<TreeItem> _treeItems = new List<TreeItem>();
        private string _currentPath;

#if PLUGIN
        public static ReCharaLoaderStudio Loader => LoaderTools.LoaderStudio;
#else
        public static ReCharaLoaderStudio Loader => ReCharaLoaderStudio.Instance;
#endif

        public const int Margin = 16;
        public const string RootText = " ...... ";
        public const string AllItemText = " - All Chara Files - ";
        public const string BookMarkText = " - Favorites - ";
        public static Color ActiveColor = new Color(0.8f, 1, 0.86f);

        public void DrawGui(string path)
        {
            if (path == null || Content == null) return;
            _currentPath = path;
            GetDir(new DirectoryInfo(path));
        }

        public void DrawGui(DirectoryInfo dirInfo)
        {
            if (dirInfo == null || Content == null) return;
            _currentPath = dirInfo.FullName;
            GetDir(dirInfo);
        }

        public void GetDir(DirectoryInfo dirInfo)
        {
            Clear();
            if (!dirInfo.Exists) return;
            GetTreeItem(dirInfo);
            if (_treeItems.Count > 0)
            {
                LoadTreeItems();
            }
        }

        private void GetTreeItem(DirectoryInfo info)
        {
            DirectoryInfo[] array = info.GetDirectories();
            for (int i = 0; i < array.Length; i++)
            {
                DirectoryInfo folder = array[i];
                var item = new TreeItem(folder.FullName, 0);
                _treeItems.Add(item);
                GetTreeItem(item, folder, 1);
            }
        }

        private void GetTreeItem(TreeItem parent, DirectoryInfo info, int depth)
        {
            var dirs = info.GetDirectories();
            if (dirs.Length < 1) return;
            for (int i = 0; i < dirs.Length; i++)
            {
                DirectoryInfo folder = dirs[i];
                var item = new TreeItem(folder.FullName, depth);
                item.Parent = parent;
                parent.AddChild(item);
                GetTreeItem(item, folder, depth + 1);
            }
        }

        private void LoadTreeItems()
        {
            var content = Content;
            foreach (var item in _treeItems)
            {
                LoadTreeItems(item, content);
            }
            LoadBookMarkButton(content);
            //LoadAllPathButton(content);
            RootDirButton(content);
        }

        private void LoadTreeItems(TreeItem item, Transform content)
        {
            SetTreeButton(item, content);
            if (item.Children == null) return;
            foreach (var child in item.Children)
            {
                LoadTreeItems(child, content);
            }
        }

        private void SetTreeButton(TreeItem item, Transform content)
        {
            var node = UnityEngine.Object.Instantiate(NodeObj);
            item.Obj = node;

            var trans = node.transform;
            //if (item.Children.Count > 0)
            if (item.Children != null)
            {
                var expBtn = trans.Find("Panel/ExpBtn").GetComponent<Button>();
                expBtn.onClick.AddListener(() => { SetNodeExpand(item); });
                expBtn.gameObject.SetActive(true);
                item.Icon = expBtn.transform;
            }

            var DirBtnEvn = trans.Find("Panel/DirBtn")
                .gameObject.AddComponent<ClickListener>();
            DirBtnEvn.LClick += (e) => {
                LoadNormalItem(item);
            };
            DirBtnEvn.RClick += (e) => { OpenFolder(item); };

            if (item.FullName.CompareCase(_currentPath))
            {
                ActiveButton(trans.Find("Panel/DirBtn").GetComponent<Image>());
            }

            var text = DirBtnEvn.transform.Find("Text").GetComponent<Text>();
            text.text = item.Name;

            var rectTrans = trans.GetComponent<RectTransform>();
            var offset = new Vector2(item.Depth * Margin, 0);
            rectTrans.sizeDelta += offset;

            trans.SetParent(content, false);
            if (item.Depth == 0) node.SetActive(true);
        }

        private void RootDirButton(Transform content)
        {
            var root = UnityEngine.Object.Instantiate(NodeObj).gameObject;
            var trans = root.transform;
            var img = trans.Find("Panel/DirBtn").GetComponent<Image>();

            var BtnEven = img.gameObject.AddComponent<ClickListener>();
            BtnEven.LClick += (e) => { LoadRootItems(img); };
            BtnEven.RClick += (e) => {
                OpenFolder(Loader.CurrentDataInfo.FullName);
            };

            if (LoadOption == LoadType.Root)
            {
                ActiveButton(img);
            }

            var text = img.transform.Find("Text").GetComponent<Text>();
            text.text = RootText;

            trans.GetComponent<RectTransform>().sizeDelta -= new Vector2(0, 10);
            trans.SetParent(content, false);
            trans.SetAsFirstSibling();
            root.SetActive(true);
        }

        private void LoadAllPathButton(Transform content)
        {
            var loadAll = UnityEngine.Object.Instantiate(NodeObj).gameObject;
            var trans = loadAll.transform;
            var btn = trans.Find("Panel/DirBtn").GetComponent<Button>();
            var img = btn.transform.GetComponent<Image>();

            var text = btn.transform.Find("Text").GetComponent<Text>();
            text.text = AllItemText;

            btn.onClick.AddListener(() => { LoadAllItems(img); });

            if (LoadOption == LoadType.All)
            {
                ActiveButton(img);
            }

            trans.SetParent(content, false);
            trans.SetAsFirstSibling();
            loadAll.SetActive(true);
        }

        private void LoadBookMarkButton(Transform content)
        {
            var book = UnityEngine.Object.Instantiate(NodeObj).gameObject;
            var trans = book.transform;
            var btn = trans.Find("Panel/DirBtn").GetComponent<Button>();
            var img = btn.transform.GetComponent<Image>();

            var text = btn.transform.Find("Text").GetComponent<Text>();
            text.text = BookMarkText;

            btn.onClick.AddListener(() => { LoadBookItems(img); });

            if (LoadOption == LoadType.Book)
            {
                ActiveButton(img);
            }

            trans.SetParent(content, false);
            trans.SetAsFirstSibling();
            book.SetActive(true);
        }

        private void LoadNormalItem(TreeItem item)
        {
            ActiveButton(item);
            SetLoaderPath(item.FullName);
        }

        private void SetLoaderPath(string path)
        {
#if DEBUG
            //Debug.Log(path);
#endif
            LoadOption = LoadType.Normal;
            if (path.CompareCase(_currentPath)) return;

            _currentPath = path;
            LoadNormalPath(path);
        }

        private static void LoadNormalPath(string path)
        {
            Loader.LoadView(path);
        }

        private void LoadRootItems(Image img)
        {
            ActiveButton(img);
            _currentPath = RootText;
            if (LoadOption == LoadType.Root) return;
            LoadOption = LoadType.Root;
            LoadRootItems();
        }

        private static void LoadRootItems()
        {
            Loader.LoadView();
        }

        private void LoadAllItems(Image img)
        {
            ActiveButton(img);
            _currentPath = AllItemText;
            if (LoadOption == LoadType.All) return;
            LoadOption = LoadType.All;
            LoadAllItems();
        }

        // Data Path.
        private void LoadAllItems()
        {
            //Loader.LoadView(LoadType.All);
        }

        private void LoadBookItems(Image img)
        {
            ActiveButton(img);
            _currentPath = BookMarkText;
            if (LoadOption == LoadType.Book) return;
            LoadOption = LoadType.Book;
            LoadBookItems();
        }

        private void LoadBookItems()
        {
            Loader.LoadView(LoadType.Book);
        }

        private void SetNodeExpand(TreeItem item)
        {
            if (item.Expanded)
            {
                CloseNode(item);
                return;
            }
            ExpandNode(item);
        }

        private void ExpandNode(TreeItem item)
        {
            item.Expanded = true;
            IconEffect(item, true);
            ExpandChildrenNode(item);
        }

        private void ExpandChildrenNode(TreeItem item)
        {
            if (item.Children == null) return;
            foreach (var child in item.Children)
            {
                child.Obj.SetActive(true);
                if (child.Expanded)
                    ExpandChildrenNode(child);
            }
        }

        private void CloseNode(TreeItem item)
        {
            item.Expanded = false;
            IconEffect(item, false);
            CloseChildrenNode(item);
        }

        private void CloseChildrenNode(TreeItem item)
        {
            if (item.Children == null) return;
            foreach (var child in item.Children)
            {
                child.Obj.SetActive(false);
                CloseChildrenNode(child);
            }
        }

        // Arrow
        private void IconEffect(TreeItem item, bool isExpand)
        {
            if (item.Icon == null) return;
            if (!isExpand)
            {
                item.Icon.localEulerAngles = Vector3.zero;
                return;
            }
            item.Icon.localEulerAngles = new Vector3(0, 0, -90);
        }

        private void OpenFolder(TreeItem item)
        {
            OpenFolder(item.FullName);
        }

        private void OpenFolder(string path)
        {
            var fullPath = Path.GetFullPath(path);
            //Process.Start("explorer.exe", $"/e,/select, {fullPath}");
            try
            {
                Process.Start("explorer.exe", fullPath);
            }
            catch
            {
                return;
            }
        }

        private void ActiveButton(TreeItem item)
        {
            ActiveButton(
                item.Obj.transform.Find("Panel/DirBtn").GetComponent<Image>());
        }

        private void ActiveButton(Image img)
        {
            if (ActiveNodeImg != null)
                ActiveNodeImg.color = Color.white;
            img.color = ActiveColor;
            ActiveNodeImg = img;
        }

        public void ClearInfo()
        {
            LoadOption = LoadType.Normal;
            _currentPath = string.Empty;
            if (ActiveNodeImg != null)
                ActiveNodeImg.color = Color.white;
            ActiveNodeImg = null;
        }

        // Reload
        public void Clear()
        {
            // auto destory
            Content?.DestroyChild();
            _treeItems?.Clear();
        }

        public void FullClear()
        {
            ClearInfo();
            Clear();
            if (NodeObj)
                GameObject.Destroy(NodeObj);
            NodeObj = null;
        }
    }
}
