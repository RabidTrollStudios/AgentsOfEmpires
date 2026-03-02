using System.Collections.Generic;
using System.Reflection;
using AgentSDK;
using AgentSDK;
using NUnit.Framework;
using UnityEngine;

namespace GameManager.Tests
{
	/// <summary>
	/// EditMode tests for GameManager core methods:
	/// - GetEnemyAgentNbrs
	/// - GetAgent
	/// - DetermineRoundsCompleted
	/// - IsInCorner
	/// - FindMirroredLocation
	/// All require private state injection via reflection.
	/// </summary>
	[TestFixture]
	public class GameManagerCoreTests
	{
		private GameManager gm;
		private List<GameObject> createdObjects;

		[SetUp]
		public void SetUp()
		{
			gm = GameManager.Instance;
			createdObjects = new List<GameObject>();
		}

		[TearDown]
		public void TearDown()
		{
			// Clear injected private state to avoid test contamination
			SetProp("Agents", null);
			SetProp("AgentWins", null);
			SetField("mapManager", null);

			foreach (var go in createdObjects)
				if (go != null) Object.DestroyImmediate(go);
			createdObjects.Clear();
		}

		// ── Reflection helpers ────────────────────────────────────────────────

		private void SetProp(string name, object value)
		{
			typeof(GameManager)
				.GetProperty(name, BindingFlags.NonPublic | BindingFlags.Instance)
				.SetValue(gm, value);
		}

		private void SetField(string name, object value)
		{
			typeof(GameManager)
				.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
				.SetValue(gm, value);
		}

		private T Invoke<T>(string methodName, params object[] args)
		{
			return (T)typeof(GameManager)
				.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)
				.Invoke(gm, args.Length == 0 ? null : args);
		}

		private MapManager MakeMapManager(int width, int height)
		{
			var mm = new MapManager();
			typeof(MapManager)
				.GetProperty("MapSize", BindingFlags.Public | BindingFlags.Instance)
				.SetValue(mm, new Vector3Int(width, height, 0));
			return mm;
		}

		private GameObject MakeAgentGo(int agentNbr)
		{
			var go = new GameObject("TestAgent" + agentNbr);
			createdObjects.Add(go);

			var bridge = go.AddComponent<AgentBridge>();
			bridge.InitializeAgent("Human", "TestDLL", agentNbr, ".");

			var controller = go.AddComponent<AgentController>();
			typeof(AgentController)
				.GetField("Agent", BindingFlags.NonPublic | BindingFlags.Instance)
				.SetValue(controller, bridge);

			return go;
		}

		// ── GetEnemyAgentNbrs ─────────────────────────────────────────────────

		[Test]
		public void GetEnemyAgentNbrs_TwoAgents_Agent0_ReturnsAgent1()
		{
			var go0 = new GameObject("A0"); createdObjects.Add(go0);
			var go1 = new GameObject("A1"); createdObjects.Add(go1);
			SetProp("Agents", new Dictionary<int, GameObject> { { 0, go0 }, { 1, go1 } });

			var enemies = gm.GetEnemyAgentNbrs(0);

			Assert.AreEqual(1, enemies.Count);
			Assert.AreEqual(1, enemies[0]);
		}

		[Test]
		public void GetEnemyAgentNbrs_TwoAgents_Agent1_ReturnsAgent0()
		{
			var go0 = new GameObject("A0"); createdObjects.Add(go0);
			var go1 = new GameObject("A1"); createdObjects.Add(go1);
			SetProp("Agents", new Dictionary<int, GameObject> { { 0, go0 }, { 1, go1 } });

			var enemies = gm.GetEnemyAgentNbrs(1);

			Assert.AreEqual(1, enemies.Count);
			Assert.AreEqual(0, enemies[0]);
		}

		[Test]
		public void GetEnemyAgentNbrs_SingleAgent_ReturnsEmpty()
		{
			var go0 = new GameObject("A0"); createdObjects.Add(go0);
			SetProp("Agents", new Dictionary<int, GameObject> { { 0, go0 } });

			var enemies = gm.GetEnemyAgentNbrs(0);

			Assert.IsEmpty(enemies);
		}

		// ── GetAgent ──────────────────────────────────────────────────────────

