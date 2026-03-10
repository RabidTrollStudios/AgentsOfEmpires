using System;
using System.IO;
using NUnit.Framework;

namespace GameManager.Tests
{
	/// <summary>
	/// EditMode tests for AgentLoader.
	/// Uses a temporary directory populated with sentinel files to exercise
	/// GetDLLNamesFromDir without reading real assemblies.
	/// LoadDLL error paths are covered by passing a missing or invalid file.
	/// </summary>
	[TestFixture]
	public class AgentLoaderTests
	{
		private string tempDir;
		private AgentLoader loader;

		[SetUp]
		public void SetUp()
		{
			tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
			Directory.CreateDirectory(tempDir);
			loader = new AgentLoader(tempDir);
		}

		[TearDown]
		public void TearDown()
		{
			Directory.Delete(tempDir, recursive: true);
		}

		// ── PathToDLLs ────────────────────────────────────────────────────────────

		[Test]
		public void Constructor_StoresPathToDLLs()
		{
			Assert.AreEqual(tempDir, loader.PathToDLLs,
				"PathToDLLs should return the path passed to the constructor");
		}

		// ── GetDLLNamesFromDir ─────────────────────────────────────────────────────

		[Test]
		public void GetDLLNamesFromDir_ReturnsNonNullList()
		{
			Assert.IsNotNull(loader.GetDLLNamesFromDir(null),
				"GetDLLNamesFromDir should never return null");
		}

		[Test]
		public void GetDLLNamesFromDir_EmptyDirectory_ReturnsEmptyList()
		{
			Assert.IsEmpty(loader.GetDLLNamesFromDir(null),
				"No files in the directory should yield an empty list");
		}

		[Test]
		public void GetDLLNamesFromDir_SingleMatchingFile_ExtractsDLLName()
		{
			File.WriteAllText(Path.Combine(tempDir, "PlanningAgent_Alpha.dll"), string.Empty);

			var names = loader.GetDLLNamesFromDir(null);

			Assert.AreEqual(1, names.Count);
			Assert.AreEqual("Alpha", names[0],
				"Should extract the agent name from PlanningAgent_<name>.dll");
		}

		[Test]
		public void GetDLLNamesFromDir_MultipleMatchingFiles_ReturnsAllNames()
		{
			File.WriteAllText(Path.Combine(tempDir, "PlanningAgent_Alpha.dll"), string.Empty);
			File.WriteAllText(Path.Combine(tempDir, "PlanningAgent_Beta.dll"), string.Empty);
			File.WriteAllText(Path.Combine(tempDir, "PlanningAgent_Gamma.dll"), string.Empty);

			var names = loader.GetDLLNamesFromDir(null);

			Assert.AreEqual(3, names.Count, "All three matching DLLs should be discovered");
			CollectionAssert.Contains(names, "Alpha");
			CollectionAssert.Contains(names, "Beta");
			CollectionAssert.Contains(names, "Gamma");
		}

		[Test]
		public void GetDLLNamesFromDir_ExcludesMine()
		{
			File.WriteAllText(Path.Combine(tempDir, "PlanningAgent_Mine.dll"), string.Empty);
			File.WriteAllText(Path.Combine(tempDir, "PlanningAgent_Alpha.dll"), string.Empty);

			var names = loader.GetDLLNamesFromDir(null);

			CollectionAssert.DoesNotContain(names, "Mine",
				"PlanningAgent_Mine.dll should be excluded from results");
			CollectionAssert.Contains(names, "Alpha");
		}

		[Test]
		public void GetDLLNamesFromDir_NonMatchingFilenames_AreIgnored()
		{
			File.WriteAllText(Path.Combine(tempDir, "readme.txt"), "not a dll");
			File.WriteAllText(Path.Combine(tempDir, "SomeLib.dll"), string.Empty);
			File.WriteAllText(Path.Combine(tempDir, "PlanningAgent_Alpha.dll"), string.Empty);

			var names = loader.GetDLLNamesFromDir(null);

			Assert.AreEqual(1, names.Count,
				"Only PlanningAgent_*.dll files (excluding Mine) should be returned");
			Assert.AreEqual("Alpha", names[0]);
		}

		[Test]
		public void GetDLLNamesFromDir_OnlyMine_ReturnsEmptyList()
		{
			File.WriteAllText(Path.Combine(tempDir, "PlanningAgent_Mine.dll"), string.Empty);

			Assert.IsEmpty(loader.GetDLLNamesFromDir(null),
				"A directory containing only PlanningAgent_Mine.dll should yield an empty list");
		}

		// ── LoadDLL ────────────────────────────────────────────────────────────────

		[Test]
		public void LoadDLL_MissingFile_ThrowsFileNotFoundException()
		{
			Assert.Throws<FileNotFoundException>(
				() => loader.LoadDLL("Blue", "NonExistent", null),
				"LoadDLL should throw when the DLL file does not exist");
		}

		[Test]
		public void LoadDLL_InvalidFileContent_ThrowsBadImageFormatException()
		{
			File.WriteAllText(
				Path.Combine(tempDir, "PlanningAgent_Fake.dll"),
				"not a valid dotnet assembly");

			Assert.Throws<BadImageFormatException>(
				() => loader.LoadDLL("Blue", "Fake", null),
				"LoadDLL should throw when the file is not a valid .NET assembly");
		}
	}
}
