using Sandbox;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;
using static Sandbox.Connection;

namespace Flux;

public struct FluxProject
{
	public string Package { get; set; }

	[JsonIgnore]
	public string Name { get; set; }

	[JsonIgnore]
	public string RootPath { get; set; }

	[JsonIgnore]
	public string CodePath { get; set; }

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