		[Test]
		public void GetAgent_ReturnsAgentForAgentNbr()
		{
			var agentGo = MakeAgentGo(0);
			var expectedAgent = agentGo.GetComponent<AgentBridge>();
			SetProp("Agents", new Dictionary<int, GameObject> { { 0, agentGo } });

			var result = gm.GetAgent(0);

			Assert.IsNotNull(result);
			Assert.AreEqual(expectedAgent, result);
		}

		// ── DetermineRoundsCompleted ──────────────────────────────────────────

		[Test]
		public void DetermineRoundsCompleted_EmptyDictionary_ReturnsZero()
		{
			SetProp("AgentWins", new Dictionary<string, int>());

			int result = Invoke<int>("DetermineRoundsCompleted");

			Assert.AreEqual(0, result);
		}

		[Test]
		public void DetermineRoundsCompleted_SumsAllWins()
		{
			SetProp("AgentWins", new Dictionary<string, int>
			{
				{ Constants.HUMAN_ABBR, 2 },
				{ Constants.ORC_ABBR,   1 }
			});

			int result = Invoke<int>("DetermineRoundsCompleted");

			Assert.AreEqual(3, result);
		}

		[Test]
		public void DetermineRoundsCompleted_AllZero_ReturnsZero()
		{
			SetProp("AgentWins", new Dictionary<string, int>
			{
				{ Constants.HUMAN_ABBR, 0 },
				{ Constants.ORC_ABBR,   0 }
			});

			int result = Invoke<int>("DetermineRoundsCompleted");

			Assert.AreEqual(0, result);
		}

		// ── IsInCorner ────────────────────────────────────────────────────────

		[Test]
		public void IsInCorner_LowerLeftCorner_ReturnsTrue()
		{
			// 30x30 map, margin=5: x<5 AND y<5
			SetField("mapManager", MakeMapManager(30, 30));

			bool result = Invoke<bool>("IsInCorner", new Vector3Int(2, 2, 0), 5);

			Assert.IsTrue(result);
		}

		[Test]
		public void IsInCorner_UpperRightCorner_ReturnsTrue()
		{
			// 30x30 map, margin=5: x>=25 AND y>=25
			SetField("mapManager", MakeMapManager(30, 30));

			bool result = Invoke<bool>("IsInCorner", new Vector3Int(27, 27, 0), 5);

			Assert.IsTrue(result);
		}

		[Test]
		public void IsInCorner_UpperLeftCorner_ReturnsFalse()
		{
			// Near left AND near top but not a game corner (only lower-left and upper-right)
			SetField("mapManager", MakeMapManager(30, 30));

			bool result = Invoke<bool>("IsInCorner", new Vector3Int(2, 27, 0), 5);

			Assert.IsFalse(result);
		}

		[Test]
		public void IsInCorner_LowerRightCorner_ReturnsFalse()
		{
			// Near right AND near bottom but not a game corner
			SetField("mapManager", MakeMapManager(30, 30));

			bool result = Invoke<bool>("IsInCorner", new Vector3Int(27, 2, 0), 5);

			Assert.IsFalse(result);
		}

		[Test]
		public void IsInCorner_Center_ReturnsFalse()
		{
			SetField("mapManager", MakeMapManager(30, 30));

			bool result = Invoke<bool>("IsInCorner", new Vector3Int(15, 15, 0), 5);

			Assert.IsFalse(result);
		}

		// ── FindMirroredLocation ──────────────────────────────────────────────

		[Test]
		public void FindMirroredLocation_Worker_MirrorsCorrectly()
		{
			// mapSize=(30,30), UNIT_SIZE[WORKER]=(1,1), pos=(5,5)
			// mirror.x = 30 - 1 - 5 = 24
			// mirror.y = 30 - 2 + 1 - 5 = 24
			SetField("mapManager", MakeMapManager(30, 30));

			var result = Invoke<Vector3Int>(
				"FindMirroredLocation",
				new Vector3Int(5, 5, 0), UnitType.WORKER);

			Assert.AreEqual(new Vector3Int(24, 24, 0), result);
		}

		[Test]
		public void FindMirroredLocation_OriginWorker_ReturnsMapEdge()
		{
			// pos=(0,0): mirror.x = 30 - 1 - 0 = 29, mirror.y = 30 - 2 + 1 - 0 = 29
			SetField("mapManager", MakeMapManager(30, 30));

			var result = Invoke<Vector3Int>(
				"FindMirroredLocation",
				new Vector3Int(0, 0, 0), UnitType.WORKER);

			Assert.AreEqual(new Vector3Int(29, 29, 0), result);
		}
	}
}
