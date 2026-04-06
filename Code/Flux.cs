using Flux.Reflection;
using HarmonyLib;
using Sandbox;
using Sandbox.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Serialization;
using static Editor.EditorUtility;

namespace Flux;

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

public struct FluxHotload
{
	public string FullIdent;
	public DateTime Start;
	public byte[] AssemblyData;
	public byte[] CodeArchiveData;
}

public partial class Flux
{
	public static Flux Instance;

	public static Logger Log = new( "Flux" );

	public static Harmony Harmony;

	public string Root;

	public string ModsRoot => Path.Combine( Root, @"..\Mods" );

	public string SandboxRoot;

	public Dictionary<string, List<FluxProject>> Projects = new();

	private bool _isCompiling = false;
	private List<FluxHotload> _pendingHotloads = new();
	private List<FluxHotload> _inFlightHotloads = new();

	[UnmanagedCallersOnly]
	public static void OnPluginLoad()
	{
		Instance = new();
	}

	public Flux()
	{
		Harmony = new Harmony( "Flux.Harmony" );
		Root = Path.GetDirectoryName( Managed.This.Location );
		SandboxRoot = Path.GetFullPath( Path.Combine( Root, @"..\..\" ) );

		RunHarmonyPatches();
		GatherAllProjects();

		MainThread.Queue( () => { InjectCommands(); } );
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
		foreach ( var dir in Directory.GetDirectories( Path.Combine( Root, @"..\Mods" ) ) )
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
		{
			Log.Warning( $"Failed to find '{package}'. Either it doesn't exist, or it's hidden." );
		}

		var folder = Path.Combine( ModsRoot, projectName );
		Directory.CreateDirectory( folder );

		FluxProject project = new();
		project.Name = projectName;
		project.Package = package;
		project.RootPath = folder;
		project.CodePath = Path.Combine( folder, "Code" );

		project.WriteSlnx();
		CopyDirectory( projectName, package, Path.GetFullPath( Path.Combine( Root, @"ProjectTemplate" ) ), folder );

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
		try
		{
			foreach ( var project in FluxProject.DirtyProjects )
			{
				Log.Info( $"Hotloading {project.Package}" );
				DateTime start = DateTime.Now;

				await project.CompileGroup.BuildAsync();

				if ( !project.Compiler.Output.Successful )
					continue;

				_pendingHotloads.Add( new()
				{
					FullIdent = project.Package,
					AssemblyData = project.Compiler.Output.AssemblyData,
					CodeArchiveData = project.Compiler.Output.Archive.Serialize(),
					Start = start
				} );
			}
		}
		catch ( Exception e )
		{
			Log.Error( $"Flux Hotload compilation failed: {e}" );
		}
		finally
		{
			FluxProject.DirtyProjects.Clear();
			_isCompiling = false;
		}
	}
}
