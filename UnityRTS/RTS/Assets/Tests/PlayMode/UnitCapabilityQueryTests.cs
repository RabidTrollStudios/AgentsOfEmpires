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
			var unit = PlaceUnit(UnitType.WORKER, new Vector3Int(5, 5, 0));

			unit.ChangeColor(Color.red);

			Assert.AreEqual(Color.red, unit.Color,
				"ChangeColor should update the Color property");
			yield return null;
		}

		// ── CanTrainUnit ───────────────────────────────────────────────────────

		[UnityTest]
		public IEnumerator CanTrainUnit_BarracksSoldier_ReturnsTrue()
		{
			var barracks = PlaceUnit(UnitType.BARRACKS, new Vector3Int(5, 5, 0));

			Assert.IsTrue(barracks.CanTrainUnit(UnitType.SOLDIER),
				"BARRACKS should be able to train SOLDIER");
			yield return null;
		}

		[UnityTest]
		public IEnumerator CanTrainUnit_BarracksWorker_ReturnsFalse()
		{
			var barracks = PlaceUnit(UnitType.BARRACKS, new Vector3Int(5, 5, 0));

			Assert.IsFalse(barracks.CanTrainUnit(UnitType.WORKER),
				"BARRACKS should not be able to train WORKER");
			yield return null;
		}

		// ── CanBuildUnit ───────────────────────────────────────────────────────

		[UnityTest]
		public IEnumerator CanBuildUnit_WorkerBase_ReturnsTrue()
		{
			var worker = PlaceUnit(UnitType.WORKER, new Vector3Int(5, 5, 0));

			Assert.IsTrue(worker.CanBuildUnit(UnitType.BASE),
				"WORKER should be able to build BASE");
			yield return null;
		}

		[UnityTest]
		public IEnumerator CanBuildUnit_WorkerSoldier_ReturnsFalse()
		{
			var worker = PlaceUnit(UnitType.WORKER, new Vector3Int(5, 5, 0));

			Assert.IsFalse(worker.CanBuildUnit(UnitType.SOLDIER),
				"WORKER should not be able to build SOLDIER");
			yield return null;
		}

		// ── CenterGridPosition ─────────────────────────────────────────────────

		[UnityTest]
		public IEnumerator CenterGridPosition_1x1Unit_EqualsGridPosition()
		{
			var pos    = new Vector3Int(10, 10, 0);
			var worker = PlaceUnit(UnitType.WORKER, pos);

			Assert.AreEqual(pos, worker.CenterGridPosition,
				"1×1 unit CenterGridPosition should equal GridPosition");
			yield return null;
		}

		[UnityTest]
		public IEnumerator CenterGridPosition_3x3Building_IsOffsetByOne()
		{
			var pos      = new Vector3Int(5, 15, 0);
			var baseUnit = PlaceUnit(UnitType.BASE, pos);
			var expected = new Vector3Int(pos.x + 1, pos.y - 1, 0);

			Assert.AreEqual(expected, baseUnit.CenterGridPosition,
				"3×3 building CenterGridPosition should be GridPosition + (1,−1)");
			yield return null;
		}
	}
}
