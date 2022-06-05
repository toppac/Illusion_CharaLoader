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

#if !PLUGIN

        public static IEnumerator AppendCo(this IEnumerator baseCoroutine, params Action[] actions)
        {
            return ComposeCoroutine(new IEnumerator[]
            {
                baseCoroutine,
                CreateCoroutine(actions)
            });
        }

        public static IEnumerator ComposeCoroutine(params IEnumerator[] coroutine)
        {
            return coroutine.GetEnumerator();
        }

        public static IEnumerator CreateCoroutine(params Action[] actions)
        {
            if (actions == null) throw new ArgumentNullException(nameof(actions));

            var first = true;
            foreach (var action in actions)
            {
                if (first)
                    first = false;
                else
                    yield return null;

                action();
            }
        }

        public static IEnumerator AttachToYield(this IEnumerator coroutine, Action onYieldAction)
        {
            if (coroutine == null)
            {
                throw new ArgumentNullException("coroutine");
            }
            if (onYieldAction == null)
            {
                throw new ArgumentNullException("onYieldAction");
            }
            while (coroutine.MoveNext())
            {
                onYieldAction();
                yield return coroutine.Current;
            }
            yield break;
        }

        public static IEnumerator CreateCoroutine(YieldInstruction yieldInstruction, params Action[] actions)
        {
            if (yieldInstruction == null)
            {
                throw new ArgumentNullException("yieldInstruction");
            }
            if (actions == null)
            {
                throw new ArgumentNullException("actions");
            }
            yield return yieldInstruction;
            yield return CreateCoroutine(actions);
            yield break;
        }
#endif
    }

}
