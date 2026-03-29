using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Flux.Reflection;

internal static class PackageManager
{
	private static Type Type => Managed.Engine.GetType( "Sandbox.PackageManager" );
	public static IEnumerable<object> ActivePackages => Type.GetProperty( "ActivePackages", BindingFlags.Static | BindingFlags.Public ).GetValue( null ) as IEnumerable<object>;
}
