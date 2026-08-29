namespace RonSijm.Git2Proj.Git;

internal sealed class GitRepositoryInfo(string rootPath)
{
	public string RootPath { get; } = rootPath;

	public string Name { get; } = new DirectoryInfo(rootPath).Name;

	public static async Task<GitRepositoryInfo> LoadAsync(string probePath, CancellationToken cancellationToken)
	{
		var output = (await GitProcessRunner.RunOutputAsync(probePath, cancellationToken, "rev-parse", "--show-toplevel")).Trim();

		if (string.IsNullOrWhiteSpace(output))
		{
			throw new InvalidOperationException($"Could not resolve a git repository from '{probePath}'.");
		}

		return new GitRepositoryInfo(Path.GetFullPath(output));
	}
}
