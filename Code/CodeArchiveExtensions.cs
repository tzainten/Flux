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
}
