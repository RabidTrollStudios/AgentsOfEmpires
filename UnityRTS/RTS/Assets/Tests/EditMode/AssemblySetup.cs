using NUnit.Framework;
using UnityEngine;

namespace GameManager.Tests
{
	/// <summary>
	/// Creates a minimal GameManager singleton before any test runs.
	/// Needed because Constants.HEALTH static initializer reads GameManager.Instance.StartingMineGold.
	/// In EditMode, Awake() is NOT called — only the constructor runs, which sets the singleton.
	/// StartingMineGold defaults to 10000 via its field initializer.
	/// </summary>
	[SetUpFixture]
	public class AssemblySetup
	{
		private GameObject gmObject;

		[OneTimeSetUp]
		public void CreateGameManagerSingleton()
		{
			if (GameManager.Instance == null)
			{
				gmObject = new GameObject("TestGM");
				gmObject.AddComponent<GameManager>();
			}
		}

		[OneTimeTearDown]
		public void DestroyGameManagerSingleton()
		{
			if (gmObject != null)
			{
				Object.DestroyImmediate(gmObject);
			}
		}
	}
}
