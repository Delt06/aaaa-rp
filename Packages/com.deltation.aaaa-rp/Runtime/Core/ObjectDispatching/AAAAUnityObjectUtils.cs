using UnityEngine;
using UnityEngine.Assertions;

namespace DELTation.AAAARP.Core
{
    public static class AAAAUnityObjectUtils
    {
        public static void MarkDirty(Object obj)
        {
            Assert.IsNotNull(obj);
            obj.MarkDirty();
        }
    }
}