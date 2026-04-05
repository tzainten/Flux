using Flux.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Sandbox;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using static Editor.EditorUtility;

namespace Flux;

public partial class Flux
{
	private static Package __activePackage_Package;

	private static PropertyInfo __activePackage_Package_PropertyInfo = Managed.Engine.GetType( "Sandbox.PackageManager+ActivePackage" ).GetProperty( "Package", BindingFlags.Instance | BindingFlags.Public );
	private static PropertyInfo __activePackage_AssemblyFileSystem_PropertyInfo = Managed.Engine.GetType( "Sandbox.PackageManager+ActivePackage" ).GetProperty( "AssemblyFileSystem", BindingFlags.Instance | BindingFlags.Public );

	private static HashSet<CompileGroup> __compileGroups = new();

	private void RunHarmonyPatches()
	{
		Managed.Compiling.Postfix( "Sandbox.Compiler", "BuildArchive", nameof( Compiler_BuildArchive_Postfix ) );
		Managed.Compiling.Prefix( "Sandbox.CompileGroup", "Dispose", nameof( CompileGroup_Dispose_Prefix ) );

		Managed.GameInstance.Postfix( "Sandbox.GameInstanceDll", "CloseGame", nameof( GameInstanceDll_CloseGame_Postfix ) );
		Managed.GameInstance.Prefix( "Sandbox.GameInstanceDll", "FinishLoadingAssemblies", nameof( GameInstanceDll_FinishLoadingAssemblies_Prefix ) );
		Managed.GameInstance.Postfix( "Sandbox.GameInstanceDll", "FinishLoadingAssemblies", nameof( GameInstanceDll_FinishLoadingAssemblies_Postfix ) );

		Managed.Engine.Postfix( "Sandbox.PackageManager+ActivePackage", "CompileCodeArchive", nameof( PackageManager_ActivePackage_CompileCodeArchive_Postfix ) );
		Managed.Engine.Prefix( "Sandbox.Scene", "InitSystems", nameof( Scene_InitSystems_Prefix ) );
	}

	private static bool CompileGroup_Dispose_Prefix( object __instance )
	{
		var group = (CompileGroup)__instance;
		if ( __compileGroups.Contains( group ) )
			return false;

		return true;
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

	private static void GameInstanceDll_FinishLoadingAssemblies_Prefix()
	{
		if ( !Instance._pendingHotloads.Any() )
			return;

		Instance._inFlightHotloads.Clear();

		var pendingHotloads = Instance._pendingHotloads.ToArray();
		Instance._pendingHotloads.Clear();

		foreach ( var hotload in pendingHotloads )
		{
			var ident = hotload.FullIdent;
			var data = hotload.AssemblyData;
			var archiveData = hotload.CodeArchiveData;

			var activePackage = PackageManager.ActivePackages.FirstOrDefault( ap =>
			{
				var pkg = (Package)__activePackage_Package_PropertyInfo.GetValue( ap );
				return pkg.FullIdent == ident;
			} );

			Package package = activePackage != null
				? (Package)__activePackage_Package_PropertyInfo.GetValue( activePackage )
				: Package.FetchAsync( ident, true ).GetAwaiter().GetResult();

			PackageLoader.LoadAssemblyDirect( package, ident, data, archiveData );
			Instance._inFlightHotloads.Add( hotload );
		}
	}

	private static void GameInstanceDll_FinishLoadingAssemblies_Postfix()
	{
		if ( !Instance._inFlightHotloads.Any() )
			return;

		foreach ( var hotload in Instance._inFlightHotloads )
		{
			Log.Info( $"Hotloaded {hotload.FullIdent} in {(DateTime.Now - hotload.Start).TotalSeconds.ToString( "N2" )}s" );
		}

		Instance._inFlightHotloads.Clear();
	}

	private static void GameInstanceDll_CloseGame_Postfix( object __instance )
	{
		foreach ( var project in FluxProject.ActiveProjects.ToList() )
		{
			project.Active = false;
		}

		foreach ( var group in __compileGroups )
		{
			group.Dispose();
		}
		__compileGroups.Clear();
	}

	private static void Compiler_BuildArchive_Postfix( object __instance, CompilerOutput output, CodeArchive __result )
	{
		var compiler = (Compiler)__instance;
		if ( !Instance.Projects.ContainsKey( compiler.Name ) )
			return;

		var group = compiler.Group;
		if ( !__compileGroups.Contains( group ) )
			__compileGroups.Add( group );

		var archive = __result;

		Log.Info( $"Attempting extraction of '{archive.CompilerName}'" );

		var files = archive.GetFiles();
		foreach ( var project in Instance.Projects[archive.CompilerName] )
		{
			var outputPath = Path.Combine( project.RootPath, "ThirdParty", archive.CompilerName );
			var revisionPath = Path.Combine( outputPath, "REVISION" );

			if ( __activePackage_Package != null && __activePackage_Package.Revision != null )
			{
				var shouldExtract = !File.Exists( revisionPath ) || long.Parse( File.ReadAllText( revisionPath ) ) != __activePackage_Package.Revision.VersionId;

				if ( !shouldExtract )
					continue;
			}

			if ( Directory.Exists( outputPath ) )
				continue; // @TODO: Maybe we should clean the directory if the revision doesn't match? But then the user would lose any manual changes they made! Tricky...

			Directory.CreateDirectory( outputPath );

			foreach ( var (path, content) in files )
			{
				var localPath = path;
				if ( archive.FileMap.TryGetValue( path, out var mappedPath ) )
					localPath = mappedPath;

				var filePath = Path.Combine( outputPath, localPath );
				Directory.CreateDirectory( Path.GetDirectoryName( filePath ) );
				File.WriteAllText( filePath, content );
			}

			var csProjPath = Path.Combine( outputPath, $"{archive.CompilerName}.csproj" );
			if ( !File.Exists( csProjPath ) )
				File.WriteAllText( csProjPath, archive.MakeCsProjFile() );

			if ( __activePackage_Package != null && __activePackage_Package.Revision != null )
				File.WriteAllText( revisionPath, __activePackage_Package.Revision.VersionId.ToString() );

			project.WriteSlnx();
			project.WriteCsproj();
		}

		Log.Info( $"Injecting code into '{compiler.Name}'" );

		foreach ( var project in Instance.Projects[compiler.Name] )
		{
			__result.InjectProject( project );

			if ( project.Compiler != null )
				continue;

			compiler.AddSourcePath( project.CodePath );
			compiler.MarkForRecompile();
			project.Active = true;
			project.Compiler = compiler;
		}

		__activePackage_Package = null;
	}
}
