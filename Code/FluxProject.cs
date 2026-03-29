using HarmonyLib;
using Sandbox;
using Sandbox.Diagnostics;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;
using static Editor.EditorUtility;
using static Sandbox.Connection;

namespace Flux;

public class FluxProject
{
	public static List<FluxProject> All = new();

	public static List<FluxProject> ActiveProjects = new();

	public static List<FluxProject> DirtyProjects = new();

	public string Package { get; set; }

	[JsonIgnore]
	public string Name { get; set; }

	[JsonIgnore]
	public string RootPath { get; set; }

	[JsonIgnore]
	public string CodePath { get; set; }

	[JsonIgnore]
	public bool Active
	{
		get;
		set
		{
			field = value;
			OnActiveChanged( value );
		}
	}

	[JsonIgnore]
	public FileSystemWatcher Watcher;

	[JsonIgnore]
	public CompileGroup CompileGroup => Compiler.Group;

	[JsonIgnore]
	public Compiler Compiler;

	public FluxProject()
	{
		All.Add( this );
	}

	private Timer _watcherDebounce;

	private void OnActiveChanged( bool newActive )
	{
		if ( newActive && !ActiveProjects.Contains( this ) )
		{
			ActiveProjects.Add( this );

			Watcher = new FileSystemWatcher( RootPath, "*.cs" )
			{
				IncludeSubdirectories = true,
				NotifyFilter = NotifyFilters.LastWrite
						 | NotifyFilters.FileName
						 | NotifyFilters.DirectoryName,
			};

			void Hotload()
			{
				_watcherDebounce?.Dispose();
				_watcherDebounce = new Timer( _ =>
				{
					DirtyProjects.Add( this );
					Compiler.MarkForRecompile();
				}, null, TimeSpan.FromMilliseconds( 300 ), Timeout.InfiniteTimeSpan );
			}

			Watcher.Changed += ( sender, e ) => { Hotload(); };
			Watcher.Created += ( sender, e ) => { Hotload(); };
			Watcher.Deleted += ( sender, e ) => { Hotload(); };
			Watcher.Renamed += ( sender, e ) => { Hotload(); };

			Watcher.EnableRaisingEvents = true;
		}

		if ( !newActive )
		{
			ActiveProjects.Remove( this );

			Watcher.EnableRaisingEvents = false;
			Watcher = null;
		}
	}

	public List<string> GetFiles()
	{
		List<string> files = new List<string>();

		var options = new EnumerationOptions { RecurseSubdirectories = true };
		files.AddRange( Directory.GetFiles( CodePath, "*.cs", options ) );
		files.AddRange( Directory.GetFiles( CodePath, "*.razor", options ) );

		return files;
	}

	internal void WriteCsproj()
	{
		var thirdParty = Path.Combine( RootPath, "ThirdParty" );
		Directory.CreateDirectory( thirdParty );

		var sb = new StringBuilder();
		foreach ( var packagePath in Directory.GetDirectories( thirdParty ) )
		{
			var ident = Path.GetFileName( packagePath );
			sb.AppendLine( $"<ProjectReference Include=\"../ThirdParty/{ident}/{ident}.csproj\" />" );
		}

		var file = Path.Combine( RootPath, $"Code/{Name}.csproj" );
		if ( !File.Exists( file ) )
			return;

		var contents = File.ReadAllText( file );
		contents = contents.Replace( "${thirdParty}", sb.ToString() );
		File.WriteAllText( file, contents );
	}

	internal void WriteSlnx()
	{
		var sb = new StringBuilder();
		sb.AppendLine( "<Solution>" );

		sb.AppendLine( $"\t<Project Path=\"Code/{Name}.csproj\" />" );
		sb.AppendLine( "\t<Folder Name=\"/ThirdParty/\">" );

		var thirdParty = Path.Combine( RootPath, "ThirdParty" );
		if ( Directory.Exists( thirdParty ) )
		{
			foreach ( var packagePath in Directory.GetDirectories( Path.Combine( RootPath, "ThirdParty" ) ) )
			{
				var ident = Path.GetFileName( packagePath );
				sb.AppendLine( $"\t\t<Project Path=\"ThirdParty/{ident}/{ident}.csproj\" />" );
			}
		}

		sb.AppendLine( "\t</Folder>" );
		sb.AppendLine( "</Solution>" );

		File.WriteAllText( Path.Combine( RootPath, $"{Name}.slnx" ), sb.ToString() );
	}
}
