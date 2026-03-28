using HarmonyLib;
using Sandbox;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Flux;

internal static class AssemblyExtensions
{
	const BindingFlags ALL_DECLARED = BindingFlags.Public | BindingFlags.NonPublic
				| BindingFlags.Instance | BindingFlags.Static
				| BindingFlags.DeclaredOnly;

	internal static void Postfix( this Assembly assembly, string typeName, string patch, BindingFlags flags = ALL_DECLARED )
	{
		var type = assembly.GetType( typeName )
			?? throw new InvalidOperationException( $"Type '{typeName}' not found in '{assembly.GetName().Name}'" );

		var ctors = AccessTools.GetDeclaredConstructors( type, false );
		foreach ( var ctor in ctors )
		{
			Flux.Instance.HarmonyInstance.Patch( ctor, postfix: new HarmonyMethod( typeof( Flux ).GetMethod( patch, ALL_DECLARED ) ) );
		}
	}

	internal static void Prefix( this Assembly assembly, string typeName, string patch, BindingFlags flags = ALL_DECLARED )
	{
		var type = assembly.GetType( typeName )
			?? throw new InvalidOperationException( $"Type '{typeName}' not found in '{assembly.GetName().Name}'" );

		var ctors = AccessTools.GetDeclaredConstructors( type, false );
		foreach ( var ctor in ctors )
		{
			Flux.Instance.HarmonyInstance.Patch( ctor, prefix: new HarmonyMethod( typeof( Flux ).GetMethod( patch, ALL_DECLARED ) ) );
		}
	}

	internal static void Postfix( this Assembly assembly, string typeName, string methodName, string patch, BindingFlags flags = ALL_DECLARED )
	{
		var type = assembly.GetType( typeName )
			?? throw new InvalidOperationException( $"Type '{typeName}' not found in '{assembly.GetName().Name}'" );

		var method = type.GetMethod( methodName, flags )
			?? throw new InvalidOperationException( $"Method '{methodName}' not found on '{typeName}'" );

		Flux.Instance.HarmonyInstance.Patch( method, postfix: new HarmonyMethod( typeof( Flux ).GetMethod( patch, ALL_DECLARED ) ) );
	}

	internal static void Postfix( this Assembly assembly, string typeName, string methodName, Type[] parameters, string patch )
	{
		var type = assembly.GetType( typeName )
			?? throw new InvalidOperationException( $"Type '{typeName}' not found in '{assembly.GetName().Name}'" );

		var method = type.GetMethod( methodName, parameters )
			?? throw new InvalidOperationException( $"Method '{methodName}' not found on '{typeName}'" );

		Flux.Instance.HarmonyInstance.Patch( method, postfix: new HarmonyMethod( typeof( Flux ).GetMethod( patch, ALL_DECLARED ) ) );
	}

	internal static void Prefix( this Assembly assembly, string typeName, string methodName, string patch, BindingFlags flags = ALL_DECLARED )
	{
		var type = assembly.GetType( typeName )
			?? throw new InvalidOperationException( $"Type '{typeName}' not found in '{assembly.GetName().Name}'" );

		var method = type.GetMethod( methodName, flags )
			?? throw new InvalidOperationException( $"Method '{methodName}' not found on '{typeName}'" );

		Flux.Instance.HarmonyInstance.Patch( method, prefix: new HarmonyMethod( typeof( Flux ).GetMethod( patch, ALL_DECLARED ) ) );
	}
}
