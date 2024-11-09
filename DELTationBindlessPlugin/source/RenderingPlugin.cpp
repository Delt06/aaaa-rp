// Example low level rendering Unity plugin

#include "winapifamily.h"

#include "PlatformBase.h"
#include "Unity/IUnityGraphics.h"
#include "Unity/IUnityLog.h"
#include "HookWrapper.h"

#include <MinHook.h>
#ifdef USE_PIX
#include <pix3.h>
#include <shlobj.h>
#endif

#include <assert.h>
#if WINAPI_FAMILY != WINAPI_FAMILY_DESKTOP_APP
#undef WINVER
#define WINVER 0x0499
#endif
#include <dxgi.h>
#include <d3d12.h>
#include <d3dx12.h>
#include <math.h>
#include <vector>
#include <map>
#include <set>
#include <strsafe.h>

#include "Unity/IUnityGraphicsD3D12.h"
#include "Unity/IUnityProfiler.h"

static bool s_IsDevelopmentBuild = false;

bool ShouldLoadWinPixDLL(int argc, LPWSTR* argv)
{
    for (int i = 0; i < argc; i++)
    {
        if (lstrcmpW(argv[i], L"-pix-capture") == 0)
        {
            return true;
        }
    }

    return false;
}

#ifdef USE_PIX
static std::wstring GetLatestWinPixGpuCapturerPath()
{
    LPWSTR programFilesPath = nullptr;
    SHGetKnownFolderPath(FOLDERID_ProgramFiles, KF_FLAG_DEFAULT, NULL, &programFilesPath);

    std::wstring pixSearchPath = programFilesPath + std::wstring(L"\\Microsoft PIX\\*");

    WIN32_FIND_DATAW findData;
    bool foundPixInstallation = false;
    wchar_t newestVersionFound[MAX_PATH];

    HANDLE hFind = FindFirstFileW(pixSearchPath.c_str(), &findData);
    if (hFind != INVALID_HANDLE_VALUE)
    {
        do 
        {
            if (((findData.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) == FILE_ATTRIBUTE_DIRECTORY) &&
                 (findData.cFileName[0] != '.'))
            {
                if (!foundPixInstallation || wcscmp(newestVersionFound, findData.cFileName) <= 0)
                {
                    foundPixInstallation = true;
                    StringCchCopyW(newestVersionFound, _countof(newestVersionFound), findData.cFileName);
                }
            }
        } 
        while (FindNextFileW(hFind, &findData) != 0);
    }

    FindClose(hFind);

    if (!foundPixInstallation)
    {
        // TODO: Error, no PIX installation found
    }

    wchar_t output[MAX_PATH];
    StringCchCopyW(output, pixSearchPath.length(), pixSearchPath.data());
    StringCchCatW(output, MAX_PATH, &newestVersionFound[0]);
    StringCchCatW(output, MAX_PATH, L"\\WinPixGpuCapturer.dll");

    return &output[0];
}
#endif

extern "C" uint32_t UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API IsPixLoaded()
{
    return GetModuleHandleW(L"WinPixGpuCapturer.dll") != 0;
}


extern "C" uint32_t UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API BeginPixCapture(IN LPWSTR filePath)
{
    #ifdef USE_PIX
    PIXCaptureParameters pixCaptureParameters = {};
    pixCaptureParameters.GpuCaptureParameters.FileName = filePath;
    return PIXBeginCapture(PIX_CAPTURE_GPU, &pixCaptureParameters);
    #else
    return S_OK;
    #endif
    
}

extern "C" uint32_t UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API EndPixCapture()
{
    #ifdef USE_PIX
    HRESULT result;
    while ((result = PIXEndCapture(FALSE)) == E_PENDING)
    {
        // Keep running
    }
    return result;
    #else
    return S_OK;
    #endif
}

extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API OpenPixCapture(IN LPWSTR filePath)
{
    #ifdef USE_PIX
    PIXOpenCaptureInUI(filePath);
    #endif
}

// --------------------------------------------------------------------------
// UnitySetInterfaces

struct IUnityGraphicsD3D12v7;
static void UNITY_INTERFACE_API OnGraphicsDeviceEvent(UnityGfxDeviceEventType eventType);

