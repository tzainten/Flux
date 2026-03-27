using BepInSbox;
using BepInSbox.Core.Sbox;
using HarmonyLib;
using Sandbox;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;

namespace Flux;

[BepInPlugin( "Flux", "tzainten.Flux", "1.0.0" )]
public class Flux : BaseSandboxPlugin
{
	public static Flux Instance;

	public string Root;

	public string SandboxRoot;

	public Dictionary<string, List<FluxProject>> Projects = new();

	public string CompilerName;

	protected override void OnPluginLoad()
	{
		base.OnPluginLoad();

		Instance = this;

		Root = Path.GetDirectoryName( Managed.This.Location );
		SandboxRoot = Path.GetFullPath( Path.Combine( Root, @"..\..\" ) );

		RunHarmonyPatches();
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
			project.Name = Path.GetFileName( dir );
			project.RootPath = dir;
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
		project.Name = projectName;
		project.Package = package;
		project.RootPath = folder;
		project.CodePath = Path.Combine( folder, "Code" );

		project.WriteSlnx();
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

	private void RunHarmonyPatches()
	{
		var codeArchiveType = Managed.Compiling.GetType( "Sandbox.CodeArchive" );
		var deserialize = codeArchiveType.GetMethod( "Deserialize", BindingFlags.Instance | BindingFlags.NonPublic );

		var codeArchive_deserialize_postfix = new HarmonyMethod( typeof( Flux )
				.GetMethod( nameof( CodeArchive_Deserialize_Postfix ), BindingFlags.Static | BindingFlags.NonPublic ) );

		HarmonyInstance.Patch( deserialize, postfix: codeArchive_deserialize_postfix );

		var compilerType = typeof( Compiler );
		var updateFromArchive = compilerType.GetMethod( "UpdateFromArchive", BindingFlags.Instance | BindingFlags.Public );

		var compiler_updateFromArchive_postfix = new HarmonyMethod( typeof( Flux )
				.GetMethod( nameof( Compiler_UpdateFromArchive_Postfix ), BindingFlags.Static | BindingFlags.NonPublic ) );

		HarmonyInstance.Patch( updateFromArchive, postfix: compiler_updateFromArchive_postfix );
	}

	[HarmonyPostfix]
	private static void CodeArchive_Deserialize_Postfix( object __instance, byte[] data )
	{
		var codeArchive = (CodeArchive)__instance;
		if ( !Instance.Projects.ContainsKey( codeArchive.CompilerName ) )
			return;

		Instance.CompilerName = codeArchive.CompilerName;

		var files = codeArchive.GetFiles();
		foreach ( var project in Instance.Projects[Instance.CompilerName] )
		{
			var outputPath = Path.Combine( project.RootPath, "ThirdParty", Instance.CompilerName );

			if ( Directory.Exists( outputPath ) )
				Directory.Delete( outputPath, true );

			foreach ( var (path, content) in files )
			{
				var filePath = Path.Combine( outputPath, path );
				Directory.CreateDirectory( Path.GetDirectoryName( filePath ) );
				File.WriteAllText( filePath, content );
			}

			File.WriteAllText( Path.Combine( outputPath, $"{codeArchive.CompilerName}.csproj" ), codeArchive.MakeCsProjFile() );
			project.WriteSlnx();
			project.WriteCsproj();
		}
	}

	[HarmonyPostfix]
	private static void Compiler_UpdateFromArchive_Postfix( object __instance, CodeArchive a )
	{
		if ( string.IsNullOrEmpty( Instance.CompilerName ) )
			return;

		if ( Instance.Projects.ContainsKey( Instance.CompilerName ) )
		{
			var compileGroup = ((Compiler)__instance).Group;
			foreach ( var project in Instance.Projects[Instance.CompilerName] )
			{
				var compiler = compileGroup.GetOrCreateCompiler( $"flux.{Instance.CompilerName}.{project.Name.ToLower()}" );

				compiler.GeneratedCode.AppendLine( $"global using Microsoft.AspNetCore.Components;" );
				compiler.GeneratedCode.AppendLine( $"global using Microsoft.AspNetCore.Components.Rendering;" );
				compiler.GeneratedCode.AppendLine( $"global using static Sandbox.Internal.GlobalGameNamespace;" );

				compiler.AddReference( $"package.{Instance.CompilerName}" );
				compiler.AddSourcePath( project.CodePath );
				compiler.MarkForRecompile();
			}
		}

		Instance.CompilerName = string.Empty;
	}
}
