using System.Collections;
using System.Linq;
using AgentSDK;
using GameManager.GameElements;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// Play Mode tests for pausable/resumable building construction.
	/// Verifies that build progress is stored on the building itself,
	/// survives pawn death or reassignment, and can be resumed by
	/// the same or a different pawn.
	/// </summary>
	[TestFixture]
	public class BuildResumeTests : PlayModeTestBase
	{
		/// <summary>
		/// Find the first incomplete building of the given type in the unit registry.
		/// </summary>
		private Unit FindUnbuiltBuilding(UnitType type)
		{
			return ctx.UnitManager.GetAllUnits().Values
				.Select(go => go.GetComponent<Unit>())
				.FirstOrDefault(u => u != null && u.UnitType == type && !u.IsBuilt);
		}

		#region Progress Survives Pawn Death

		/// <summary>
		/// After a pawn dies mid-construction, the incomplete building retains
		/// its accumulated build progress (BuildProgress > 0).
		/// </summary>
		[UnityTest]
		public IEnumerator Pawn_KilledDuringBuild_BuildingRetainsProgress()
		{
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(9, 10, 0));
			pawn.StartBuilding(new BuildEventArgs(pawn, new Vector3Int(10, 10, 0), UnitType.BASE));
			Assert.AreEqual(UnitAction.BUILD, pawn.CurrentAction, "Pawn should start building");

			// Wait until the building exists and has accumulated some progress
			Unit building = null;
			yield return WaitForTick(() =>
			{
				building = FindUnbuiltBuilding(UnitType.BASE);
				return building != null && building.BuildProgress > 0f;
			}, timeoutSeconds: 15f, failMessage: "Building should exist with positive progress");

			float progressBeforeDeath = building.BuildProgress;
			Assert.Greater(progressBeforeDeath, 0f, "Build progress should be positive before death");

			// Kill the pawn
			pawn.Health = 0;
			pawn.Update();
			yield return WaitFrames(2);

			// Building should retain its progress
			Assert.IsFalse(building.IsBuilt, "Building should not be complete yet");
			Assert.GreaterOrEqual(building.BuildProgress, progressBeforeDeath,
				$"Building should retain progress after pawn death (was {progressBeforeDeath:F2}s)");
		}

		#endregion

		#region Another Pawn Resumes

		/// <summary>
		/// After Pawn A abandons a build (by moving away), Pawn B can resume
		/// construction at the same position. The building completes successfully.
		/// </summary>
		[UnityTest]
		public IEnumerator AnotherPawn_CanResumeInterruptedBuild()
		{
			Vector3Int buildPos = new Vector3Int(10, 10, 0);
			Unit pawnA = PlaceUnit(UnitType.PAWN, new Vector3Int(9, 10, 0));
			pawnA.StartBuilding(new BuildEventArgs(pawnA, buildPos, UnitType.BASE));
			Assert.AreEqual(UnitAction.BUILD, pawnA.CurrentAction, "Pawn A should start building");

			// Wait for some build progress to accumulate
			Unit building = null;
			yield return WaitForTick(() =>
			{
				building = FindUnbuiltBuilding(UnitType.BASE);
				return building != null && building.BuildProgress > 0f;
			}, timeoutSeconds: 15f, failMessage: "Build progress should have started");

			float progressAtInterrupt = building.BuildProgress;

			// Pawn A moves away, abandoning the build (progress preserved on building)
			pawnA.StartMoving(new MoveEventArgs(pawnA, UnitType.PAWN, new Vector3Int(1, 1, 0)));
			Assert.AreEqual(UnitAction.MOVE, pawnA.CurrentAction, "Pawn A should switch to MOVE");

			yield return WaitFrames(3);

			// Pawn B starts building at the same position — should resume rather than reject
			Unit pawnB = PlaceUnit(UnitType.PAWN, new Vector3Int(8, 10, 0));
			pawnB.StartBuilding(new BuildEventArgs(pawnB, buildPos, UnitType.BASE));
			Assert.AreEqual(UnitAction.BUILD, pawnB.CurrentAction,
				"Pawn B should resume building the incomplete BASE");

			// Wait for the building to complete
			yield return WaitForTick(
				() => building != null && building.IsBuilt,
				timeoutSeconds: 10f,
				failMessage: "Building should complete after Pawn B resumes");

			Assert.IsTrue(building.IsBuilt, "BASE should be fully built after resume");
		}

		#endregion

		#region Same Pawn Resumes

		/// <summary>
		/// A pawn that moves away mid-build can be sent back to resume construction.
		/// Progress is preserved and the building completes.
		/// </summary>
		[UnityTest]
		public IEnumerator Pawn_InterruptedBuild_CanResumeSelf()
		{
			Vector3Int buildPos = new Vector3Int(10, 10, 0);
			Unit pawn = PlaceUnit(UnitType.PAWN, new Vector3Int(9, 10, 0));
			pawn.StartBuilding(new BuildEventArgs(pawn, buildPos, UnitType.BASE));
			Assert.AreEqual(UnitAction.BUILD, pawn.CurrentAction);

			// Wait for build progress to accumulate
			Unit building = null;
			yield return WaitForTick(() =>
			{
				building = FindUnbuiltBuilding(UnitType.BASE);
				return building != null && building.BuildProgress > 0f;
			}, timeoutSeconds: 15f, failMessage: "Build progress should have started");

			float progressAtInterrupt = building.BuildProgress;

			// Interrupt with a move — building retains its progress
			pawn.StartMoving(new MoveEventArgs(pawn, UnitType.PAWN, new Vector3Int(1, 10, 0)));
			Assert.AreEqual(UnitAction.MOVE, pawn.CurrentAction,
				"Pawn should switch from BUILD to MOVE");
			Assert.GreaterOrEqual(building.BuildProgress, progressAtInterrupt,
				"Building should retain progress after move interrupts build");

			// Wait for pawn to be idle
			yield return WaitForTick(
				() => pawn.CurrentAction == UnitAction.IDLE,
				timeoutSeconds: 15f,
				failMessage: "Pawn should become idle after reaching move target");

			// Pawn resumes the same build
			pawn.StartBuilding(new BuildEventArgs(pawn, buildPos, UnitType.BASE));
			Assert.AreEqual(UnitAction.BUILD, pawn.CurrentAction,
				"Pawn should be able to resume its own interrupted build");

			// Wait for completion
			yield return WaitForTick(
				() => building.IsBuilt,
				timeoutSeconds: 10f,
				failMessage: "Building should complete after pawn resumes");

			Assert.IsTrue(building.IsBuilt, "BASE should be fully built after self-resume");
		}

		#endregion
	}
}
