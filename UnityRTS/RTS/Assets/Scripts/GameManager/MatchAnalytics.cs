using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AgentSDK;

namespace GameManager
{
	/// <summary>
	/// Aggregates per-round RoundStats across a full match and serializes
	/// the result to a JSON file for later analysis / visualization.
	/// One MatchAnalytics instance per agent per match.
	/// </summary>
	[Serializable]
	public class MatchAnalytics
	{
		#region Match Metadata

		public string AgentName;
		public string AgentDLLName;
		public string OpponentDLLName;
		public string MatchStarted;
		public int TotalRounds;
		public int RoundsWon;
		public int RoundsLost;

		#endregion

		#region Round Data

		public List<RoundStats> Rounds = new List<RoundStats>();

		#endregion

		#region Current Round Tracking

		[NonSerialized] private RoundStats currentRound;

		public RoundStats CurrentRound => currentRound;

		public void BeginRound(int roundNumber, int startingGold)
		{
			currentRound = new RoundStats
			{
				RoundNumber = roundNumber,
				StartingGold = startingGold,
				PeakGold = startingGold
			};
		}

		public void EndRound(string result, float duration, int endingGold,
			Dictionary<UnitType, int> unitCounts, int score)
		{
			if (currentRound == null) return;

			currentRound.Result = result;
			currentRound.DurationSeconds = duration;
			currentRound.EndingGold = endingGold;
			currentRound.FinalScore = score;

			foreach (var kvp in unitCounts)
				currentRound.FinalUnitCounts[kvp.Key.ToString()] = kvp.Value;

			if (result == "WIN")
				RoundsWon++;
			else
				RoundsLost++;

			Rounds.Add(currentRound);
			currentRound = null;
		}

		#endregion

		#region Aggregate Computed Stats

		public float OverallWinRate => TotalRounds > 0 ? (float)RoundsWon / TotalRounds : 0f;

		public float AvgCommandSuccessRate =>
			Rounds.Count > 0 ? Rounds.Average(r => r.CommandSuccessRate) : 0f;

		public float AvgGoldGathered =>
			Rounds.Count > 0 ? (float)Rounds.Average(r => r.GoldGathered) : 0f;

		public float AvgUnitsProduced =>
			Rounds.Count > 0 ? (float)Rounds.Average(r => r.TotalUnitsProduced) : 0f;

		public float AvgKillDeathRatio =>
			Rounds.Count > 0 ? Rounds.Average(r => r.KillDeathRatio) : 0f;

		public float AvgRoundDuration =>
			Rounds.Count > 0 ? Rounds.Average(r => r.DurationSeconds) : 0f;

		#endregion

		#region Serialization

		public void SaveToFile(string filePath)
		{
			string json = ToJson();
			File.WriteAllText(filePath, json, Encoding.UTF8);
		}

		/// <summary>
		/// Hand-rolled JSON serialization to avoid Unity's JsonUtility limitations
		/// with Dictionaries and computed properties.
		/// </summary>
		public string ToJson()
		{
			var sb = new StringBuilder();
			sb.AppendLine("{");

			// Match metadata
			sb.AppendLine($"  \"agentName\": \"{Escape(AgentName)}\",");
			sb.AppendLine($"  \"agentDLLName\": \"{Escape(AgentDLLName)}\",");
			sb.AppendLine($"  \"opponentDLLName\": \"{Escape(OpponentDLLName)}\",");
			sb.AppendLine($"  \"matchStarted\": \"{Escape(MatchStarted)}\",");
			sb.AppendLine($"  \"totalRounds\": {TotalRounds},");
			sb.AppendLine($"  \"roundsWon\": {RoundsWon},");
			sb.AppendLine($"  \"roundsLost\": {RoundsLost},");
			sb.AppendLine($"  \"overallWinRate\": {OverallWinRate:F3},");

			// Aggregate stats
			sb.AppendLine($"  \"avgCommandSuccessRate\": {AvgCommandSuccessRate:F3},");
			sb.AppendLine($"  \"avgGoldGathered\": {AvgGoldGathered:F1},");
			sb.AppendLine($"  \"avgUnitsProduced\": {AvgUnitsProduced:F1},");
			sb.AppendLine($"  \"avgKillDeathRatio\": {AvgKillDeathRatio:F2},");
			sb.AppendLine($"  \"avgRoundDuration\": {AvgRoundDuration:F1},");

			// Rounds array
			sb.AppendLine("  \"rounds\": [");
			for (int i = 0; i < Rounds.Count; i++)
			{
				sb.Append(RoundToJson(Rounds[i], "    "));
				sb.AppendLine(i < Rounds.Count - 1 ? "," : "");
			}
			sb.AppendLine("  ]");

			sb.AppendLine("}");
			return sb.ToString();
		}

