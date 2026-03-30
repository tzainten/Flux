using BepInSbox;
using BepInSbox.Core.Sbox;
using Flux.Reflection;
using HarmonyLib;
using Sandbox;
using Sandbox.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using static Editor.EditorUtility;

namespace Flux;

[BepInPlugin( "Flux", "tzainten.Flux", "1.0.0" )]
public class FluxBootstrap : BaseSandboxPlugin
{
	protected override void OnPluginLoad()
	{
		base.OnPluginLoad();

		Flux.HarmonyInstance = HarmonyInstance;
		Flux.Instance = new();
		Flux.Instance.OnPluginLoad();
	}
}

public class FluxSystem : GameObjectSystem<FluxSystem>
{
	public FluxSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.FinishUpdate, 0, FinishUpdate, "FluxSystem::FinishUpdate" );
	}

	void FinishUpdate()
	{
		Flux.Instance.Tick();
	}
}

public partial class Flux
{
	public static Flux Instance;

	public static Logger Log = new( "Flux" );

	public static Harmony HarmonyInstance;

	public string Root;

	public string SandboxRoot;

	public Dictionary<string, List<FluxProject>> Projects = new();

	private bool _isCompiling = false;
	private Dictionary<string, byte[]> _pendingHotloads = new();
	private Dictionary<string, DateTime> _hotloadTimestamps = new();

	public void OnPluginLoad()
	{
		Instance = this;

		Root = Path.GetDirectoryName( Managed.This.Location );
		SandboxRoot = Path.GetFullPath( Path.Combine( Root, @"..\..\" ) );

		RunHarmonyPatches();
		GatherAllProjects();
		InjectCommands();
	}

	public void Tick()
	{
		if ( !FluxProject.DirtyProjects.Any() || _isCompiling )
			return;

		_isCompiling = true;
		_ = CompileDirtyProjects();
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

	private async Task CompileDirtyProjects()
	{
		foreach ( var project in FluxProject.DirtyProjects )
		{
			Log.Info( $"Hotloading {project.Package}" );
			_hotloadTimestamps.Add( project.Package, DateTime.Now );

			await project.CompileGroup.BuildAsync();

			if ( !project.Compiler.Output.Successful )
				continue;

			_pendingHotloads[project.Package] = project.Compiler.Output.AssemblyData;
		}

		FluxProject.DirtyProjects.Clear();
		_isCompiling = false;
	}
}
