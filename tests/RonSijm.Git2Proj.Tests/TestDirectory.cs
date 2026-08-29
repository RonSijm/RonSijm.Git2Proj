namespace RonSijm.Git2Proj.Tests;

internal static class TestDirectory
{
	public static void Delete(string path)
	{
		if (!Directory.Exists(path))
		{
			return;
		}

		foreach (var directory in Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories))
		{
			File.SetAttributes(directory, FileAttributes.Normal);
		}

		foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
		{
			File.SetAttributes(file, FileAttributes.Normal);
		}

		File.SetAttributes(path, FileAttributes.Normal);
		Directory.Delete(path, recursive: true);
	}
}
