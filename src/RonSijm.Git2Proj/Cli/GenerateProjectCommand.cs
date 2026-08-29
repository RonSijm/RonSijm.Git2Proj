using RonSijm.Git2Proj.Git;
using RonSijm.Git2Proj.ProjectGeneration;

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

			var changedFiles = await GitChangedFileCollector.CollectAsync(
				repository.RootPath,
				options.BaseRevision,
				includeUntrackedFiles: !options.TrackedOnly,
				cancellationToken);

			var filteredFiles = changedFiles
				.Where(path => !string.Equals(path, outputPath, StringComparison.OrdinalIgnoreCase))
				.Where(path => !options.CSharpOnly || string.Equals(Path.GetExtension(path), ".cs", StringComparison.OrdinalIgnoreCase))
				.ToArray();

			var projectName = string.IsNullOrWhiteSpace(options.ProjectName)
				? $"{repository.Name}.GitChanges"
				: options.ProjectName.Trim();

			ProjectFileWriter.Write(outputPath, projectName, repository.RootPath, filteredFiles, options.Mode);

			Console.WriteLine($"Generated {outputPath}");
			Console.WriteLine($"Included {filteredFiles.Length} changed file(s) from {repository.RootPath}");

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
