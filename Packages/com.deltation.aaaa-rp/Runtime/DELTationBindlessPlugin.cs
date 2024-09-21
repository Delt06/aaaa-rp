using System;
using System.Runtime.InteropServices;

namespace DELTation.AAAARP
{
    public static class DELTationBindlessPlugin
    {
        private const string DLLName =
#if (PLATFORM_IOS || PLATFORM_TVOS || PLATFORM_BRATWURST || PLATFORM_SWITCH) && !UNITY_EDITOR
            "__Internal";
#else
            "DELTationBindlessPlugin";
#endif

        [DllImport(DLLName)]
        public static extern uint GetSRVDescriptorHeapCount();

        [DllImport(DLLName)]
        public static extern int CreateSRVDescriptor(IntPtr pTexture, uint index);
    }
}