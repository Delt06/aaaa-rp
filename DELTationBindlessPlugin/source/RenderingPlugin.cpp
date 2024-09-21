// Example low level rendering Unity plugin

#include "PlatformBase.h"
#include "RenderAPI.h"
#include "Unity/IUnityLog.h"
#include "HookWrapper.h"

#include <MinHook.h>

#include <assert.h>
#include <d3d12.h>
#include <d3dx12.h>
#include <dxgi.h>
#include <math.h>
#include <vector>
#include <map>
#include <set>

#include "Unity/IUnityGraphicsD3D12.h"

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
	UNITY_LOG(s_Log, "Serializing root signature...");
	D3D12_ROOT_SIGNATURE_DESC desc = *pRootSignature;
	desc.Flags |= D3D12_ROOT_SIGNATURE_FLAG_CBV_SRV_UAV_HEAP_DIRECTLY_INDEXED | D3D12_ROOT_SIGNATURE_FLAG_SAMPLER_HEAP_DIRECTLY_INDEXED;
	const HRESULT result = s_pSerializeRootSignatureHook->GetOriginalPtr()(&desc, Version, ppBlob, ppErrorBlob);
	if (FAILED(result))
		UNITY_LOG_ERROR(s_Log, "Serializing root signature failure");
	else
		UNITY_LOG(s_Log, "Serializing root signature success");
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
		.ptr = s_descriptorHeap_CBV_SRV_UAV_CPUDescriptorHandleForHeapStart + ptrOffset
	};

	D3D12_SHADER_RESOURCE_VIEW_DESC srvDesc = {};
	srvDesc.Format = pTexture->GetDesc().Format;
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
	s_Graphics->RegisterDeviceEventCallback(OnGraphicsDeviceEvent);
	
#if SUPPORT_VULKAN
	if (s_Graphics->GetRenderer() == kUnityGfxRendererNull)
	{
		extern void RenderAPI_Vulkan_OnPluginLoad(IUnityInterfaces*);
		RenderAPI_Vulkan_OnPluginLoad(unityInterfaces);
	}
#endif // SUPPORT_VULKAN

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


static RenderAPI* s_CurrentAPI = NULL;
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
		}
		else
		{
			UNITY_LOG_ERROR(s_Log, "Failed to get device");
		}
		
		assert(s_CurrentAPI == NULL);
		s_DeviceType = s_Graphics->GetRenderer();
		s_CurrentAPI = CreateRenderAPI(s_DeviceType);
	}

	// Let the implementation process the device related events
	if (s_CurrentAPI)
	{
		s_CurrentAPI->ProcessDeviceEvent(eventType, s_UnityInterfaces);
	}

	// Cleanup graphics API implementation upon shutdown
	if (eventType == kUnityGfxDeviceEventShutdown)
	{
		delete s_CurrentAPI;
		s_CurrentAPI = NULL;
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
	// Unknown / unsupported graphics device type? Do nothing
	if (s_CurrentAPI == NULL)
		return;
}

// --------------------------------------------------------------------------
// GetRenderEventFunc, an example function we export which is used to get a rendering event callback function.

extern "C" UnityRenderingEvent UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API GetRenderEventFunc()
{
	return OnRenderEvent;
}

// --------------------------------------------------------------------------
// DX12 plugin specific
// --------------------------------------------------------------------------

extern "C" UNITY_INTERFACE_EXPORT void* UNITY_INTERFACE_API GetRenderTexture()
{
	return s_CurrentAPI->getRenderTexture();
}

extern "C" UNITY_INTERFACE_EXPORT void UNITY_INTERFACE_API SetRenderTexture(UnityRenderBuffer rb)
{
	s_CurrentAPI->setRenderTextureResource(rb);
}

extern "C" UNITY_INTERFACE_EXPORT bool UNITY_INTERFACE_API IsSwapChainAvailable()
{
	return s_CurrentAPI->isSwapChainAvailable();
}

extern "C" UNITY_INTERFACE_EXPORT unsigned int UNITY_INTERFACE_API GetPresentFlags()
{
	return s_CurrentAPI->getPresentFlags();
}

extern "C" UNITY_INTERFACE_EXPORT unsigned int UNITY_INTERFACE_API GetSyncInterval()
{
	return s_CurrentAPI->getSyncInterval();
}

extern "C" UNITY_INTERFACE_EXPORT unsigned int UNITY_INTERFACE_API GetBackBufferWidth()
{
	return s_CurrentAPI->getBackbufferHeight();
}

extern "C" UNITY_INTERFACE_EXPORT unsigned int UNITY_INTERFACE_API GetBackBufferHeight()
{
	return s_CurrentAPI->getBackbufferWidth();
}