typedef decltype(&D3D12SerializeRootSignature) t_D3D12SerializeRootSignature;
typedef void (*t_CreateShaderResourceView)(
			ID3D12Device *pThis,
			ID3D12Resource *pResource,
			const D3D12_SHADER_RESOURCE_VIEW_DESC *pDesc,
			D3D12_CPU_DESCRIPTOR_HANDLE DestDescriptor);
typedef HRESULT (*t_CreateDescriptorHeap)(
			ID3D12Device *pThis,
			const D3D12_DESCRIPTOR_HEAP_DESC *pDescriptorHeapDesc,
			REFIID riid,
			void **ppvHeap);
typedef void (*t_CopyDescriptorsSimple)(
			ID3D12Device *pThis, 
			UINT NumDescriptors,
			D3D12_CPU_DESCRIPTOR_HANDLE DestDescriptorRangeStart,
			D3D12_CPU_DESCRIPTOR_HANDLE SrcDescriptorRangeStart,
			D3D12_DESCRIPTOR_HEAP_TYPE DescriptorHeapsType);

IUnityInterfaces* s_UnityInterfaces = nullptr;
IUnityGraphics* s_Graphics = nullptr;
IUnityLog* s_Log = nullptr;

static HookWrapper<t_D3D12SerializeRootSignature>* s_pSerializeRootSignatureHook = nullptr;
static HookWrapper<t_CreateDescriptorHeap>* s_pCreateDescriptorHeapHook = nullptr;

static HRESULT WINAPI DetourD3D12SerializeRootSignature(
			_In_ const D3D12_ROOT_SIGNATURE_DESC* pRootSignature,
			_In_ D3D_ROOT_SIGNATURE_VERSION Version,
			_Out_ ID3DBlob** ppBlob,
			_Always_(_Outptr_opt_result_maybenull_) ID3DBlob** ppErrorBlob)
{
	D3D12_ROOT_SIGNATURE_DESC desc = *pRootSignature;
	desc.Flags |= D3D12_ROOT_SIGNATURE_FLAG_CBV_SRV_UAV_HEAP_DIRECTLY_INDEXED | D3D12_ROOT_SIGNATURE_FLAG_SAMPLER_HEAP_DIRECTLY_INDEXED;
	const HRESULT result = s_pSerializeRootSignatureHook->GetOriginalPtr()(&desc, Version, ppBlob, ppErrorBlob);
	if (FAILED(result))
    {
        UNITY_LOG_ERROR(s_Log, "Serializing root signature failure");
    }
    return result;
}

ID3D12DescriptorHeap* s_pDescriptorHeap_CBV_SRV_UAV = nullptr;
UINT s_descriptorHeap_CBV_SRV_UAV_IncrementSize = 0;
SIZE_T s_descriptorHeap_CBV_SRV_UAV_CPUDescriptorHandleForHeapStart = 0;

static HRESULT DetourCreateDescriptorHeap(
			ID3D12Device *pThis,
			const D3D12_DESCRIPTOR_HEAP_DESC *pDescriptorHeapDesc,
			REFIID riid,
			void **ppvHeap)
{
	const HRESULT result = s_pCreateDescriptorHeapHook->GetOriginalPtr()(pThis, pDescriptorHeapDesc, riid, ppvHeap);
	
	if (SUCCEEDED(result) && pDescriptorHeapDesc->Type == D3D12_DESCRIPTOR_HEAP_TYPE_CBV_SRV_UAV)
	{
		s_pDescriptorHeap_CBV_SRV_UAV = static_cast<ID3D12DescriptorHeap*>(*ppvHeap);
		s_descriptorHeap_CBV_SRV_UAV_IncrementSize = pThis->GetDescriptorHandleIncrementSize(D3D12_DESCRIPTOR_HEAP_TYPE_CBV_SRV_UAV);
		s_descriptorHeap_CBV_SRV_UAV_CPUDescriptorHandleForHeapStart = s_pDescriptorHeap_CBV_SRV_UAV->GetCPUDescriptorHandleForHeapStart().ptr;
		UNITY_LOG(s_Log, "Created a CBV/SRV/UAV descriptor heap.");
	}

	return result;
}

#define NAMEOF(x) #x

FARPROC GetD3D12SerializeRootSignatureTargetFunction()
{
	return GetProcAddress(GetModuleHandle("d3d12.dll"), NAMEOF(D3D12SerializeRootSignature));
}

