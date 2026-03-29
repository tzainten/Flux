using Microsoft.CodeAnalysis.CSharp;
using Sandbox;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using static Sandbox.CodeArchive;

namespace Flux;

public static class CodeArchiveExtensions
{
	public static void InjectProject( this CodeArchive archive, FluxProject project )
	{
		if ( archive.CompilerName != project.Package )
			throw new NotSupportedException( $"Project {project.Name} cannot be injected into package {archive.CompilerName}!" );

		foreach ( var file in project.GetFiles() )
		{
			var localPath = Path.GetRelativePath( project.CodePath, file );
			var content = File.ReadAllText( file );
			archive.AddFile( file, localPath, content );
		}

		for ( int i = 0; i < archive.SyntaxTrees.Count; i++ )
		{
			var tree = archive.SyntaxTrees[i];
			var filePath = tree.FilePath;

			var modFilePath = Path.Combine( project.RootPath, "ThirdParty", archive.CompilerName, filePath );
			if ( !File.Exists( modFilePath ) )
				continue;

			var newContent = File.ReadAllText( modFilePath );
			var newTree = CSharpSyntaxTree.ParseText(
				newContent,
				path: filePath,
				encoding: Encoding.UTF8,
				options: archive.Configuration.GetParseOptions()
			);
			archive.SyntaxTrees[i] = newTree;
		}
	}

	public static void AddFile( this CodeArchive archive, string physicalPath, string localPath, string content )
	{
		if ( localPath.Contains( "Assembly.cs" ) )
			return;

		archive.FileMap[physicalPath] = localPath;

		if ( Path.GetExtension( localPath ).Equals( ".cs", StringComparison.OrdinalIgnoreCase ) )
		{
			archive.SyntaxTrees.RemoveAll( t => string.Equals( t.FilePath, localPath, StringComparison.OrdinalIgnoreCase ) );

			var parseOptions = archive.Configuration.GetParseOptions()
				?? CSharpParseOptions.Default.WithLanguageVersion( LanguageVersion.CSharp14 );
			var syntaxTree = CSharpSyntaxTree.ParseText( content, path: localPath, encoding: Encoding.UTF8, options: parseOptions );
			archive.SyntaxTrees.Add( syntaxTree );
		}
		else
		{
			archive.AdditionalFiles.RemoveAll( f => string.Equals( f.LocalPath, localPath, StringComparison.OrdinalIgnoreCase ) );
			archive.AdditionalFiles.Add( new AdditionalFile( content, localPath ) );
		}
	}

	public static List<(string Path, string Content)> GetFiles( this CodeArchive archive )
	{
		var files = new List<(string Path, string Content)>();

		foreach ( var syntaxTree in archive.SyntaxTrees )
		{
			var path = syntaxTree.FilePath;
			if ( string.IsNullOrEmpty( path ) ) continue;

			var text = syntaxTree.GetText()?.ToString();
			if ( !string.IsNullOrEmpty( text ) && !path.Contains( "__compiler_extra" ) )
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
