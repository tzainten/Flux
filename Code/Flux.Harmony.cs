using Flux.Reflection;
using Sandbox;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Flux;

public partial class Flux
{
	private static Package __activePackage_Package;

	private static PropertyInfo __activePackage_Package_PropertyInfo = Managed.Engine.GetType( "Sandbox.PackageManager+ActivePackage" ).GetProperty( "Package", BindingFlags.Instance | BindingFlags.Public );
	private static PropertyInfo __activePackage_AssemblyFileSystem_PropertyInfo = Managed.Engine.GetType( "Sandbox.PackageManager+ActivePackage" ).GetProperty( "AssemblyFileSystem", BindingFlags.Instance | BindingFlags.Public );

	private void RunHarmonyPatches()
	{
		Managed.Compiling.Postfix( "Sandbox.CodeArchive", "Deserialize", nameof( CodeArchive_Deserialize_Postfix ) );
		Managed.Compiling.Postfix( "Sandbox.Compiler", "UpdateFromArchive", nameof( Compiler_UpdateFromArchive_Postfix ) );
		Managed.Compiling.Postfix( "Sandbox.Compiler", "BuildArchive", nameof( Compiler_BuildArchive_Postfix ) );

		Managed.GameInstance.Postfix( "Sandbox.GameInstanceDll", "CloseGame", nameof( GameInstanceDll_CloseGame_Postfix ) );
		Managed.GameInstance.Postfix( "Sandbox.GameInstanceDll", "FinishLoadingAssemblies", nameof( GameInstanceDll_FinishLoadingAssemblies_Postfix ) );

		Managed.Engine.Postfix( "Sandbox.PackageManager+ActivePackage", "CompileCodeArchive", nameof( PackageManager_ActivePackage_CompileCodeArchive_Postfix ) );
	}

	private static void PackageManager_ActivePackage_CompileCodeArchive_Postfix( object __instance )
	{
		var package = (Package)__activePackage_Package_PropertyInfo.GetValue( __instance );
		__activePackage_Package = package;
	}

	private static void GameInstanceDll_FinishLoadingAssemblies_Postfix()
	{
		if ( !Instance._pendingHotloads.Any() )
			return;

		var activePackages = PackageManager.ActivePackages.Where( ap =>
		{
			var package = (Package)__activePackage_Package_PropertyInfo.GetValue( ap );
			return Instance._pendingHotloads.ContainsKey( package.FullIdent );
		} );

		foreach ( var activePackage in activePackages )
		{
			var package = (Package)__activePackage_Package_PropertyInfo.GetValue( activePackage );
			var assemblyFileSystem = __activePackage_AssemblyFileSystem_PropertyInfo.GetValue( activePackage ) as BaseFileSystem;

			var dllFile = assemblyFileSystem?.FindFile( "", "*.dll", true )
				.FirstOrDefault( f => f.Contains( package.FullIdent ) );

			if ( dllFile == null )
				continue;

			GameInstanceDll.LoadAssemblyFromPackage( activePackage, dllFile, Instance._pendingHotloads[package.FullIdent] );

			var start = Instance._hotloadTimestamps[package.FullIdent];
			Instance.Logger.LogInfo( $"Hotloaded {package.FullIdent} in {(DateTime.Now - start).TotalSeconds.ToString( "N2" )}s" );
		}

		Instance._pendingHotloads.Clear();
		Instance._hotloadTimestamps.Clear();
	}

	private static void GameInstanceDll_CloseGame_Postfix( object __instance )
	{
		foreach ( var project in FluxProject.All )
		{
			project.Active = false;
		}
	}

	private static void CodeArchive_Deserialize_Postfix( object __instance, byte[] data )
	{
		var archive = (CodeArchive)__instance;
		if ( !Instance.Projects.ContainsKey( archive.CompilerName ) )
			return;

		var files = archive.GetFiles();
		foreach ( var project in Instance.Projects[archive.CompilerName] )
		{
			var outputPath = Path.Combine( project.RootPath, "ThirdParty", archive.CompilerName );
			var revisionPath = Path.Combine( outputPath, "REVISION" );

			var shouldExtract = !File.Exists( revisionPath ) || long.Parse( File.ReadAllText( revisionPath ) ) != __activePackage_Package.Revision.VersionId;

			if ( !shouldExtract )
				continue;

			if ( Directory.Exists( outputPath ) )
				Directory.Delete( outputPath, true );

			foreach ( var (path, content) in files )
			{
				var filePath = Path.Combine( outputPath, path );
				Directory.CreateDirectory( Path.GetDirectoryName( filePath ) );
				File.WriteAllText( filePath, content );
			}

			var csProjPath = Path.Combine( outputPath, $"{archive.CompilerName}.csproj" );
			if ( !File.Exists( csProjPath ) )
				File.WriteAllText( csProjPath, archive.MakeCsProjFile() );

			File.WriteAllText( revisionPath, __activePackage_Package.Revision.VersionId.ToString() );

			project.WriteSlnx();
			project.WriteCsproj();
		}

		__activePackage_Package = null;
	}

	private static void Compiler_UpdateFromArchive_Postfix( object __instance, CodeArchive a )
	{
		if ( string.IsNullOrEmpty( a.CompilerName ) )
			return;

		if ( !Instance.Projects.ContainsKey( a.CompilerName ) )
			return;

		var compiler = (Compiler)__instance;
		foreach ( var project in Instance.Projects[a.CompilerName] )
		{
			compiler.AddSourcePath( project.CodePath );
			project.Active = true;
		}
	}

	private static void Compiler_BuildArchive_Postfix( object __instance, CodeArchive __result )
	{
		var compiler = (Compiler)__instance;
		if ( !Instance.Projects.ContainsKey( compiler.Name ) )
			return;

		foreach ( var project in Instance.Projects[compiler.Name] )
		{
			__result.InjectProject( project );
			project.Compiler = compiler;
		}
	}
}
