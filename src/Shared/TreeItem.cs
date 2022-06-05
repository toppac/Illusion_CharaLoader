using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Xml.Linq;
using System.Threading;
using System.Linq;
using System.Text.RegularExpressions;
using Common;

namespace CharaLoader
{
    public class TreeItem
    {
        public bool Expanded = false;
        public int Depth { get; set; }

        public TreeItem Parent { get; set; }
        public GameObject Obj { get; set; }
        // Arrow
        public Transform Icon { get; set; }
        public List<TreeItem> Children => _children;

        public string Name => Path.GetFileName(FullName);

        public string FullName
        {
            get;
            private set;
        }

        public TreeItem(string Path, int depth = 0)
        {
            Depth = depth;
            FullName = Path;
            // _children = new Lazy<List<TreeItem>>();
            // _children = new List<TreeItem>();
        }

        public void AddChild(TreeItem item)
        {
            if (_children == null)
                _children = new List<TreeItem>();
            _children.Add(item);
        }

        public void ClearChildren()
        {
            _children?.Clear();
        }

        public override string ToString() => FullName;

        private List<TreeItem> _children;
    }

/*    public abstract class BookMarkItem<TData>
    {
        public string Idtt => Endorse.GUID;
        public string FileVersion { get; private set; }
        public string FileName { get; private set; }

        public abstract XDocument XmlFile();
    }*/

    internal class BookMarkTool
    {
        private string _filePath;

        public const string FileVersion = "1.0";

        public string FileName { get; private set; }

        public string Idtt => Endorse.GUID;

        public BookMarkTool(string fileDir, string fileName)
        {
            FileName = fileName;
            CheckFile(fileDir);
        }

        private void CheckFile(string fileDir)
        {
            var dirPath = Path.GetFullPath(fileDir);
            var xmlPath = Path.Combine(dirPath, FileName);
            _filePath = xmlPath;
            try
            {
                if (!File.Exists(xmlPath))
                {
                    CreateXml();
                    return;
                }

                var doc = XDocument.Load(xmlPath);
                var idtt = doc.Root.Attribute("Plugin").Value;

                if (idtt.CompareCase(Idtt))
                {
                    _xDoc = doc;
                    return;
                }
                CreateXml();
            }
            catch //(Exception ex)
            {
                return;
            }
        }

        private void CreateXml()
        {
            var xml = new XDocument(
                new XDeclaration(FileVersion, "utf-8", string.Empty),
                new XElement("BookMark",
                new XAttribute("Plugin", Idtt),
                new XAttribute("Type", "Character")
                ));
            _xDoc = xml;
            Save();
        }

        private void Save()
        {
            if (_isBacklog) return;
            var timer = new Timer(AutoSave);
            timer.Change(30000, Timeout.Infinite);
            _isBacklog = true;
        }

        private void AutoSave(object obj)
        {
            try
            {
                _xDoc.Save(_filePath, SaveOptions.OmitDuplicateNamespaces);
            }
            finally
            {
                _isBacklog = false;
            }
        }

        public List<string> GetAllBookPath()
        {
            var paths = new List<string>();

            foreach (var e in _xDoc.Descendants("Path"))
            {
                var path = e.Value;
                if (string.IsNullOrEmpty(path)) continue;
                paths.Add(path);
            }
            return paths;
        }

        public IEnumerable<string> GetBookPath()
        {
            foreach (var e in _xDoc.Descendants("Path"))
            {
                var rp = e.Value;
                if (string.IsNullOrEmpty(rp)) continue;
                yield return rp;
            }
        }

        public bool FindAny()
        {
            return _xDoc.FirstNode != null;
        }

        public bool Exists(string relativePath)
        {
            foreach (var element in _xDoc.Descendants("Path"))
            {
                if (element.Value.CompareCase(relativePath))
                    return true;
            }
            return false;
        }

