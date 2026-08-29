using AwesomeAssertions;
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
			ProjectFileWriter.Write(outputPath, "Generated.Project", rootPath, new[] { csharpFile, jsonFile }, ProjectItemMode.Compile, FolderStructureMode.Full);

			var document = XDocument.Load(outputPath);
			var itemGroup = document.Root!.Element("ItemGroup")!;

			itemGroup.Elements("Compile").Should().ContainSingle();
			itemGroup.Elements("None").Should().ContainSingle();
			itemGroup.Elements("Compile").Single().Element("Link")!.Value.Should().Be(Path.Combine("src", "Changed.cs"));
		}
		finally
		{
			TestDirectory.Delete(rootPath);
		}
	}

	[Fact]
	public void Write_ProjectFolderStructure_KeepsPathsRelativeToNearestProject()
	{
		var rootPath = CreateTemporaryDirectory();

		try
		{
			var projectDirectory = Path.Combine(rootPath, "src", "Sample.Project");
			var nestedDirectory = Path.Combine(projectDirectory, "Features", "FeatureA");
			Directory.CreateDirectory(nestedDirectory);

			var projectFile = Path.Combine(projectDirectory, "Sample.Project.csproj");
			var changedFile = Path.Combine(nestedDirectory, "Changed.cs");
			File.WriteAllText(projectFile, "<Project Sdk=\"Microsoft.NET.Sdk\" />\n");
			File.WriteAllText(changedFile, "class Changed { }\n");

			var outputPath = Path.Combine(rootPath, "Generated.csproj");
			ProjectFileWriter.Write(outputPath, "Generated.Project", rootPath, new[] { changedFile }, ProjectItemMode.Compile, FolderStructureMode.Project);

			var document = XDocument.Load(outputPath);
			var linkPath = document.Root!.Descendants("Compile").Single().Element("Link")!.Value;

			linkPath.Should().Be(Path.Combine("Features", "FeatureA", "Changed.cs"));
		}
		finally
		{
			TestDirectory.Delete(rootPath);
		}
	}

	[Fact]
	public void Write_FlatFolderStructure_UsesOnlyFileNames()
	{
		var rootPath = CreateTemporaryDirectory();

		try
		{
			var nestedDirectory = Path.Combine(rootPath, "src", "FeatureA");
			Directory.CreateDirectory(nestedDirectory);

			var changedFile = Path.Combine(nestedDirectory, "Changed.cs");
			File.WriteAllText(changedFile, "class Changed { }\n");

			var outputPath = Path.Combine(rootPath, "Generated.csproj");
			ProjectFileWriter.Write(outputPath, "Generated.Project", rootPath, new[] { changedFile }, ProjectItemMode.Compile, FolderStructureMode.Flat);

			var document = XDocument.Load(outputPath);
			var linkPath = document.Root!.Descendants("Compile").Single().Element("Link")!.Value;

			linkPath.Should().Be("Changed.cs");
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
