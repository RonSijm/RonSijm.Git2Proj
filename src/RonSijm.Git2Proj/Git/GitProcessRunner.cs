using System.Diagnostics;

namespace RonSijm.Git2Proj.Git;

internal static class GitProcessRunner
{
	public static async Task<string> RunOutputAsync(string repositoryRootPath, CancellationToken cancellationToken, params string[] arguments)
	{
		var (output, _) = await RunAsync(repositoryRootPath, cancellationToken, arguments);
		return output;
	}

	public static async Task<IReadOnlyList<string>> RunLinesAsync(string repositoryRootPath, CancellationToken cancellationToken, params string[] arguments)
	{
		var output = await RunOutputAsync(repositoryRootPath, cancellationToken, arguments);

		return output
			.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
	}

	private static async Task<(string Output, string Error)> RunAsync(string repositoryRootPath, CancellationToken cancellationToken, params string[] arguments)
	{
		using var process = new Process
		{
			StartInfo = CreateStartInfo(repositoryRootPath, arguments),
		};

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

		return (output, error);
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
