using System.Collections;
using AgentSDK;
using GameManager.GameElements;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// Play Mode tests for Unit constant-derived property getters
	/// that are uncovered by other tests: Cost, CreationTime,
	/// Dependencies, and Velocity.
	/// </summary>
	[TestFixture]
	public class UnitPropertyTests : PlayModeTestBase
	{
		#region Cost Property

		[UnityTest]
		public IEnumerator PawnCost_MatchesConstant()
		{
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			yield return null;

			Assert.AreEqual(Constants.COST[UnitType.PAWN], pawn.Cost,
				"Pawn.Cost should match Constants.COST[PAWN]");
		}

		[UnityTest]
		public IEnumerator WarriorCost_MatchesConstant()
		{
			Unit warrior = PlaceUnit(UnitType.WARRIOR, new Vector3Int(5, 5, 0));
			yield return null;

			Assert.AreEqual(Constants.COST[UnitType.WARRIOR], warrior.Cost,
				"Warrior.Cost should match Constants.COST[WARRIOR]");
		}

		#endregion

		#region CreationTime Property

		[UnityTest]
		public IEnumerator PawnCreationTime_MatchesConstant()
		{
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			yield return null;

			Assert.AreEqual(Constants.CREATION_TIME[UnitType.PAWN], pawn.CreationTime,
				"Pawn.CreationTime should match Constants.CREATION_TIME[PAWN]");
		}

		[UnityTest]
		public IEnumerator BaseCreationTime_MatchesConstant()
		{
			Unit baseUnit = PlaceUnit(UnitType.BASE, new Vector3Int(5, 5, 0));
			yield return null;

			Assert.AreEqual(Constants.CREATION_TIME[UnitType.BASE], baseUnit.CreationTime,
				"Base.CreationTime should match Constants.CREATION_TIME[BASE]");
		}

		#endregion

		#region Dependencies Property

		[UnityTest]
		public IEnumerator PawnDependencies_IsEmpty()
		{
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			yield return null;

			Assert.IsNotNull(pawn.Dependencies);
			Assert.AreEqual(Constants.DEPENDENCY[UnitType.PAWN].Count, pawn.Dependencies.Count);
		}

		[UnityTest]
		public IEnumerator BarracksDependencies_ContainsBase()
		{
			Unit barracks = PlaceUnit(UnitType.BARRACKS, new Vector3Int(5, 5, 0));
			yield return null;

			Assert.IsNotNull(barracks.Dependencies);
			Assert.Contains(UnitType.BASE, barracks.Dependencies,
				"Barracks should depend on BASE");
		}

		#endregion

		#region Velocity Property

		[UnityTest]
		public IEnumerator Velocity_CanBeSetAndRead()
		{
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			yield return null;

			Vector3 vel = new Vector3(1f, 0.5f, 0f);
			pawn.Velocity = vel;

			Assert.AreEqual(vel, pawn.Velocity,
				"Velocity getter should return what was set");
		}

		[UnityTest]
		public IEnumerator Velocity_DefaultIsZero()
		{
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));
			yield return null;

			// Velocity is not explicitly initialized in Initialize — check initial value
			// (it uses the default Vector3 which is zero)
			Assert.AreEqual(Vector3.zero, pawn.Velocity);
		}

		#endregion
	}
}
