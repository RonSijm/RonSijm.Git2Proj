using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;

namespace RonSijm.Git2Proj.SourceAnalysis;

internal static class ReferenceDepthExpander
{
	private static int isMsBuildRegistered;

	public static async Task<IReadOnlyList<string>> ExpandAsync(string repositoryRootPath, IReadOnlyCollection<string> changedFiles, int referenceDepth, CancellationToken cancellationToken)
	{
		if (referenceDepth <= 0)
		{
			return changedFiles
				.OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
				.ToArray();
		}

		RegisterMsBuild();

		using var workspace = MSBuildWorkspace.Create();
		var documentIndex = await LoadDocumentIndexAsync(repositoryRootPath, workspace, cancellationToken);
		var expandedFiles = new SortedSet<string>(changedFiles, StringComparer.OrdinalIgnoreCase);
		var pendingFiles = new Queue<(string FilePath, int Depth)>(
			changedFiles
				.Where(IsCSharpFile)
				.Select(Path.GetFullPath)
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.Select(filePath => (filePath, 0)));
		var visitedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		while (pendingFiles.Count > 0)
		{
			var (filePath, depth) = pendingFiles.Dequeue();

			if (!visitedFiles.Add(filePath) || depth >= referenceDepth)
			{
				continue;
			}

			if (!documentIndex.TryGetValue(filePath, out var document))
			{
				continue;
			}

			var referencedFiles = await CollectDirectReferenceFilesAsync(document, cancellationToken);

			foreach (var referencedFile in referencedFiles)
			{
				if (!IsRelevantSourceFile(repositoryRootPath, referencedFile))
				{
					continue;
				}

				expandedFiles.Add(referencedFile);
				pendingFiles.Enqueue((referencedFile, depth + 1));
			}
		}

		return expandedFiles.ToArray();
	}

	private static void RegisterMsBuild()
	{
		if (Interlocked.Exchange(ref isMsBuildRegistered, 1) == 0 && !MSBuildLocator.IsRegistered)
		{
			MSBuildLocator.RegisterDefaults();
		}
	}

	private static async Task<IReadOnlyDictionary<string, Document>> LoadDocumentIndexAsync(string repositoryRootPath, MSBuildWorkspace workspace, CancellationToken cancellationToken)
	{
		var documentIndex = new Dictionary<string, Document>(StringComparer.OrdinalIgnoreCase);
		var projectPaths = Directory
			.EnumerateFiles(repositoryRootPath, "*.csproj", SearchOption.AllDirectories)
			.Where(IsSupportedProjectPath)
			.OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
			.ToArray();

		if (projectPaths.Length == 0)
		{
			return documentIndex;
		}

		foreach (var projectPath in projectPaths)
		{
			var project = await workspace.OpenProjectAsync(projectPath, cancellationToken: cancellationToken);

			foreach (var solutionProject in project.Solution.Projects)
			{
				foreach (var document in solutionProject.Documents)
				{
					if (document.FilePath is null)
					{
						continue;
					}

					documentIndex.TryAdd(Path.GetFullPath(document.FilePath), document);
				}
			}
		}

		return documentIndex;
	}

	private static bool IsSupportedProjectPath(string projectPath)
	{
		if (projectPath.EndsWith(".GitChanges.csproj", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		var directoryParts = Path.GetDirectoryName(projectPath)?
			.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) ?? [];

		return !directoryParts.Any(part =>
			string.Equals(part, "bin", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(part, "obj", StringComparison.OrdinalIgnoreCase));
	}

	private static bool IsCSharpFile(string filePath)
	{
		return string.Equals(Path.GetExtension(filePath), ".cs", StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsRelevantSourceFile(string repositoryRootPath, string filePath)
	{
		var normalizedRepositoryRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(repositoryRootPath));
		var normalizedFilePath = Path.GetFullPath(filePath);

		return normalizedFilePath.StartsWith($"{normalizedRepositoryRoot}{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
			&& File.Exists(normalizedFilePath)
			&& IsCSharpFile(normalizedFilePath);
	}

	private static async Task<IReadOnlyCollection<string>> CollectDirectReferenceFilesAsync(Document document, CancellationToken cancellationToken)
	{
		var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken);
		var semanticModel = await document.GetSemanticModelAsync(cancellationToken);

		if (syntaxRoot is null || semanticModel is null)
		{
			return [];
		}

		var referenceFiles = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

		foreach (var typeDeclaration in syntaxRoot.DescendantNodes().OfType<TypeDeclarationSyntax>())
		{
			AddDeclaredSymbolFiles(referenceFiles, semanticModel.GetDeclaredSymbol(typeDeclaration), document.FilePath!);
		}

		foreach (var node in syntaxRoot.DescendantNodesAndSelf().Where(CanReferenceSourceSymbol))
		{
			AddSymbolFiles(referenceFiles, semanticModel.GetSymbolInfo(node), document.FilePath!);
		}

		return referenceFiles;
	}

	private static bool CanReferenceSourceSymbol(SyntaxNode node)
	{
		return node is IdentifierNameSyntax
			or GenericNameSyntax
			or QualifiedNameSyntax
			or AliasQualifiedNameSyntax
			or AttributeSyntax
			or InvocationExpressionSyntax
			or ObjectCreationExpressionSyntax
			or MemberAccessExpressionSyntax;
	}

	private static void AddDeclaredSymbolFiles(ISet<string> referenceFiles, ISymbol? symbol, string currentFilePath)
	{
		if (symbol is null)
		{
			return;
		}

		foreach (var declaringReference in symbol.DeclaringSyntaxReferences)
		{
			var filePath = declaringReference.SyntaxTree.FilePath;

			if (!string.IsNullOrWhiteSpace(filePath) &&
				!string.Equals(Path.GetFullPath(filePath), currentFilePath, StringComparison.OrdinalIgnoreCase))
			{
				referenceFiles.Add(Path.GetFullPath(filePath));
			}
		}
	}

	private static void AddSymbolFiles(ISet<string> referenceFiles, SymbolInfo symbolInfo, string currentFilePath)
	{
		foreach (var symbol in EnumerateSymbols(symbolInfo))
		{
			if (!ShouldConsiderSymbol(symbol))
			{
				continue;
			}

			foreach (var relatedSymbol in GetRelevantSymbols(symbol))
			{
				AddDeclaredSymbolFiles(referenceFiles, relatedSymbol, currentFilePath);
			}
		}
	}

	private static IEnumerable<ISymbol> EnumerateSymbols(SymbolInfo symbolInfo)
	{
		if (symbolInfo.Symbol is not null)
		{
			yield return symbolInfo.Symbol;
		}

		foreach (var candidateSymbol in symbolInfo.CandidateSymbols)
		{
			yield return candidateSymbol;
		}
	}

	private static bool ShouldConsiderSymbol(ISymbol symbol)
	{
		return symbol.Kind is SymbolKind.NamedType
			or SymbolKind.Method
			or SymbolKind.Property
			or SymbolKind.Field
			or SymbolKind.Event;
	}

	private static IEnumerable<ISymbol> GetRelevantSymbols(ISymbol symbol)
	{
		yield return symbol.OriginalDefinition;

		if (symbol is IMethodSymbol methodSymbol && methodSymbol.MethodKind == MethodKind.Constructor)
		{
			yield return methodSymbol.ContainingType;
		}

		if (symbol.ContainingType is not null)
		{
			yield return symbol.ContainingType;
		}
	}
}
