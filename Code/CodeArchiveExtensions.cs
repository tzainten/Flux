using Sandbox;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace Flux;

public static class CodeArchiveExtensions
{
	public static List<(string Path, string Content)> GetFiles( this CodeArchive archive )
	{
		var files = new List<(string Path, string Content)>();

		foreach ( var syntaxTree in archive.SyntaxTrees )
		{
			var path = syntaxTree.FilePath;
			if ( string.IsNullOrEmpty( path ) ) continue;

			var text = syntaxTree.GetText()?.ToString();
			if ( !string.IsNullOrEmpty( text ) )
			{
				files.Add( (path, text) );
			}
		}

		foreach ( var additionalFile in archive.AdditionalFiles )
		{
			if ( string.IsNullOrEmpty( additionalFile.LocalPath ) ) continue;
			files.Add( (additionalFile.LocalPath, additionalFile.Text) );
		}

		return files;
	}

	public static string MakeCsProjFile( this CodeArchive archive )
	{
		var sb = new StringBuilder();

		sb.AppendLine( "<Project Sdk=\"Microsoft.NET.Sdk.Razor\">" );

		sb.AppendLine( "\t<PropertyGroup>" );
		sb.AppendLine( $"\t\t<RootNamespace>{(string.IsNullOrEmpty( archive.Configuration.RootNamespace ) ? "Sandbox" : archive.Configuration.RootNamespace)}</RootNamespace>" );
		sb.AppendLine( "\t</PropertyGroup>" );

		sb.AppendLine( "\t<ItemGroup>" );
		sb.AppendLine( "\t\t<Using Include=\"Sandbox.Internal.GlobalGameNamespace\" Static=\"true\" />" );
		sb.AppendLine( "\t\t<Using Include=\"Microsoft.AspNetCore.Components\" />" );
		sb.AppendLine( "\t\t<Using Include=\"Microsoft.AspNetCore.Components.Rendering\" />" );
		sb.AppendLine( "\t</ItemGroup>" );

		sb.AppendLine( "\t<ItemGroup>" );
		sb.AppendLine( "\t\t<Analyzer Include=\"$(FACEPUNCH_ENGINE)\\bin\\managed\\Sandbox.CodeUpgrader.dll\"/>" );
		sb.AppendLine( "\t\t<Analyzer Include=\"$(FACEPUNCH_ENGINE)\\bin\\managed\\Sandbox.Generator.dll\"/>" );
		sb.AppendLine( "\t\t<Reference Include=\"$(FACEPUNCH_ENGINE)\\bin\\managed\\Sandbox.System.dll\"/>" );
		sb.AppendLine( "\t\t<Reference Include=\"$(FACEPUNCH_ENGINE)\\bin\\managed\\Sandbox.Engine.dll\"/>" );
		sb.AppendLine( "\t\t<Reference Include=\"$(FACEPUNCH_ENGINE)\\bin\\managed\\Sandbox.Filesystem.dll\"/>" );
		sb.AppendLine( "\t\t<Reference Include=\"$(FACEPUNCH_ENGINE)\\bin\\managed\\Sandbox.Reflection.dll\"/>" );
		sb.AppendLine( "\t\t<Reference Include=\"$(FACEPUNCH_ENGINE)\\bin\\managed\\Sandbox.Mounting.dll\"/>" );
		sb.AppendLine( "\t\t<Reference Include=\"$(FACEPUNCH_ENGINE)\\bin\\managed\\Microsoft.AspNetCore.Components.dll\"/>" );
		sb.AppendLine( "\t</ItemGroup>" );

		sb.AppendLine( "\t<ItemGroup>" );
		sb.AppendLine( "\t\t<ProjectReference Include=\"$(FACEPUNCH_ENGINE)\\addons/base/code/Base Library.csproj\" />" );
		sb.AppendLine( "\t</ItemGroup>" );

		sb.AppendLine( "</Project>" );

		return sb.ToString();
	}
}
