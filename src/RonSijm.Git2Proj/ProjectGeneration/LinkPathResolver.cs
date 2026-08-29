using RonSijm.Git2Proj.Cli;

namespace RonSijm.Git2Proj.ProjectGeneration;

internal static class LinkPathResolver
{
	public static string Resolve(string repositoryRootPath, string filePath, FolderStructureMode folderStructureMode)
	{
		var linkPath = folderStructureMode switch
		{
			FolderStructureMode.Full => Path.GetRelativePath(repositoryRootPath, filePath),
			FolderStructureMode.Project => ResolveProjectRelativePath(repositoryRootPath, filePath),
			FolderStructureMode.Flat => Path.GetFileName(filePath),
			_ => throw new InvalidOperationException($"Unsupported folder structure mode '{folderStructureMode}'."),
		};

		return linkPath;
	}

	private static string ResolveProjectRelativePath(string repositoryRootPath, string filePath)
	{
		var projectDirectory = FindNearestProjectDirectory(repositoryRootPath, filePath);
		var baseDirectory = projectDirectory ?? repositoryRootPath;
		var linkPath = Path.GetRelativePath(baseDirectory, filePath);

		return linkPath;
	}

	private static string? FindNearestProjectDirectory(string repositoryRootPath, string filePath)
	{
		var repositoryDirectory = new DirectoryInfo(Path.GetFullPath(repositoryRootPath));
		var currentDirectoryPath = Path.GetDirectoryName(Path.GetFullPath(filePath));

		while (!string.IsNullOrWhiteSpace(currentDirectoryPath))
		{
			if (ContainsProjectFile(currentDirectoryPath))
			{
				return currentDirectoryPath;
			}

			if (PathsEqual(currentDirectoryPath, repositoryDirectory.FullName))
			{
				break;
			}

			var parentDirectory = Directory.GetParent(currentDirectoryPath);
			if (parentDirectory is null)
			{
				break;
			}

			currentDirectoryPath = parentDirectory.FullName;
		}

		return null;
	}

	private static bool ContainsProjectFile(string directoryPath)
	{
		var projectFiles = Directory.EnumerateFiles(directoryPath, "*.csproj", SearchOption.TopDirectoryOnly)
			.Where(projectPath => !projectPath.EndsWith(".GitChanges.csproj", StringComparison.OrdinalIgnoreCase));

		return projectFiles.Any();
	}

	private static bool PathsEqual(string leftPath, string rightPath)
	{
		return string.Equals(
			Path.TrimEndingDirectorySeparator(leftPath),
			Path.TrimEndingDirectorySeparator(rightPath),
			StringComparison.OrdinalIgnoreCase);
	}
}
