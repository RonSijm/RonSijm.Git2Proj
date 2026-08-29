using CommandLine;

namespace RonSijm.Git2Proj.Cli;

[Verb("generate", HelpText = "Generate a focused .csproj from the current git working tree changes.")]
internal sealed class GenerateOptions
{
	[Option('s', "sha", HelpText = "Use a base commit, tag, or other git revision and include all current changes since that revision.")]
	public string? BaseRevision { get; set; }

	[Option('r', "repo", HelpText = "A path inside the git repository to inspect. Defaults to the current directory.")]
	public string? RepositoryPath { get; set; }

	[Option('o', "output", HelpText = "The .csproj path to write. Defaults to <repo-name>.GitChanges.csproj in the repository root.")]
	public string? OutputPath { get; set; }

	[Option('n', "name", HelpText = "The generated project name. Defaults to <repo-name>.GitChanges.")]
	public string? ProjectName { get; set; }

	[Option("reference-depth", Default = 0, HelpText = "Recursively include referenced C# source files up to the specified depth.")]
	public int ReferenceDepth { get; set; }

	[Option("mode", Default = ProjectItemMode.Browse, HelpText = "Browse keeps all changed files as linked None items. Compile adds changed .cs files as Compile items.")]
	public ProjectItemMode Mode { get; set; }

	[Option("tracked-only", Default = false, HelpText = "Exclude untracked files from the generated project.")]
	public bool TrackedOnly { get; set; }

	[Option("csharp-only", Default = false, HelpText = "Only include changed .cs files.")]
	public bool CSharpOnly { get; set; }

	[Option("overwrite", Default = false, HelpText = "Overwrite the output file if it already exists.")]
	public bool Overwrite { get; set; }
}
