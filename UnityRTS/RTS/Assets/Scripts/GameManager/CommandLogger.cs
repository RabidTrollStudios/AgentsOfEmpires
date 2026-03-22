using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace GameManager
{
	/// <summary>
	/// Logs all agent commands and their outcomes to a human-readable text file
	/// and to the Unity console. One file per agent per match, containing all rounds.
	/// Also feeds per-round analytics into the owning agent's MatchAnalytics.
	/// </summary>
	public class CommandLogger
	{
		private StreamWriter writer;
		private readonly string agentName;
		private readonly GameObject logContext;
		private int roundNbr;

		/// <summary>
		/// Optional analytics tracker. When set, every LogCommand call also
		/// records structured data into the current RoundStats.
		/// </summary>
		public MatchAnalytics Analytics { get; set; }

		public CommandLogger(string agentName, string filePath, GameObject logContext)
		{
			this.agentName = agentName;
			this.logContext = logContext;
			writer = new StreamWriter(filePath, append: false, encoding: Encoding.UTF8);
			writer.AutoFlush = true;
			writer.WriteLine($"=== Command Log for {agentName} ===");
			writer.WriteLine($"Started: {DateTime.Now}");
			writer.WriteLine();
		}

		public void StartRound(int roundNbr)
		{
			this.roundNbr = roundNbr;
			writer.WriteLine($"--- Round {roundNbr} ---");
		}

		public void EndRound(string result)
		{
			writer.WriteLine($"--- End Round {roundNbr}: {result} ---");
			writer.WriteLine();
		}

		/// <summary>
		/// Logs a command to both the file and the Unity console.
		/// Also records structured analytics when a MatchAnalytics is attached.
		/// </summary>
		/// <param name="command">Command type (e.g. MOVE, BUILD, GATHER, TRAIN, ATTACK)</param>
		/// <param name="details">What/who is involved</param>
		/// <param name="result">Outcome (SUCCESS, FAILED, STARTED, EXEC_FAILED)</param>
		public void LogCommand(string command, string details, string result)
		{
			int frame = Time.frameCount;
			float time = GameManager.Instance.TotalGameTime;
			string line = $"[F{frame} T={time:F1}s] {command}: {details} -> {result}";
			writer.WriteLine(line);
			GameManager.Instance.Log($"{agentName} {line}", logContext);

			RecordAnalytics(command, result, time);
		}

		/// <summary>
		/// Feeds command outcome data into the current RoundStats.
		/// </summary>
		private void RecordAnalytics(string command, string result, float gameTime)
		{
			var round = Analytics?.CurrentRound;
			if (round == null) return;

			if (result.StartsWith("THROTTLED"))
			{
				round.RecordThrottle();
				return;
			}

			// DEATH is an observation event, not a command outcome
			if (command == "DEATH")
				return;

			bool succeeded = result.StartsWith("SUCCESS") || result.StartsWith("STARTED");
			round.RecordCommand(command, succeeded);

			// Track milestone timing on first successful dispatch
			if (succeeded)
			{
				switch (command)
				{
					case "BUILD":
						round.RecordMilestone("BUILD", gameTime);
						break;
					case "ATTACK":
						round.RecordMilestone("ATTACK", gameTime);
						break;
				}
			}
		}

		public void Close()
		{
			writer?.Flush();
			writer?.Close();
			writer = null;
		}
	}
}
