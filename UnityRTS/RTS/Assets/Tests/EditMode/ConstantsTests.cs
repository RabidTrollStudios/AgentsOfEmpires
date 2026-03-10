using NUnit.Framework;
using AgentSDK;

namespace GameManager.Tests
{
	[TestFixture]
	public class ConstantsTests
	{
		[SetUp]
		public void SetUp()
		{
			// Ensure constants are calculated at speed 1 unless overridden by specific test
			Constants.GAME_SPEED = 1;
			Constants.CalculateGameConstants();
		}

		[Test]
		public void Speed1_PawnMovingSpeed()
		{
			Assert.AreEqual(0.1f, Constants.MOVING_SPEED[UnitType.PAWN], 0.001f);
		}

		[Test]
		public void Speed5_PawnMovingSpeedScales()
		{
			Constants.GAME_SPEED = 5;
			Constants.CalculateGameConstants();
			Assert.AreEqual(0.5f, Constants.MOVING_SPEED[UnitType.PAWN], 0.001f);
		}

		[Test]
		public void MaxSpeed_CreationTimeInverse()
		{
			Constants.GAME_SPEED = 30;
			Constants.CalculateGameConstants();
			// SCALAR_CREATION_TIME = 1/30, PAWN creation = SCALAR * 2
			float expected = (1f / 30f) * 2f;
			Assert.AreEqual(expected, Constants.CREATION_TIME[UnitType.PAWN], 0.001f);
		}

		[Test]
		public void Speed1_WarriorDamage()
		{
			Assert.AreEqual(48.0f, Constants.DAMAGE[UnitType.WARRIOR], 0.001f);
		}

		[Test]
		public void Speed3_WarriorDamageScales()
		{
			Constants.GAME_SPEED = 3;
			Constants.CalculateGameConstants();
			Assert.AreEqual(144.0f, Constants.DAMAGE[UnitType.WARRIOR], 0.001f);
		}

		[Test]
		public void ImmobileUnits_ZeroSpeed()
		{
			Assert.AreEqual(0.0f, Constants.MOVING_SPEED[UnitType.MINE]);
			Assert.AreEqual(0.0f, Constants.MOVING_SPEED[UnitType.BASE]);
			Assert.AreEqual(0.0f, Constants.MOVING_SPEED[UnitType.BARRACKS]);
			Assert.AreEqual(0.0f, Constants.MOVING_SPEED[UnitType.ARCHERY]);
		}

		[Test]
		public void NonCombatants_ZeroDamage()
		{
			Assert.AreEqual(0.0f, Constants.DAMAGE[UnitType.MINE]);
			Assert.AreEqual(0.0f, Constants.DAMAGE[UnitType.PAWN]);
			Assert.AreEqual(0.0f, Constants.DAMAGE[UnitType.BASE]);
			Assert.AreEqual(0.0f, Constants.DAMAGE[UnitType.BARRACKS]);
			Assert.AreEqual(0.0f, Constants.DAMAGE[UnitType.ARCHERY]);
		}

		[Test]
		public void CostDelegatedFromSDK()
		{
			// After CalculateGameConstants, COST should match GameConstants values
			foreach (var kvp in GameConstants.COST)
			{
				Assert.AreEqual(kvp.Value, Constants.COST[kvp.Key], 0.001f,
					"Cost mismatch for {0}", kvp.Key);
			}
		}

		[Test]
		public void HealthMine_MatchesStartingGold()
		{
			// GameManager.Instance.StartingMineGold defaults to 10000
			Assert.AreEqual(10000f, Constants.HEALTH[UnitType.MINE], 0.001f);
		}

		[Test]
		public void SpeedZero_CreationTimeIsExplicitInfinity()
		{
			// GAME_SPEED=0 is an intentional pause — creation times are explicitly set to Infinity
			Constants.GAME_SPEED = 0;
			Constants.CalculateGameConstants();
			Assert.AreEqual(float.PositiveInfinity, Constants.CREATION_TIME[UnitType.PAWN]);
		}
	}
}
