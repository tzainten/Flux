using BepInSbox;
using BepInSbox.Core.Sbox;
using BepInSbox.Logging;
using HarmonyLib;
using Sandbox;
using Sandbox.Engine;
using Sandbox.Services;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Flux;

[BepInPlugin( "Flux", "tzainten.Flux", "1.0.0" )]
public class Flux : BaseSandboxPlugin
{
	public static Flux Instance;

	public string Root;

	public string SandboxRoot;

	public Dictionary<string, List<FluxProject>> Projects = new();

	public Package Package;

	public struct FluxProject
	{
		public string Package { get; set; }

		[JsonIgnore]
		public string CodePath { get; set; }
	}

	protected override void OnPluginLoad()
	{
		base.OnPluginLoad();

		Instance = this;

		Root = Path.GetDirectoryName( Managed.This.Location );
		SandboxRoot = Path.GetFullPath( Path.Combine( Root, @"..\..\" ) );

		var compileGroupType = typeof( CompileGroup );
		PatchCompileGroupConstructor( compileGroupType );
		PatchPackageSetter();

		GatherAllProjects();
		InjectCommands();
	}

	void InjectCommands()
	{
		var conVarSystemType = Managed.Engine.GetType( "Sandbox.ConVarSystem" );
		var addAssembly = conVarSystemType.GetMethod( "AddAssembly", BindingFlags.Static | BindingFlags.NonPublic );
		addAssembly?.Invoke( null, new object[] { Managed.This, "flux", null } );
	}

	private void GatherAllProjects()
	{
		foreach ( var dir in Directory.GetDirectories( Root ) )
		{
			var fluxFile = Directory.GetFiles( dir, "*.flux" ).FirstOrDefault();
			if ( fluxFile == null )
				continue;

			var json = Json.ParseToJsonObject( File.ReadAllText( fluxFile ) );

			FluxProject project = Json.Deserialize<FluxProject>( File.ReadAllText( fluxFile ) );
			project.CodePath = Path.Combine( dir, "Code" );
			AddProject( project );
		}
	}

	[ConCmd( "flux_new", Help = "<projectName> <package>" )]
	private static void Cmd_CreateProject( string projectName, string package )
	{
		_ = Instance.CreateProject( projectName, package );
	}

	private async Task CreateProject( string projectName, string package )
	{
		var pack = await Package.FetchAsync( package, true );
		if ( pack == null )
			return;

		var folder = Path.Combine( Root, projectName );
		Directory.CreateDirectory( folder );

		FluxProject project = new();
		project.Package = package;
		project.CodePath = Path.Combine( folder, "Code" );

		CopyDirectory( projectName, package, Path.GetFullPath( Path.Combine( Root, @"..\flex\project_template" ) ), folder );

		AddProject( project );
	}

	static void CopyDirectory( string projectName, string package, string source, string destination )
	{
		Directory.CreateDirectory( destination );

		foreach ( var file in Directory.GetFiles( source ) )
		{
			var path = Path.Combine( destination, Path.GetFileName( file ).Replace( "$projectName", projectName ) );
			File.Copy( file, path, overwrite: true );
			var contents = File.ReadAllText( path );
			contents = contents.Replace( "${projectName}", projectName );
			contents = contents.Replace( "${package}", package );
			contents = contents.Replace( "${sbox}", Instance.SandboxRoot );
			contents = contents.Replace( "${root}", Instance.Root );
			File.WriteAllText( path, contents );
		}

		foreach ( var dir in Directory.GetDirectories( source ) )
			CopyDirectory( projectName, package, dir, Path.Combine( destination, Path.GetFileName( dir ) ) );
	}

	private void AddProject( FluxProject project )
	{
		if ( !Projects.ContainsKey( project.Package ) )
			Projects.Add( project.Package, new() );
		Projects[project.Package].Add( project );
	}

	private void PatchCompileGroupConstructor( Type compileGroupType )
	{
		var ctors = compileGroupType.GetConstructors( BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic );

		foreach ( var ctor in ctors )
		{
			var postfix = new HarmonyMethod( typeof( Flux )
				.GetMethod( nameof( CompileGroup_Constructor ), BindingFlags.Static | BindingFlags.NonPublic ) );

			HarmonyInstance.Patch( ctor, postfix: postfix );
		}
	}

	[HarmonyPostfix]
	private static void CompileGroup_Constructor( object __instance, string name )
	{
		if ( Instance.Package == null )
			return;

		var fullIdent = Instance.Package.FullIdent;
		if ( Instance.Projects.ContainsKey( fullIdent ) )
		{
			var compileGroup = (CompileGroup)__instance;
			foreach ( var project in Instance.Projects[fullIdent] )
			{
				var compiler = compileGroup.GetOrCreateCompiler( $"flux.{fullIdent}" );

				compiler.GeneratedCode.AppendLine( $"global using Microsoft.AspNetCore.Components;" );
				compiler.GeneratedCode.AppendLine( $"global using Microsoft.AspNetCore.Components.Rendering;" );
				compiler.GeneratedCode.AppendLine( $"global using static Sandbox.Internal.GlobalGameNamespace;" );

				compiler.AddReference( $"package.{fullIdent}" );
				compiler.AddSourcePath( project.CodePath );
				compiler.MarkForRecompile();
			}
		}

		Instance.Package = null;
	}

	private void PatchPackageSetter()
	{
		var activePackageType = Managed.Engine.GetType( "Sandbox.PackageManager+ActivePackage" );
		var packageProperty = activePackageType.GetProperty( "Package",
			BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic );
		var packageSetter = packageProperty.SetMethod;

		var postfix = new HarmonyMethod( typeof( Flux )
			.GetMethod( nameof( PackageSetterPostfix ), BindingFlags.Static | BindingFlags.NonPublic ) );

		HarmonyInstance.Patch( packageSetter, postfix: postfix );
	}

	[HarmonyPostfix]
	private static void PackageSetterPostfix( object __instance, Package value )
	{
		Instance.Package = value;
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
	}
}
