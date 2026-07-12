using System.Collections;
using AgentSDK;
using GameManager.GameElements;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// PlayMode tests for Unit capability query methods:
	/// ChangeColor, CanTrainUnit, CanBuildUnit, and CenterGridPosition.
	/// </summary>
	[TestFixture]
	public class UnitCapabilityQueryTests : PlayModeTestBase
	{
		// ── ChangeColor ────────────────────────────────────────────────────────

		[UnityTest]
		public IEnumerator ChangeColor_UpdatesColorProperty()
		{
			var unit = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));

			unit.ChangeColor(Color.red);

			Assert.AreEqual(Color.red, unit.Color,
				"ChangeColor should update the Color property");
			yield return null;
		}

		// ── CanTrainUnit ───────────────────────────────────────────────────────

		[UnityTest]
		public IEnumerator CanTrainUnit_BarracksWarrior_ReturnsTrue()
		{
			var barracks = PlaceUnit(UnitType.BARRACKS, new Vector3Int(5, 5, 0));

			Assert.IsTrue(barracks.CanTrainUnit(UnitType.WARRIOR),
				"BARRACKS should be able to train WARRIOR");
			yield return null;
		}

		[UnityTest]
		public IEnumerator CanTrainUnit_BarracksPawn_ReturnsFalse()
		{
			var barracks = PlaceUnit(UnitType.BARRACKS, new Vector3Int(5, 5, 0));

			Assert.IsFalse(barracks.CanTrainUnit(UnitType.PAWN),
				"BARRACKS should not be able to train PAWN");
			yield return null;
		}

		// ── CanBuildUnit ───────────────────────────────────────────────────────

		[UnityTest]
		public IEnumerator CanBuildUnit_PawnBase_ReturnsTrue()
		{
			var pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));

			Assert.IsTrue(pawn.CanBuildUnit(UnitType.BASE),
				"PAWN should be able to build BASE");
			yield return null;
		}

		[UnityTest]
		public IEnumerator CanBuildUnit_PawnWarrior_ReturnsFalse()
		{
			var pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(5, 5, 0));

			Assert.IsFalse(pawn.CanBuildUnit(UnitType.WARRIOR),
				"PAWN should not be able to build WARRIOR");
			yield return null;
		}

		// ── CenterGridPosition ─────────────────────────────────────────────────

		[UnityTest]
		public IEnumerator CenterGridPosition_1x1Unit_EqualsGridPosition()
		{
			var pos    = new Vector3Int(10, 10, 0);
			var pawn = PlaceUnit(UnitType.PAWN, pos);

			Assert.AreEqual(pos, pawn.CenterGridPosition,
				"1×1 unit CenterGridPosition should equal GridPosition");
			yield return null;
		}

		[UnityTest]
		public IEnumerator CenterGridPosition_3x3Building_IsOffsetByOne()
		{
			var pos      = new Vector3Int(5, 15, 0);
			var barracks = PlaceUnit(UnitType.BARRACKS, pos);
			// Footprint extends UP from the bottom-left anchor; the top row is a walkable
			// passage, so the body center of a 3x3 building is anchor + (1, +1).
			var expected = new Vector3Int(pos.x + 1, pos.y + 1, 0);

			Assert.AreEqual(expected, barracks.CenterGridPosition,
				"3×3 building CenterGridPosition should be GridPosition + (1,+1)");
			yield return null;
		}
	}
}
