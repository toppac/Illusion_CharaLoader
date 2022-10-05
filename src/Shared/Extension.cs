using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace CharaLoader
{
    internal static class Extension
    {
        public static bool ContainsCase(this string source, string dest)
        {
            return source.IndexOf(dest, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool ContainsCase(this string source, string dest, StringComparison comparison)
        {
            return source.IndexOf(dest, comparison) >= 0;
        }

        public static bool CompareCase(this string source, string dest)
        {
            return CompareCase(source, dest, StringComparison.OrdinalIgnoreCase);
        }

        public static bool CompareCase(this string source, string dest, StringComparison comparison)
        {
            return string.Compare(source, dest, comparison) == 0;
        }

        public static void DestroyChild(this Component component)
        {
            if (component == null) return;
            Transform trans = component.transform;
            foreach (Transform item in trans)
            {
                UnityEngine.Object.Destroy(item.gameObject);
            }
        }
    }
}
