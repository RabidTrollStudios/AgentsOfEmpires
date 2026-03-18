using AgentSDK;
using GameManager.GameElements;
using System;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using UnityEngine;
using System.Text;
using System.Text.RegularExpressions;


[assembly: InternalsVisibleTo("GameManager")]
[assembly: InternalsVisibleTo("AgentController")]
[assembly: InternalsVisibleTo("GameManager.Tests.EditMode")]
[assembly: InternalsVisibleTo("GameManager.Tests.PlayMode")]

namespace GameManager
{

	/// <summary>
	/// Represents a Player in the game
	/// </summary>
	[Serializable]
	public abstract partial class Agent : MonoBehaviour
	{
		#region Public Properties

		/// <summary>
	    /// Unique number that identifies this agent
	    /// </summary>
	    public int AgentNbr { get; private set; }

        /// <summary>
        /// Name for this agent (used in debugging)
        /// </summary>
        public string AgentName { get; private set; }

		/// <summary>
		/// DLL name for this agent (used in declaring the winner)
		/// </summary>
		public string AgentDLLName { get; private set; }

		/// <summary>
		/// Number of wins this agent currently has
		/// </summary>
		public int AgentNbrWins { get; internal set; }

		private string DllPath { get; set; }

		private FileStream LogFileStream { get; set; }

		private string logFileName { get; set; }

		#endregion

		#region Public File Logging

		/// <summary>
		/// Log the learned data to a csv file
		/// </summary>
		/// <param name="str"></param>
		public void Log(string str)
		{
			if (str.Contains(","))
				str = "\"" + str + "\"";
			byte[] info = new UTF8Encoding(true).GetBytes(str + ",");
			LogFileStream.Write(info, 0, info.Length);
		}

		internal void EndLogLine()
		{
			byte[] info = new UTF8Encoding(true).GetBytes("\n");
			LogFileStream.Write(info, 0, info.Length);
		}

		internal void CloseLogFile()
		{
			LogFileStream.Close();
		}

		internal void OpenLogFile()
		{
			LogFileStream = File.Open(logFileName,FileMode.Append);
		}

		internal void OpenCommandLog()
		{
			string cmdLogPath = DllPath + Path.AltDirectorySeparatorChar
				+ "CommandLog_" + AgentDLLName + "_" + AgentName + ".txt";
			CmdLog = new CommandLogger(AgentName + " " + AgentDLLName, cmdLogPath, this.gameObject);
		}

		internal void CloseCommandLog()
		{
			CmdLog?.Close();
			CmdLog = null;
		}

		#endregion

		#region Constructors and Initialization

		/// <summary>
		/// InitializeAgent the agent's identity, this is called once at the
		/// beginning of the entire game
		/// </summary>
		/// <param name="agentName">agent's blue/red name</param>
		/// <param name="agentNbr">agent's unique number</param>
		/// <param name="dllName">agent's dll name</param>
		/// <param name="dllPath"></param>
		internal void InitializeAgent(string agentName, string dllName, int agentNbr, string dllPath)
        {
            AgentName = agentName;
            AgentNbr = agentNbr;
			AgentDLLName = dllName;
			DllPath = dllPath;
			AgentNbrWins = 0;

			string baseName = "PlanningAgent_" + dllName + "_" + agentName;
			logFileName = dllPath + Path.AltDirectorySeparatorChar + baseName + ".csv";

			// Create a new file by appending a number if it already exists
			if (File.Exists(logFileName))
			{
				string[] files = Directory.GetFiles(dllPath + Path.AltDirectorySeparatorChar, baseName + "*.csv");
				int max = 0;

				Regex rx = new Regex(Regex.Escape(baseName) + @"_(\d)\.csv",
					RegexOptions.Compiled | RegexOptions.IgnoreCase);

				foreach (string file in files)
				{
					MatchCollection mc = rx.Matches(file);

					foreach (Match m in mc)
					{
						int value;
						if (Int32.TryParse(m.Groups[1].Value, out value) && max < value)
						{
							max = value;
						}
					}
				}
				logFileName = dllPath + Path.AltDirectorySeparatorChar + baseName + "_" + (++max) + ".csv";
			}
			GameManager.Instance.Log("Creating: " + logFileName, this.gameObject);
			//LogFileStream = File.Create(logFileName);
        }

        /// <summary>
        /// InitializeMatch
        /// This method must be overriden by
        /// the PlanningAgent and is called at the beginning of each matching
        /// of two agents.  Each match is comprised of multiple rounds.  This
        /// is called only once to initialize the agent regardless of the
        /// number of rounds.
        /// </summary>
        public abstract void InitializeMatch();

        /// <summary>
        /// InitializeRound
        /// This method must be overridden by the PlanningAgent and is
        /// called at the beginning of each round in a game.  Multiple
        /// rounds make a single match between two agents.
        /// </summary>
        public abstract void InitializeRound();

        /// <summary>
        /// Learn
        /// This method is called at the end of each match BEFORE any
        /// remaining troops are destroyed, so the PlanningAgent can
        /// observe the "win" state and learn from it.
        /// </summary>
        public abstract void Learn();

        #endregion

        #region Properties

        /// <summary>
        /// The amount of gold the agent currently has
        /// </summary>
        public int Gold { get; internal set; }

		/// <summary>
		/// Command logger for recording all commands and their outcomes
		/// </summary>
		internal CommandLogger CmdLog { get; set; }

		/// <summary>
		/// Screen color of the agent
		/// </summary>
		internal Color Color { get; set; }

		#endregion

		#region Public Methods

		/// <summary>
		/// Updates the agent each frame
		/// </summary>
		public virtual void Update() { }

		/// <summary>
		/// Clean up file handles when the editor stops or the object is destroyed
		/// </summary>
		protected virtual void OnDestroy()
		{
			CloseCommandLog();
		}

		#endregion
	}
}
