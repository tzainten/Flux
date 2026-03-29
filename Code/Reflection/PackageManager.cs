using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Flux.Reflection;

internal static class PackageManager
{
	private static Type Type = Managed.Engine.GetType( "Sandbox.PackageManager" );

	private static PropertyInfo __packageManager_ActivePackages_PropertyInfo = Type.GetProperty( "ActivePackages", BindingFlags.Static | BindingFlags.Public );

	public static IEnumerable<object> ActivePackages => __packageManager_ActivePackages_PropertyInfo.GetValue( null ) as IEnumerable<object>;
}
