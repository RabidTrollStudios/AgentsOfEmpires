using System;
using System.Collections.Generic;
using System.Linq;
using AgentSDK;

namespace GameManager
{
	/// <summary>
	/// Captures per-round performance analytics for a single agent.
	/// Populated incrementally during gameplay by CommandLogger and GameManager,
	/// then snapshot at round end for serialization.
	/// </summary>
	[Serializable]
	public class RoundStats
	{
		#region Round Metadata

		public int RoundNumber;
		public string Result; // "WIN", "LOSS"
		public float DurationSeconds;

		#endregion

		#region Command Counts

		public Dictionary<string, int> CommandsIssued = new Dictionary<string, int>();
		public Dictionary<string, int> CommandsSucceeded = new Dictionary<string, int>();
		public Dictionary<string, int> CommandsFailed = new Dictionary<string, int>();
		public int CommandsThrottled;

		#endregion

		#region Economy

		public int StartingGold;
		public int GoldGathered;
		public int GoldSpent;
		public int PeakGold;
		public int EndingGold;

		#endregion

		#region Military

		public Dictionary<string, int> UnitsProduced = new Dictionary<string, int>();
		public Dictionary<string, int> UnitsLost = new Dictionary<string, int>();
		public Dictionary<string, int> EnemyUnitsKilled = new Dictionary<string, int>();
		public float DamageDealt;
		public float DamageReceived;

		#endregion

		#region Buildings

		public Dictionary<string, int> BuildingsConstructed = new Dictionary<string, int>();
		public Dictionary<string, int> BuildingsLost = new Dictionary<string, int>();

		#endregion

		#region Timing Milestones (game-time seconds, -1 = never)

		public float TimeToFirstBuild = -1f;
		public float TimeToFirstMilitary = -1f;
		public float TimeToFirstAttack = -1f;

		#endregion

		#region End-of-Round Snapshot

		public Dictionary<string, int> FinalUnitCounts = new Dictionary<string, int>();
		public int FinalScore;

		#endregion

		#region Tracking Methods

		public void RecordCommand(string commandType, bool succeeded)
		{
			if (!CommandsIssued.ContainsKey(commandType))
				CommandsIssued[commandType] = 0;
			CommandsIssued[commandType]++;

			if (succeeded)
			{
				if (!CommandsSucceeded.ContainsKey(commandType))
					CommandsSucceeded[commandType] = 0;
				CommandsSucceeded[commandType]++;
			}
			else
			{
				if (!CommandsFailed.ContainsKey(commandType))
					CommandsFailed[commandType] = 0;
				CommandsFailed[commandType]++;
			}
		}

		public void RecordThrottle()
		{
			CommandsThrottled++;
		}

		public void RecordGoldGathered(int amount)
		{
			GoldGathered += amount;
		}

		public void RecordGoldSpent(int amount)
		{
			GoldSpent += amount;
		}

		public void UpdatePeakGold(int currentGold)
		{
			if (currentGold > PeakGold)
				PeakGold = currentGold;
		}

		public void RecordUnitProduced(UnitType unitType)
		{
			string key = unitType.ToString();
			if (!UnitsProduced.ContainsKey(key))
				UnitsProduced[key] = 0;
			UnitsProduced[key]++;
		}

		public void RecordUnitLost(UnitType unitType)
		{
			string key = unitType.ToString();
			bool isBuilding = !Constants.CAN_MOVE[unitType] && unitType != UnitType.MINE;
			if (isBuilding)
			{
				if (!BuildingsLost.ContainsKey(key))
					BuildingsLost[key] = 0;
				BuildingsLost[key]++;
			}
			else
			{
				if (!UnitsLost.ContainsKey(key))
					UnitsLost[key] = 0;
				UnitsLost[key]++;
			}
		}

		public void RecordEnemyKill(UnitType unitType)
		{
			string key = unitType.ToString();
			if (!EnemyUnitsKilled.ContainsKey(key))
				EnemyUnitsKilled[key] = 0;
			EnemyUnitsKilled[key]++;
		}

		public void RecordBuildingConstructed(UnitType unitType)
		{
			string key = unitType.ToString();
			if (!BuildingsConstructed.ContainsKey(key))
				BuildingsConstructed[key] = 0;
			BuildingsConstructed[key]++;
		}

		public void RecordDamageDealt(float amount)
		{
			DamageDealt += amount;
		}

		public void RecordDamageReceived(float amount)
		{
			DamageReceived += amount;
		}

		public void RecordMilestone(string milestone, float gameTime)
		{
			switch (milestone)
			{
				case "BUILD":
					if (TimeToFirstBuild < 0) TimeToFirstBuild = gameTime;
					break;
				case "MILITARY":
					if (TimeToFirstMilitary < 0) TimeToFirstMilitary = gameTime;
					break;
				case "ATTACK":
					if (TimeToFirstAttack < 0) TimeToFirstAttack = gameTime;
					break;
			}
		}

		#endregion

		#region Computed Properties

		public int TotalCommandsIssued => CommandsIssued.Values.Sum();
		public int TotalCommandsSucceeded => CommandsSucceeded.Values.Sum();
		public int TotalCommandsFailed => CommandsFailed.Values.Sum();

		public float CommandSuccessRate =>
			TotalCommandsIssued > 0 ? (float)TotalCommandsSucceeded / TotalCommandsIssued : 0f;

		public int TotalUnitsProduced => UnitsProduced.Values.Sum();
		public int TotalUnitsLost => UnitsLost.Values.Sum();
		public int TotalEnemyKills => EnemyUnitsKilled.Values.Sum();
		public int TotalBuildingsConstructed => BuildingsConstructed.Values.Sum();

		public float KillDeathRatio
		{
			get
			{
				int deaths = TotalUnitsLost + BuildingsLost.Values.Sum();
				return deaths > 0 ? (float)TotalEnemyKills / deaths : TotalEnemyKills;
			}
		}

		#endregion
	}
}
