using AwesomeAssertions;
using System.Diagnostics;
using System.Xml.Linq;
using RonSijm.Git2Proj.SourceAnalysis;

namespace RonSijm.Git2Proj.Tests;

public sealed class ReferenceDepthExpanderTests
{
	[Fact]
	public async Task ExpandAsync_UsesDepthToIncludeReferencedSourceFiles()
	{
		var rootPath = CreateTemporaryDirectory();

		try
		{
			var projectPath = Path.Combine(rootPath, "Sample");
			await RunDotNetAsync(rootPath, "new", "classlib", "-n", "Sample", "--framework", "net10.0");

			var projectFilePath = Path.Combine(projectPath, "Sample.csproj");
			var changedFilePath = Path.Combine(projectPath, "Changed.cs");
			var helperFilePath = Path.Combine(projectPath, "Helper.cs");
			var leafFilePath = Path.Combine(projectPath, "Leaf.cs");

			await File.WriteAllTextAsync(projectFilePath, """
				<Project Sdk="Microsoft.NET.Sdk">
				  <PropertyGroup>
				    <TargetFramework>net10.0</TargetFramework>
				    <ImplicitUsings>enable</ImplicitUsings>
				    <Nullable>enable</Nullable>
				  </PropertyGroup>
				</Project>
				""");
			await File.WriteAllTextAsync(changedFilePath, """
				namespace Sample;

				public sealed class Changed
				{
					private readonly Helper helper = new();
				}
				""");
			await File.WriteAllTextAsync(helperFilePath, """
				namespace Sample;

				public sealed class Helper
				{
					private readonly Leaf leaf = new();
				}
				""");
			await File.WriteAllTextAsync(leafFilePath, """
				namespace Sample;

				public sealed class Leaf
				{
				}
				""");

			var depthOneFiles = await ReferenceDepthExpander.ExpandAsync(rootPath, [changedFilePath], 1, CancellationToken.None);
			var depthTwoFiles = await ReferenceDepthExpander.ExpandAsync(rootPath, [changedFilePath], 2, CancellationToken.None);

			depthOneFiles.Should().Contain(changedFilePath);
			depthOneFiles.Should().Contain(helperFilePath);
			depthOneFiles.Should().NotContain(leafFilePath);

			depthTwoFiles.Should().Contain(changedFilePath);
			depthTwoFiles.Should().Contain(helperFilePath);
			depthTwoFiles.Should().Contain(leafFilePath);
		}
		finally
		{
			TestDirectory.Delete(rootPath);
		}
	}

	[Fact]
	public async Task Generate_WithReferenceDepth_AddsReferencedFilesToProject()
	{
		var rootPath = CreateTemporaryDirectory();

		try
		{
			var repositoryPath = Path.Combine(rootPath, "Repo");
			await RunDotNetAsync(rootPath, "new", "classlib", "-n", "Repo", "--framework", "net10.0");
			await RunGitAsync(repositoryPath, "init");
			await RunGitAsync(repositoryPath, "config", "user.email", "git2proj@example.test");
			await RunGitAsync(repositoryPath, "config", "user.name", "Git2Proj Tests");

			var classFilePath = Path.Combine(repositoryPath, "Class1.cs");
			var helperFilePath = Path.Combine(repositoryPath, "Helper.cs");
			var leafFilePath = Path.Combine(repositoryPath, "Leaf.cs");

			await File.WriteAllTextAsync(classFilePath, """
				namespace Repo;

				public sealed class Class1
				{
					private readonly Helper helper = new();
				}
				""");
			await File.WriteAllTextAsync(helperFilePath, """
				namespace Repo;

				public sealed class Helper
				{
					private readonly Leaf leaf = new();
				}
				""");
			await File.WriteAllTextAsync(leafFilePath, """
				namespace Repo;

				public sealed class Leaf
				{
				}
				""");

			await RunGitAsync(repositoryPath, "add", ".");
			await RunGitAsync(repositoryPath, "commit", "-m", "Initial");

			await File.WriteAllTextAsync(classFilePath, """
				namespace Repo;

				public sealed class Class1
				{
					private readonly Helper helper = new();
					public int Value => 1;
				}
				""");

			var outputPath = Path.Combine(repositoryPath, "Repo.GitChanges.csproj");
			await RunDotNetAsync(
				"D:\\source\\RonSijm\\RonSijm.Git2Proj",
				"run",
				"--project",
				".\\src\\RonSijm.Git2Proj\\RonSijm.Git2Proj.csproj",
				"--",
				"generate",
				"--repo",
				repositoryPath,
				"--reference-depth",
				"2",
				"--output",
				outputPath,
				"--overwrite");

			var document = XDocument.Load(outputPath);
			var linkedFiles = document
				.Root!
				.Descendants()
				.Where(element => element.Name.LocalName is "Compile" or "None")
				.Select(element => element.Element("Link")!.Value)
				.ToArray();

			linkedFiles.Should().Contain("Class1.cs");
			linkedFiles.Should().Contain("Helper.cs");
			linkedFiles.Should().Contain("Leaf.cs");
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

	private static async Task RunGitAsync(string workingDirectory, params string[] arguments)
	{
		var (exitCode, _, error) = await RunProcessAsync("git", workingDirectory, arguments);
		exitCode.Should().Be(0, error);
	}

	private static async Task RunDotNetAsync(string workingDirectory, params string[] arguments)
	{
		var (exitCode, _, error) = await RunProcessAsync("dotnet", workingDirectory, arguments);
		exitCode.Should().Be(0, error);
	}

	private static async Task<(int ExitCode, string Output, string Error)> RunProcessAsync(string fileName, string workingDirectory, params string[] arguments)
	{
		var startInfo = new ProcessStartInfo(fileName)
		{
			WorkingDirectory = workingDirectory,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true,
		};

		foreach (var argument in arguments)
		{
			startInfo.ArgumentList.Add(argument);
		}

		using var process = new Process
		{
			StartInfo = startInfo,
		};

		process.Start();

		var output = await process.StandardOutput.ReadToEndAsync();
		var error = await process.StandardError.ReadToEndAsync();
		await process.WaitForExitAsync();

		return (process.ExitCode, output, error);
	}
}
