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
		Managed.Compiling.Postfix( "Sandbox.Compiler", "BuildArchive", nameof( Compiler_BuildArchive_Postfix ) );

		Managed.GameInstance.Postfix( "Sandbox.GameInstanceDll", "CloseGame", nameof( GameInstanceDll_CloseGame_Postfix ) );
		Managed.GameInstance.Postfix( "Sandbox.GameInstanceDll", "FinishLoadingAssemblies", nameof( GameInstanceDll_FinishLoadingAssemblies_Postfix ) );

		Managed.Engine.Postfix( "Sandbox.PackageManager+ActivePackage", "CompileCodeArchive", nameof( PackageManager_ActivePackage_CompileCodeArchive_Postfix ) );
		Managed.Engine.Prefix( "Sandbox.Scene", "InitSystems", nameof( Scene_InitSystems_Prefix ) );
	}

	private static void Scene_InitSystems_Prefix( object __instance )
	{
		var scene = (Scene)__instance;

		Game.TypeLibrary.GetType().GetMethod( "AddAssembly", BindingFlags.Instance | BindingFlags.NonPublic ).Invoke( Game.TypeLibrary, [Assembly.GetExecutingAssembly(), true] );
	}

	private static void PackageManager_ActivePackage_CompileCodeArchive_Postfix( object __instance )
	{
		var package = (Package)__activePackage_Package_PropertyInfo.GetValue( __instance );
		__activePackage_Package = package;
		Log.Info( $"'{package.FullIdent}' is the ActivePackage" );
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
			Log.Info( $"Hotloaded {package.FullIdent} in {(DateTime.Now - start).TotalSeconds.ToString( "N2" )}s" );
		}

		Instance._pendingHotloads.Clear();
		Instance._hotloadTimestamps.Clear();
	}

	private static void GameInstanceDll_CloseGame_Postfix( object __instance )
	{
		foreach ( var project in FluxProject.ActiveProjects.ToList() )
		{
			project.Active = false;
		}
	}

	private static void CodeArchive_Deserialize_Postfix( object __instance, byte[] data )
	{
		var archive = (CodeArchive)__instance;
		if ( __activePackage_Package == null || !Instance.Projects.ContainsKey( archive.CompilerName ) )
			return;

		Log.Info( $"Attempting extraction of '{archive.CompilerName}'" );

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

	private static void Compiler_BuildArchive_Postfix( object __instance, CodeArchive __result )
	{
		var compiler = (Compiler)__instance;
		if ( !Instance.Projects.ContainsKey( compiler.Name ) )
			return;

		Log.Info( $"Injecting code into '{compiler.Name}'" );

		foreach ( var project in Instance.Projects[compiler.Name] )
		{
			__result.InjectProject( project );
			compiler.AddSourcePath( project.CodePath );
			compiler.MarkForRecompile();
			project.Active = true;
			project.Compiler = compiler;
		}
	}
}
