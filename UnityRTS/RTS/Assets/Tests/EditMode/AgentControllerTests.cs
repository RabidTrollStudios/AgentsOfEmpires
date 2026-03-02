using NUnit.Framework;
using UnityEngine;

namespace GameManager.Tests
{
	/// <summary>
	/// EditMode tests for AgentController: covers the null-agent guard in Update().
	/// All other AgentController paths require GameManager.Instance fully wired and
	/// are covered in PlayMode (AgentControllerTests.cs).
	/// </summary>
	[TestFixture]
	public class AgentControllerTests
	{
		[Test]
		public void Update_AgentNull_DoesNotThrow()
		{
			var go = new GameObject("AgentControllerGO");
			var controller = go.AddComponent<AgentController>();
			// Agent is null by default — Update must return early before touching anything else

			Assert.DoesNotThrow(() => controller.Update(),
				"Update with Agent == null should return early without throwing");

			UnityEngine.Object.DestroyImmediate(go);
		}
	}
}
