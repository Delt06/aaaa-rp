using System;
using System.Runtime.InteropServices;

namespace DELTation.AAAARP.BindlessPlugin.Runtime
{
    public static class BindlessPluginBindings
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

        [DllImport(DLLName)]
        public static extern uint IsPixLoaded();

        [DllImport(DLLName)]
        public static extern uint BeginPixCapture([MarshalAs(UnmanagedType.LPWStr)] string filename);
        
        [DllImport(DLLName)]
        public static extern uint EndPixCapture();
        
        [DllImport(DLLName)]
        public static extern void OpenPixCapture([MarshalAs(UnmanagedType.LPWStr)] string filename);
    }
}