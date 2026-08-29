using System.Diagnostics;

namespace RonSijm.Git2Proj.Git;

internal static class GitChangedFileCollector
{
	public static async Task<IReadOnlyList<string>> CollectAsync(
		string repositoryRootPath,
		string? baseRevision,
		bool includeUntrackedFiles,
		CancellationToken cancellationToken)
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

	private static async Task AddFilesAsync(
		ISet<string> changedFiles,
		string repositoryRootPath,
		CancellationToken cancellationToken,
		params string[] arguments)
	{
		foreach (var file in await RunLinesAsync(repositoryRootPath, cancellationToken, arguments))
		{
			if (!string.IsNullOrWhiteSpace(file))
			{
				changedFiles.Add(file.Trim());
			}
		}
	}

	private static async Task<IReadOnlyList<string>> RunLinesAsync(
		string repositoryRootPath,
		CancellationToken cancellationToken,
		params string[] arguments)
	{
		using var process = new Process();
		process.StartInfo = CreateStartInfo(repositoryRootPath, arguments);

		process.Start();

		var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
		var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

		await process.WaitForExitAsync(cancellationToken);

		var output = await outputTask;
		var error = await errorTask;

		if (process.ExitCode != 0)
		{
			throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed.{Environment.NewLine}{error.Trim()}".Trim());
		}

		return output
			.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
	}

	private static ProcessStartInfo CreateStartInfo(string repositoryRootPath, IEnumerable<string> arguments)
	{
		var startInfo = new ProcessStartInfo("git")
		{
			WorkingDirectory = repositoryRootPath,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true,
		};

		foreach (var argument in arguments)
		{
			startInfo.ArgumentList.Add(argument);
		}

		return startInfo;
	}
}
