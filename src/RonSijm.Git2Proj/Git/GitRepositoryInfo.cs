using System.Diagnostics;

namespace RonSijm.Git2Proj.Git;

internal sealed class GitRepositoryInfo
{
	private GitRepositoryInfo(string rootPath)
	{
		RootPath = rootPath;
		Name = new DirectoryInfo(rootPath).Name;
	}

	public string RootPath { get; }

	public string Name { get; }

	public static async Task<GitRepositoryInfo> LoadAsync(string probePath, CancellationToken cancellationToken)
	{
		var startInfo = new ProcessStartInfo("git")
		{
			WorkingDirectory = probePath,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true,
		};

		startInfo.ArgumentList.Add("rev-parse");
		startInfo.ArgumentList.Add("--show-toplevel");

		using var process = new Process
		{
			StartInfo = startInfo,
		};

		process.Start();

		var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
		var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

		await process.WaitForExitAsync(cancellationToken);

		var output = (await outputTask).Trim();
		var error = (await errorTask).Trim();

		if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
		{
			throw new InvalidOperationException($"Could not resolve a git repository from '{probePath}'.{Environment.NewLine}{error}".Trim());
		}

		return new GitRepositoryInfo(Path.GetFullPath(output));
	}
}
