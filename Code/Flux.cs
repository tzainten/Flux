using BepInSbox;
using BepInSbox.Core.Sbox;
using HarmonyLib;
using Sandbox;
using System.Reflection;

namespace Flux;

[BepInPlugin( "Flux", "tzainten.Flux", "1.0.0" )]
public class Flux : BaseSandboxPlugin
{
	public static Flux Instance;

	protected override void OnPluginLoad()
	{
		base.OnPluginLoad();

		//var asm = AppDomain.CurrentDomain.GetAssemblies().First( asm => asm.GetName().Name.Contains( "Engine" ) );
		//var pmType = asm.GetType( "Sandbox.PackageManager" );
		//var apType = asm.GetType( "Sandbox.PackageManager+ActivePackage" );

		//Logger.LogInfo( pmType );
		//Logger.LogInfo( apType );

		//var compilerType = typeof( Compiler );

		Instance = this;

		var compileGroupType = typeof( CompileGroup );
		PatchCompileGroupConstructor( compileGroupType );
	}

	private void PatchCompileGroupConstructor( Type compileGroupType )
	{
		var ctors = compileGroupType.GetConstructors( BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic );

		foreach ( var ctor in ctors )
		{
			var postfix = new HarmonyMethod( typeof( Flux )
				.GetMethod( nameof( CompileGroupCtorPostfix ), BindingFlags.Static | BindingFlags.NonPublic ) );

			HarmonyInstance.Patch( ctor, postfix: postfix );
		}
	}

	[HarmonyPostfix]
	private static void CompileGroupCtorPostfix( object __instance, string name )
	{
		var compileGroup = (CompileGroup)__instance;
		if ( name == "sandbox" )
		{
			var compiler = compileGroup.GetOrCreateCompiler( "Flux.Loader" );

			compiler.GeneratedCode.AppendLine( $"global using Microsoft.AspNetCore.Components;" );
			compiler.GeneratedCode.AppendLine( $"global using Microsoft.AspNetCore.Components.Rendering;" );
			compiler.GeneratedCode.AppendLine( $"global using static Sandbox.Internal.GlobalGameNamespace;" );

			compiler.AddReference( "package.facepunch.sandbox" );
			compiler.AddSourcePath( @"C:\Users\tzainten\Desktop\Code" );
			compiler.MarkForRecompile();
		}
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
	}
}
