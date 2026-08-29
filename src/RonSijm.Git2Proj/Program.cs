using CommandLine;
using RonSijm.Git2Proj.Cli;

return await Parser.Default
	.ParseArguments<GenerateOptions>(args)
	.MapResult(
		options => GenerateProjectCommand.RunAsync(options, CancellationToken.None),
		_ => Task.FromResult(1));
