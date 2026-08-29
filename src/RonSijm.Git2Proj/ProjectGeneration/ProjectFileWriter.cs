using System.Xml;
using System.Xml.Linq;
using RonSijm.Git2Proj.Cli;

namespace RonSijm.Git2Proj.ProjectGeneration;

internal static class ProjectFileWriter
{
	public static void Write(string outputPath, string projectName, string repositoryRootPath, IReadOnlyCollection<string> files, ProjectItemMode mode)
	{
		var outputDirectory = Path.GetDirectoryName(outputPath);
		if (string.IsNullOrWhiteSpace(outputDirectory))
		{
			throw new InvalidOperationException($"Could not determine the output directory for '{outputPath}'.");
		}

		Directory.CreateDirectory(outputDirectory);

		var document = new XDocument(
			new XDeclaration("1.0", "utf-8", "yes"),
			new XElement(
				"Project",
				new XAttribute("Sdk", "Microsoft.NET.Sdk"),
				CreateProperties(projectName),
				CreateItems(outputDirectory, repositoryRootPath, files, mode)));

		var settings = new XmlWriterSettings
		{
			Indent = true,
			IndentChars = "  ",
			NewLineChars = "\r\n",
			NewLineHandling = NewLineHandling.Replace,
		};

		using var writer = XmlWriter.Create(outputPath, settings);
		document.Save(writer);
	}

	private static XElement CreateProperties(string projectName)
	{
		return new XElement(
			"PropertyGroup",
			new XElement("TargetFramework", "net10.0"),
			new XElement("ImplicitUsings", "enable"),
			new XElement("Nullable", "enable"),
			new XElement("EnableDefaultItems", "false"),
			new XElement("AssemblyName", projectName),
			new XElement("RootNamespace", SanitizeIdentifier(projectName)));
	}

	private static IEnumerable<XElement> CreateItems(string outputDirectory, string repositoryRootPath, IEnumerable<string> files, ProjectItemMode mode)
	{
		var items = files
			.OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
			.Select(file => CreateItem(outputDirectory, repositoryRootPath, file, mode))
			.ToArray();

		if (items.Length == 0)
		{
			yield break;
		}

		yield return new XElement("ItemGroup", items);
	}

	private static XElement CreateItem(string outputDirectory, string repositoryRootPath, string filePath, ProjectItemMode mode)
	{
		var itemName = ResolveItemName(filePath, mode);
		var includePath = Path.GetRelativePath(outputDirectory, filePath);
		var linkPath = Path.GetRelativePath(repositoryRootPath, filePath);

		return new XElement(
			itemName,
			new XAttribute("Include", includePath),
			new XElement("Link", linkPath));
	}

	private static string ResolveItemName(string filePath, ProjectItemMode mode)
	{
		if (mode == ProjectItemMode.Compile &&
			string.Equals(Path.GetExtension(filePath), ".cs", StringComparison.OrdinalIgnoreCase))
		{
			return "Compile";
		}

		return "None";
	}

	private static string SanitizeIdentifier(string value)
	{
		var characters = value
			.Select(character => char.IsLetterOrDigit(character) || character == '_' ? character : '_')
			.ToArray();

		if (characters.Length == 0)
		{
			return "GitChanges";
		}

		if (!char.IsLetter(characters[0]) && characters[0] != '_')
		{
			return $"_{new string(characters)}";
		}

		return new string(characters);
	}
}
