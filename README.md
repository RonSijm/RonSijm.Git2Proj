# RonSijm.Git2Proj

Small .NET CLI that turns the current git working tree changes into a focused `.csproj`.

## Why

On large repositories it can be useful to open **just the files you changed** as a lightweight overview project.

## Parser choice

This uses [`CommandLineParser`](https://www.nuget.org/packages/CommandLineParser), which is one of the most widely used .NET CLI parsers by NuGet download count and fits a small verb-based tool well.

## Current behavior

- Resolves the git repository from `--repo` or the current directory
- Collects modified, staged, and optionally untracked files
- Can also generate from all changes since a base git revision with `--sha`
- Generates an SDK-style `.csproj` with linked items
- Supports:
  - `browse` mode: all files are added as `None`
  - `compile` mode: changed `.cs` files are added as `Compile`

## Usage

```powershell
dotnet run --project .\src\RonSijm.Git2Proj\RonSijm.Git2Proj.csproj -- generate
```

```powershell
dotnet run --project .\src\RonSijm.Git2Proj\RonSijm.Git2Proj.csproj -- generate --repo D:\source\SomeRepo --mode compile --overwrite
```

```powershell
dotnet run --project .\src\RonSijm.Git2Proj\RonSijm.Git2Proj.csproj -- generate --tracked-only --csharp-only
```

```powershell
dotnet run --project .\src\RonSijm.Git2Proj\RonSijm.Git2Proj.csproj -- generate --sha 123abc --mode compile --overwrite
```

With `--sha`, the tool compares the current working tree against that revision, so it includes:
- files changed in commits since that revision
- current staged and unstaged tracked changes
- untracked files unless `--tracked-only` is used

## Tool packaging

The project is configured as a .NET tool with command name `git2proj`.

Pack locally:

```powershell
dotnet pack .\src\RonSijm.Git2Proj\RonSijm.Git2Proj.csproj -c Release
```

Install from the locally packed NuGet package:

```powershell
dotnet tool install --global --add-source .\src\RonSijm.Git2Proj\bin\Release RonSijm.Git2Proj
```

Or install to a custom folder without touching your global tool list:

```powershell
dotnet tool install --tool-path .\.tools --add-source .\src\RonSijm.Git2Proj\bin\Release RonSijm.Git2Proj
```

Then run:

```powershell
git2proj generate
```

## Next likely step

Adding **reference depth**
- use **Roslyn source analysis**
-- not reflection. Reflection operates on compiled assemblies; this tool needs source/project dependency discovery.