extern "C" uint32_t UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API GetSRVDescriptorHeapCount()
{
	if (s_pDescriptorHeap_CBV_SRV_UAV == nullptr)
	{
		UNITY_LOG_ERROR(s_Log, "Failed to get descriptor heap");
		return 0;
	}

	return s_pDescriptorHeap_CBV_SRV_UAV->GetDesc().NumDescriptors;
}

extern "C" int32_t UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API CreateSRVDescriptor(ID3D12Resource* pTexture, uint32_t index)
{
	if (s_pDescriptorHeap_CBV_SRV_UAV == nullptr)
	{
		UNITY_LOG_ERROR(s_Log, "Failed to get descriptor heap");
		return 1;
	}

	const IUnityGraphicsD3D12v7* pD3d12 = s_UnityInterfaces->Get<IUnityGraphicsD3D12v7>();
	ID3D12Device*                pDevice = pD3d12->GetDevice();
	if (pDevice == nullptr)
	{
		UNITY_LOG_ERROR(s_Log, "Failed to get D3D12 device");
		return 2;
	}

	const SIZE_T                ptrOffset = static_cast<SIZE_T>(index) * s_descriptorHeap_CBV_SRV_UAV_IncrementSize;
	D3D12_CPU_DESCRIPTOR_HANDLE descriptorHandle =
	{
		s_descriptorHeap_CBV_SRV_UAV_CPUDescriptorHandleForHeapStart + ptrOffset
	};

	D3D12_SHADER_RESOURCE_VIEW_DESC srvDesc = {};
	srvDesc.Format = pTexture->GetDesc().Format;

    switch (srvDesc.Format)  // NOLINT(clang-diagnostic-switch-enum)
    {
    case DXGI_FORMAT_D16_UNORM:
        srvDesc.Format = DXGI_FORMAT_R16_UNORM;
        break;
    case DXGI_FORMAT_D24_UNORM_S8_UINT:
        srvDesc.Format = DXGI_FORMAT_R24_UNORM_X8_TYPELESS;
        break;
    case DXGI_FORMAT_D32_FLOAT:
        srvDesc.Format = DXGI_FORMAT_R32_FLOAT;
        break;
    case DXGI_FORMAT_D32_FLOAT_S8X24_UINT:
        srvDesc.Format = DXGI_FORMAT_R32_FLOAT_X8X24_TYPELESS;
        break;
    default:
        break;
    }
    
	srvDesc.ViewDimension = D3D12_SRV_DIMENSION_TEXTURE2D;
	srvDesc.Shader4ComponentMapping = D3D12_DEFAULT_SHADER_4_COMPONENT_MAPPING;
	srvDesc.Texture2D.MipLevels = -1;
	srvDesc.Texture2D.PlaneSlice = 0;
	srvDesc.Texture2D.MostDetailedMip = 0;
	srvDesc.Texture2D.ResourceMinLODClamp = 0.0f;
	pDevice->CreateShaderResourceView(pTexture, &srvDesc, descriptorHandle);

	return 0;
}

extern "C" void	UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API UnityPluginLoad(IUnityInterfaces* unityInterfaces)
{
	s_UnityInterfaces = unityInterfaces;
	s_Log = s_UnityInterfaces->Get<IUnityLog>();
	s_Graphics = s_UnityInterfaces->Get<IUnityGraphics>();
    const auto pUnityProfiler = unityInterfaces->Get<IUnityProfiler>();
    s_IsDevelopmentBuild = pUnityProfiler != nullptr ? pUnityProfiler->IsAvailable() : false;

    #ifdef USE_PIX
    const LPWSTR commandLine = GetCommandLineW();
    int argc = 0;
    LPWSTR* argv = CommandLineToArgvW(commandLine, &argc);

    if (s_IsDevelopmentBuild && ShouldLoadWinPixDLL(argc, argv)) 
    {
        // Check to see if a copy of WinPixGpuCapturer.dll has already been injected into the application.
        // This may happen if the application is launched through the PIX UI. 
        if (!IsPixLoaded())
        {
            LoadLibraryW(GetLatestWinPixGpuCapturerPath().c_str());
            UNITY_LOG(s_Log, "Loaded WinPixGpuCapturer.dll.");

            // Hide the overlay
            PIXSetHUDOptions(PIX_HUD_SHOW_ON_NO_WINDOWS);
        }
        else
        {
            UNITY_LOG(s_Log, "WinPixGpuCapturer.dll is already loaded.");
        }   
    }

    LocalFree(static_cast<void*>(argv));

    #endif

    // Make sure the dll is loaded before creating the hooks.
    LoadLibraryW(L"d3d12.dll");
	s_Graphics->RegisterDeviceEventCallback(OnGraphicsDeviceEvent);

	if (MH_Initialize() == MH_OK)
	{
		UNITY_LOG(s_Log, "MH_Initialize success");
	}
	else
	{
		UNITY_LOG_ERROR(s_Log, "MH_Initialize failure");
	}

	s_pSerializeRootSignatureHook = new HookWrapper<t_D3D12SerializeRootSignature>(
		reinterpret_cast<LPVOID>(GetD3D12SerializeRootSignatureTargetFunction())
	);
	s_pSerializeRootSignatureHook->CreateAndEnable(&DetourD3D12SerializeRootSignature);

	// Run OnGraphicsDeviceEvent(initialize) manually on plugin load
	OnGraphicsDeviceEvent(kUnityGfxDeviceEventInitialize);
}

extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API UnityPluginUnload()
{
	s_Graphics->UnregisterDeviceEventCallback(OnGraphicsDeviceEvent);
	s_Graphics = nullptr;
	s_Log = nullptr;

	s_pSerializeRootSignatureHook->Disable();
	delete s_pSerializeRootSignatureHook;
	s_pSerializeRootSignatureHook = nullptr;

	if (MH_Uninitialize() == MH_OK)
	{
		UNITY_LOG(s_Log, "MH_Uninitialize success");
	}
	else
	{
		UNITY_LOG_ERROR(s_Log, "MH_Uninitialize failure");
	}
}

#if UNITY_WEBGL
typedef void	(UNITY_INTERFACE_API * PluginLoadFunc)(IUnityInterfaces* unityInterfaces);
typedef void	(UNITY_INTERFACE_API * PluginUnloadFunc)();

extern "C" void	UnityRegisterRenderingPlugin(PluginLoadFunc loadPlugin, PluginUnloadFunc unloadPlugin);

extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API RegisterPlugin()
{
	UnityRegisterRenderingPlugin(UnityPluginLoad, UnityPluginUnload);
}
#endif

// --------------------------------------------------------------------------
// GraphicsDeviceEvent


static UnityGfxRenderer s_DeviceType = kUnityGfxRendererNull;


static void UNITY_INTERFACE_API OnGraphicsDeviceEvent(UnityGfxDeviceEventType eventType)
{
	// Create graphics API implementation upon initialization
	if (eventType == kUnityGfxDeviceEventInitialize)
	{
		IUnityGraphicsD3D12v7* pD3d12 = s_UnityInterfaces->Get<IUnityGraphicsD3D12v7>();
		ID3D12Device* pDevice = pD3d12->GetDevice();
		if (pDevice != nullptr)
		{
			void** pDeviceVTable = *reinterpret_cast<void***>(pDevice);

			void* fnCreateDescriptorHeap = pDeviceVTable[14];
			s_pCreateDescriptorHeapHook = new HookWrapper<t_CreateDescriptorHeap>(fnCreateDescriptorHeap);
			s_pCreateDescriptorHeapHook->CreateAndEnable(&DetourCreateDescriptorHeap);
		    UNITY_LOG(s_Log, "Hooked CreateDescriptorHeap");
		}
		
		s_DeviceType = s_Graphics->GetRenderer();
	}

	// Cleanup graphics API implementation upon shutdown
	if (eventType == kUnityGfxDeviceEventShutdown)
	{
		s_DeviceType = kUnityGfxRendererNull;

		if (s_pCreateDescriptorHeapHook != nullptr)
		{
			s_pCreateDescriptorHeapHook->Disable();
			delete s_pCreateDescriptorHeapHook;
			s_pCreateDescriptorHeapHook = nullptr;
		}
	}
}

static void UNITY_INTERFACE_API OnRenderEvent(int eventID)
{
}

// --------------------------------------------------------------------------
// GetRenderEventFunc, an example function we export which is used to get a rendering event callback function.

extern "C" UnityRenderingEvent UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API GetRenderEventFunc()
{
	return OnRenderEvent;
}