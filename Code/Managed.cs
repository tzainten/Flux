using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Flux;

internal static class Managed
{
	internal static Assembly This => Assembly.GetExecutingAssembly();
	internal static Assembly Engine => AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault( asm => asm.GetName().Name.Contains( "Engine" ) );
	internal static Assembly Access => AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault( asm => asm.GetName().Name.Contains( "Access" ) );
}
