// dllmain.cpp : Defines the entry point for the DLL application.
#include "pch.h"
#include <string>
#include "Types.h"
#include <assert.h>
#include "ManagedRuntime.h"

BOOL APIENTRY DllMain( HMODULE hModule,
                       DWORD  ul_reason_for_call,
                       LPVOID lpReserved
                     )
{
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
    {
        ManagedRuntime Runtime;

        ManagedRuntimeParameters Params;
        Params.TypeName = L"Flux.Flux, Flux";
        Params.MethodName = L"OnPluginLoad";
        i32 ReturnCode = Runtime.Initialize(Params);
    }
    case DLL_THREAD_ATTACH:
    case DLL_THREAD_DETACH:
    case DLL_PROCESS_DETACH:
        break;
    }
    return TRUE;
}