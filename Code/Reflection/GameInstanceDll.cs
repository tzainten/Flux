using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Flux.Reflection;

internal static class GameInstanceDll
{
	private static Type Type => Managed.GameInstance.GetType( "Sandbox.GameInstanceDll" );
	private static object PackageLoader => Type.GetProperty( "PackageLoader", BindingFlags.Static | BindingFlags.Public ).GetValue( null );

	private static MethodInfo LoadAssemblyFromPackageInfo = PackageLoader.GetType().GetMethod( "LoadAssemblyFromPackage",
			BindingFlags.Instance | BindingFlags.NonPublic );

	internal static void LoadAssemblyFromPackage( object ap, string filename, byte[] bytes = null )
	{
		LoadAssemblyFromPackageInfo.Invoke( PackageLoader, [ap, filename, bytes] );
	}
}
