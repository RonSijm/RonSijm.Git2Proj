using RonSijm.Git2Proj.Git;
using RonSijm.Git2Proj.ProjectGeneration;
using RonSijm.Git2Proj.SourceAnalysis;

namespace RonSijm.Git2Proj.Cli;

internal static class GenerateProjectCommand
{
	public static async Task<int> RunAsync(GenerateOptions options, CancellationToken cancellationToken)
	{
		try
		{
			var repositoryProbePath = options.RepositoryPath is null
				? Environment.CurrentDirectory
				: Path.GetFullPath(options.RepositoryPath);

			var repository = await GitRepositoryInfo.LoadAsync(repositoryProbePath, cancellationToken);
			var outputPath = ResolveOutputPath(options, repository);

			if (File.Exists(outputPath) && !options.Overwrite)
			{
				Console.Error.WriteLine($"Output file already exists: {outputPath}");
				Console.Error.WriteLine("Use --overwrite to replace it.");
				return 1;
			}

			if (options.ReferenceDepth < 0)
			{
				Console.Error.WriteLine("--reference-depth must be 0 or greater.");
				return 1;
			}

			var changedFiles = await GitChangedFileCollector.CollectAsync(
				repository.RootPath,
				options.BaseRevision,
				includeUntrackedFiles: !options.TrackedOnly,
				cancellationToken);

			var filteredFiles = changedFiles
				.Where(path => !string.Equals(path, outputPath, StringComparison.OrdinalIgnoreCase))
				.Where(path => !options.CSharpOnly || string.Equals(Path.GetExtension(path), ".cs", StringComparison.OrdinalIgnoreCase))
				.ToArray();

			var filesToInclude = await ReferenceDepthExpander.ExpandAsync(
				repository.RootPath,
				filteredFiles,
				options.ReferenceDepth,
				cancellationToken);

			var projectName = string.IsNullOrWhiteSpace(options.ProjectName)
				? $"{repository.Name}.GitChanges"
				: options.ProjectName.Trim();

			ProjectFileWriter.Write(outputPath, projectName, repository.RootPath, filesToInclude, options.Mode, options.FolderStructure);

			Console.WriteLine($"Generated {outputPath}");
			Console.WriteLine($"Included {filesToInclude.Count} file(s) from {repository.RootPath}");

			if (options.ReferenceDepth > 0)
			{
				Console.WriteLine($"Added {filesToInclude.Count - filteredFiles.Length} referenced file(s) using depth {options.ReferenceDepth}");
			}

			return 0;
		}
		catch (Exception exception)
		{
			Console.Error.WriteLine(exception.Message);
			return 1;
		}
	}

	private static string ResolveOutputPath(GenerateOptions options, GitRepositoryInfo repository)
	{
		if (!string.IsNullOrWhiteSpace(options.OutputPath))
		{
			return Path.GetFullPath(options.OutputPath);
		}

		return Path.Combine(repository.RootPath, $"{repository.Name}.GitChanges.csproj");
	}
}
