namespace RonSijm.Git2Proj.Git;

internal static class GitChangedFileCollector
{
	public static async Task<IReadOnlyList<string>> CollectAsync(string repositoryRootPath, string? baseRevision, bool includeUntrackedFiles, CancellationToken cancellationToken)
	{
		var changedFiles = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

		if (string.IsNullOrWhiteSpace(baseRevision))
		{
			await AddFilesAsync(changedFiles, repositoryRootPath, cancellationToken, "diff", "--name-only", "--diff-filter=ACMR");
			await AddFilesAsync(changedFiles, repositoryRootPath, cancellationToken, "diff", "--cached", "--name-only", "--diff-filter=ACMR");
		}
		else
		{
			await AddFilesAsync(changedFiles, repositoryRootPath, cancellationToken, "diff", "--name-only", "--diff-filter=ACMR", baseRevision.Trim());
		}

		if (includeUntrackedFiles)
		{
			await AddFilesAsync(changedFiles, repositoryRootPath, cancellationToken, "ls-files", "--others", "--exclude-standard");
		}

		return changedFiles
			.Select(path => Path.GetFullPath(Path.Combine(repositoryRootPath, path)))
			.Where(File.Exists)
			.ToArray();
	}

	private static async Task AddFilesAsync(ISet<string> changedFiles, string repositoryRootPath, CancellationToken cancellationToken, params string[] arguments)
	{
		foreach (var file in await GitProcessRunner.RunLinesAsync(repositoryRootPath, cancellationToken, arguments))
		{
			if (!string.IsNullOrWhiteSpace(file))
			{
				changedFiles.Add(file.Trim());
			}
		}
	}
}
