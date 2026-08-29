using System.Xml.Linq;
using RonSijm.Git2Proj.Cli;
using RonSijm.Git2Proj.ProjectGeneration;

namespace RonSijm.Git2Proj.Tests;

public sealed class ProjectFileWriterTests
{
	[Fact]
	public void Write_CompileMode_MapsCSharpFilesToCompileItems()
	{
		var rootPath = CreateTemporaryDirectory();

		try
		{
			var srcDirectory = Path.Combine(rootPath, "src");
			Directory.CreateDirectory(srcDirectory);

			var csharpFile = Path.Combine(srcDirectory, "Changed.cs");
			var jsonFile = Path.Combine(srcDirectory, "appsettings.json");
			File.WriteAllText(csharpFile, "class Changed { }\n");
			File.WriteAllText(jsonFile, "{}\n");

			var outputPath = Path.Combine(rootPath, "Generated.csproj");
			ProjectFileWriter.Write(outputPath, "Generated.Project", rootPath, new[] { csharpFile, jsonFile }, ProjectItemMode.Compile);

			var document = XDocument.Load(outputPath);
			var itemGroup = document.Root!.Element("ItemGroup")!;

			Assert.Single(itemGroup.Elements("Compile"));
			Assert.Single(itemGroup.Elements("None"));
			Assert.Equal(Path.Combine("src", "Changed.cs"), itemGroup.Elements("Compile").Single().Element("Link")!.Value);
		}
		finally
		{
			TestDirectory.Delete(rootPath);
		}
	}

	private static string CreateTemporaryDirectory()
	{
		var path = Path.Combine(Path.GetTempPath(), $"git2proj-tests-{Guid.NewGuid():N}");
		Directory.CreateDirectory(path);
		return path;
	}
}
