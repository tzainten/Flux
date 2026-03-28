using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;

namespace Flux;

internal static class Managed
{
	private static Assembly FindAssembly( string name )
	{
		var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault( asm => asm.GetName().Name?.Contains( name ) ?? false );
		if ( assembly is null )
			throw new InvalidOperationException( $"Failed to find an assembly that contains '{name}' in AppDomain!" );
		return assembly;
	}

	internal static Assembly This => Assembly.GetExecutingAssembly();
	internal static Assembly Engine => FindAssembly( "Sandbox.Engine" );
	internal static Assembly Access => FindAssembly( "Sandbox.Access" );
	internal static Assembly Compiling => FindAssembly( "Sandbox.Compiling" );
	internal static Assembly Menu => FindAssembly( "Sandbox.Menu" );
	internal static Assembly GameInstance => FindAssembly( "Sandbox.GameInstance" );
	internal static Assembly System => FindAssembly( "Sandbox.System" );
}