		private string RoundToJson(RoundStats r, string indent)
		{
			var sb = new StringBuilder();
			sb.AppendLine($"{indent}{{");
			string i2 = indent + "  ";

			sb.AppendLine($"{i2}\"roundNumber\": {r.RoundNumber},");
			sb.AppendLine($"{i2}\"result\": \"{Escape(r.Result)}\",");
			sb.AppendLine($"{i2}\"durationSeconds\": {r.DurationSeconds:F1},");

			// Commands
			sb.AppendLine($"{i2}\"commands\": {{");
			sb.AppendLine($"{i2}  \"issued\": {DictToJson(r.CommandsIssued)},");
			sb.AppendLine($"{i2}  \"succeeded\": {DictToJson(r.CommandsSucceeded)},");
			sb.AppendLine($"{i2}  \"failed\": {DictToJson(r.CommandsFailed)},");
			sb.AppendLine($"{i2}  \"throttled\": {r.CommandsThrottled},");
			sb.AppendLine($"{i2}  \"totalIssued\": {r.TotalCommandsIssued},");
			sb.AppendLine($"{i2}  \"successRate\": {r.CommandSuccessRate:F3}");
			sb.AppendLine($"{i2}}},");

			// Economy
			sb.AppendLine($"{i2}\"economy\": {{");
			sb.AppendLine($"{i2}  \"startingGold\": {r.StartingGold},");
			sb.AppendLine($"{i2}  \"goldGathered\": {r.GoldGathered},");
			sb.AppendLine($"{i2}  \"goldSpent\": {r.GoldSpent},");
			sb.AppendLine($"{i2}  \"peakGold\": {r.PeakGold},");
			sb.AppendLine($"{i2}  \"endingGold\": {r.EndingGold}");
			sb.AppendLine($"{i2}}},");

			// Military
			sb.AppendLine($"{i2}\"military\": {{");
			sb.AppendLine($"{i2}  \"unitsProduced\": {DictToJson(r.UnitsProduced)},");
			sb.AppendLine($"{i2}  \"unitsLost\": {DictToJson(r.UnitsLost)},");
			sb.AppendLine($"{i2}  \"enemyUnitsKilled\": {DictToJson(r.EnemyUnitsKilled)},");
			sb.AppendLine($"{i2}  \"damageDealt\": {r.DamageDealt:F1},");
			sb.AppendLine($"{i2}  \"damageReceived\": {r.DamageReceived:F1},");
			sb.AppendLine($"{i2}  \"killDeathRatio\": {r.KillDeathRatio:F2}");
			sb.AppendLine($"{i2}}},");

			// Buildings
			sb.AppendLine($"{i2}\"buildings\": {{");
			sb.AppendLine($"{i2}  \"constructed\": {DictToJson(r.BuildingsConstructed)},");
			sb.AppendLine($"{i2}  \"lost\": {DictToJson(r.BuildingsLost)}");
			sb.AppendLine($"{i2}}},");

			// Timing milestones
			sb.AppendLine($"{i2}\"milestones\": {{");
			sb.AppendLine($"{i2}  \"timeToFirstBuild\": {FormatFloat(r.TimeToFirstBuild)},");
			sb.AppendLine($"{i2}  \"timeToFirstMilitary\": {FormatFloat(r.TimeToFirstMilitary)},");
			sb.AppendLine($"{i2}  \"timeToFirstAttack\": {FormatFloat(r.TimeToFirstAttack)}");
			sb.AppendLine($"{i2}}},");

			// End-of-round snapshot
			sb.AppendLine($"{i2}\"finalUnitCounts\": {DictToJson(r.FinalUnitCounts)},");
			sb.AppendLine($"{i2}\"finalScore\": {r.FinalScore}");

			sb.Append($"{indent}}}");
			return sb.ToString();
		}

		private static string DictToJson(Dictionary<string, int> dict)
		{
			if (dict == null || dict.Count == 0)
				return "{}";

			var entries = dict.Select(kvp => $"\"{Escape(kvp.Key)}\": {kvp.Value}");
			return "{ " + string.Join(", ", entries) + " }";
		}

		private static string FormatFloat(float value)
		{
			return value < 0 ? "null" : value.ToString("F1");
		}

		private static string Escape(string s)
		{
			if (s == null) return "";
			return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
		}

		#endregion
	}
}
