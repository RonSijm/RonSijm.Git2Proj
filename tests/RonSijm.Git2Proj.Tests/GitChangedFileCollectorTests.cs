using System.Diagnostics;
using RonSijm.Git2Proj.Git;

namespace RonSijm.Git2Proj.Tests;

public sealed class GitChangedFileCollectorTests
{
	[Fact]
	public async Task CollectAsync_ReturnsModifiedStagedAndUntrackedFiles()
	{
		var repositoryPath = CreateTemporaryDirectory();

		try
		{
			await RunGitAsync(repositoryPath, "init");
			await RunGitAsync(repositoryPath, "config", "user.email", "git2proj@example.test");
			await RunGitAsync(repositoryPath, "config", "user.name", "Git2Proj Tests");

			var trackedFile = Path.Combine(repositoryPath, "Tracked.cs");
			var stagedFile = Path.Combine(repositoryPath, "Staged.cs");
			var deletedFile = Path.Combine(repositoryPath, "Deleted.cs");

			await File.WriteAllTextAsync(trackedFile, "class Tracked { }\n");
			await File.WriteAllTextAsync(stagedFile, "class Staged { }\n");
			await File.WriteAllTextAsync(deletedFile, "class Deleted { }\n");

			await RunGitAsync(repositoryPath, "add", ".");
			await RunGitAsync(repositoryPath, "commit", "-m", "Initial");

			await File.WriteAllTextAsync(trackedFile, "class Tracked { int Value => 1; }\n");
			await File.WriteAllTextAsync(stagedFile, "class Staged { int Value => 2; }\n");
			await File.WriteAllTextAsync(Path.Combine(repositoryPath, "Untracked.cs"), "class Untracked { }\n");
			File.Delete(deletedFile);

			await RunGitAsync(repositoryPath, "add", "Staged.cs");
			await RunGitAsync(repositoryPath, "add", "-u");

			var files = await GitChangedFileCollector.CollectAsync(repositoryPath, baseRevision: null, includeUntrackedFiles: true, CancellationToken.None);

			Assert.Contains(files, path => path.EndsWith("Tracked.cs", StringComparison.OrdinalIgnoreCase));
			Assert.Contains(files, path => path.EndsWith("Staged.cs", StringComparison.OrdinalIgnoreCase));
			Assert.Contains(files, path => path.EndsWith("Untracked.cs", StringComparison.OrdinalIgnoreCase));
			Assert.DoesNotContain(files, path => path.EndsWith("Deleted.cs", StringComparison.OrdinalIgnoreCase));
		}
		finally
		{
			TestDirectory.Delete(repositoryPath);
		}
	}

	[Fact]
	public async Task CollectAsync_WithBaseRevision_ReturnsFilesChangedSinceRevision()
	{
		var repositoryPath = CreateTemporaryDirectory();

		try
		{
			await RunGitAsync(repositoryPath, "init");
			await RunGitAsync(repositoryPath, "config", "user.email", "git2proj@example.test");
			await RunGitAsync(repositoryPath, "config", "user.name", "Git2Proj Tests");

			var unchangedFile = Path.Combine(repositoryPath, "Unchanged.cs");
			var committedFile = Path.Combine(repositoryPath, "Committed.cs");
			var modifiedAfterBaseFile = Path.Combine(repositoryPath, "ModifiedAfterBase.cs");

			await File.WriteAllTextAsync(unchangedFile, "class Unchanged { }\n");
			await File.WriteAllTextAsync(committedFile, "class Committed { }\n");
			await File.WriteAllTextAsync(modifiedAfterBaseFile, "class ModifiedAfterBase { }\n");

			await RunGitAsync(repositoryPath, "add", ".");
			await RunGitAsync(repositoryPath, "commit", "-m", "Initial");

			var baseRevision = await RunGitForOutputAsync(repositoryPath, "rev-parse", "HEAD");

			await File.WriteAllTextAsync(committedFile, "class Committed { int Value => 1; }\n");
			await File.WriteAllTextAsync(Path.Combine(repositoryPath, "AddedInCommit.cs"), "class AddedInCommit { }\n");
			await RunGitAsync(repositoryPath, "add", ".");
			await RunGitAsync(repositoryPath, "commit", "-m", "Second");

			await File.WriteAllTextAsync(modifiedAfterBaseFile, "class ModifiedAfterBase { int Value => 2; }\n");
			await File.WriteAllTextAsync(Path.Combine(repositoryPath, "Untracked.cs"), "class Untracked { }\n");

			var files = await GitChangedFileCollector.CollectAsync(repositoryPath, baseRevision, includeUntrackedFiles: true, CancellationToken.None);

			Assert.Contains(files, path => path.EndsWith("Committed.cs", StringComparison.OrdinalIgnoreCase));
			Assert.Contains(files, path => path.EndsWith("AddedInCommit.cs", StringComparison.OrdinalIgnoreCase));
			Assert.Contains(files, path => path.EndsWith("ModifiedAfterBase.cs", StringComparison.OrdinalIgnoreCase));
			Assert.Contains(files, path => path.EndsWith("Untracked.cs", StringComparison.OrdinalIgnoreCase));
			Assert.DoesNotContain(files, path => path.EndsWith("Unchanged.cs", StringComparison.OrdinalIgnoreCase));
		}
		finally
		{
			TestDirectory.Delete(repositoryPath);
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
		var (exitCode, _, error) = await RunGitCoreAsync(workingDirectory, arguments);

		Assert.True(exitCode == 0, error);
	}

	private static async Task<string> RunGitForOutputAsync(string workingDirectory, params string[] arguments)
	{
		var (exitCode, output, error) = await RunGitCoreAsync(workingDirectory, arguments);

		Assert.True(exitCode == 0, error);
		return output.Trim();
	}

	private static async Task<(int ExitCode, string Output, string Error)> RunGitCoreAsync(string workingDirectory, params string[] arguments)
	{
		var startInfo = new ProcessStartInfo("git")
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
