#pragma once

#include <string>
#include <nethost.h>
#include <coreclr_delegates.h>
#include <hostfxr.h>
#include "Types.h"
#include <Windows.h>
#include <assert.h>
#include <filesystem>

struct ManagedRuntimeParameters
{
	std::wstring RuntimeConfigPath;
	std::wstring TypeName;
	std::wstring MethodName;
};

class ManagedRuntime
{
public:

	i32 Initialize(ManagedRuntimeParameters Params)
	{
		size_t Length = MAX_PATH;
		char_t HostBuffer[MAX_PATH];

		i32 ReturnCode = get_hostfxr_path(HostBuffer, &Length, nullptr);
		if (ReturnCode != 0)
		{
			return ReturnCode;
		}

		std::wstring HostPath(HostBuffer);

		void* HostFXR = LoadLibrary(HostPath.c_str());
		assert(HostFXR);

		GetRuntimeDelegate = (hostfxr_get_runtime_delegate_fn)GetProcAddress((HMODULE)HostFXR, "hostfxr_get_runtime_delegate");
		assert(GetRuntimeDelegate);

		InitializeForRuntimeConfig = (hostfxr_initialize_for_runtime_config_fn)GetProcAddress((HMODULE)HostFXR, "hostfxr_initialize_for_runtime_config");
		assert(InitializeForRuntimeConfig);

		CloseHandle = (hostfxr_close_fn)GetProcAddress((HMODULE)HostFXR, "hostfxr_close");
		assert(CloseHandle);

		size_t DotNetIdx = HostPath.rfind(L"\\dotnet\\");
		assert(DotNetIdx != std::string::npos);

		std::wstring DotNetPath(HostPath.begin(), HostPath.end());
		DotNetPath = DotNetPath.substr(0, DotNetIdx + 7);

		hostfxr_initialize_parameters InitializeParameters;
		InitializeParameters.dotnet_root = DotNetPath.c_str();
		InitializeParameters.host_path = HostPath.c_str();

		char_t ModuleBuffer[MAX_PATH] = { 0 };

		GetModuleFileName(NULL, ModuleBuffer, MAX_PATH);

		std::wstring RuntimeConfigPath = std::filesystem::path(ModuleBuffer).parent_path() / L"sbox.runtimeconfig.json";

		InitializeForRuntimeConfig(RuntimeConfigPath.c_str(), &InitializeParameters, &RuntimeHandle);
		assert(RuntimeHandle);

		GetRuntimeDelegate(RuntimeHandle, hostfxr_delegate_type::hdt_load_assembly_and_get_function_pointer, (void**)&LoadAssemblyAndGetFunctionPointer);
		assert(LoadAssemblyAndGetFunctionPointer);

		std::wstring EntryPointPath = std::filesystem::path(ModuleBuffer).parent_path() / L"Flux/Core/Flux.dll";

		component_entry_point_fn EntryPoint = NULL;
		ReturnCode = LoadAssemblyAndGetFunctionPointer(
			EntryPointPath.c_str(),
			Params.TypeName.c_str(),
			Params.MethodName.c_str(),
			UNMANAGEDCALLERSONLY_METHOD,
			NULL,
			(void**)&EntryPoint
		);

		if (ReturnCode == 0 && EntryPoint != nullptr)
		{
			EntryPoint(nullptr, 0);
		}

		return ReturnCode;
	}

private: // Delegates

	hostfxr_handle								RuntimeHandle;
	hostfxr_close_fn							CloseHandle;

	hostfxr_initialize_for_runtime_config_fn	InitializeForRuntimeConfig;
	hostfxr_get_runtime_delegate_fn				GetRuntimeDelegate;
	load_assembly_and_get_function_pointer_fn	LoadAssemblyAndGetFunctionPointer;

};
