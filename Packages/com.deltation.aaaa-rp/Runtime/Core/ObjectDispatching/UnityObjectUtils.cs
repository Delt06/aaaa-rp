using UnityEngine;

namespace DELTation.AAAARP.Core.ObjectDispatching
{
    internal static class UnityObjectUtils
    {
        public static void MarkDirty(Object obj)
        {
            obj.MarkDirty();
        }
    }
}