        public bool FindAnyAndClear(string relativePath)
        {
            var ar = _xDoc.Descendants("Path")
                .Where(x => x.Value.CompareCase(relativePath));
            if (ar.Any())
            {
                if (ar.Count() > 1)
                {
                    IsDirty = true;
                    ar.Skip(1).Select(x => x.Parent).Remove();
                }
                return true;
            }
            return false;
        }

        public bool Find(string relativePath)
        {
            foreach (var p in _xDoc.Descendants("Path"))
            {
                if (p.Value.CompareCase(relativePath))
                {
                    _sePoseElement = p.Parent;
                    return true;
                }
            }
            _sePoseElement = null;
            return false;
        }

        public IEnumerable<XElement> FindAllItem()
        {
            return _xDoc.Descendants("Cha");
        }

        public IEnumerable<XElement> FindAllPath()
        {
            return _xDoc.Descendants("Path");
        }

        public IEnumerable<XElement> FindNode(string keyWord)
        {
            var ar = _xDoc.Descendants("Cha").Where(x => x.Attribute("name").Value == keyWord
            || x.Attribute("Tag").Value.Split(',').All(t => t.ContainsCase(keyWord)));
            var ls = ar.Select(x => x.Element("Path"));
            return ls;
        }

        public void SetElementTag(string tag)
        {
            var str = FormatTagText(tag);
            if (_sePoseElement != null)
            {
                _sePoseElement.Attribute("Tag").Value = str;
                Save();
            }
        }

        public void SetElementTag(string tag, out string str)
        {
            str = FormatTagText(tag);
            if (_sePoseElement != null)
            {
                _sePoseElement.Attribute("Tag").Value = str;
                Save();
            }
        }

        public string GetElementTag()
        {
            if (_sePoseElement == null) return string.Empty;
            return _sePoseElement.Attribute("Tag").Value;
        }

        public void Add(string name, string relativePath, string tag = null)
        {
            if (Exists(relativePath)) return;

            if (tag == null) { tag = string.Empty; }
            else { tag = FormatTagText(tag); }

            var et = new XElement("Cha", new XAttribute("name", name),
                new XAttribute("Tag", tag),
                new XElement("Path", relativePath)
                );
            _xDoc.Root.Add(et);
            _sePoseElement = et;
            Save();
        }

        public void Remove(string relativePath)
        {
            foreach (var el in _xDoc.Descendants("Path"))
            {
                if (el.Value.CompareCase(relativePath))
                {
                    el.Parent.Remove();
                    Save();
                    return;
                }
            }
        }

        public void Cleaner()
        {
            // step: 1
            var pos = _xDoc.Descendants("Cha");
            pos.Where(p => p.Element("Path") == null).Remove();

            // step: 2
            var comparer = new OrdinalIgnoreCaseComparer();
            var pts = _xDoc.Descendants("Path").ToList();
            pts.GroupBy(x => x.Value, comparer).SelectMany(g => g.Skip(1))
                .Select(p => p.Parent).Remove();

            // step: 3
            var paths = _xDoc.Descendants("Path");
            paths.Where(p => !CheckElementPath(p.Value))
                .Select(e => e.Parent).Remove();

            //Cleaner2();
            Save();
        }

        public bool CheckElementPath(string fullPath)
        {
            if (!File.Exists(fullPath)) return false;
            return true;
        }

        public string GetElementPath(string relativePath)
        {
            if (relativePath == null) return null;
            var path = Path.GetFullPath(relativePath);
            if (!File.Exists(path)) return null;
            return path;
        }

        public string FormatTagText(string tag)
        {
            var reg = new Regex(@"(\w+\s?)+");
            var match = reg.Matches(tag).Cast<Match>().Select(m => m.Value);
            return string.Join(", ", match);
        }

        private XDocument _xDoc;
        private XElement _sePoseElement;

        public bool IsDirty = false;
        private bool _isBacklog = false;
    }
}
