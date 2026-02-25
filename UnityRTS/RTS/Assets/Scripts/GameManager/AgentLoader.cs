using AgentSDK;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;

namespace GameManager
{
	/// <summary>
	/// Handles loading agent DLLs from disk and discovering available agents.
	/// </summary>
	public class AgentLoader
	{
		/// <summary>
		/// Path to the DLLs directory
		/// </summary>
		private string pathToDLLs;

		public AgentLoader(string pathToDLLs)
		{
			this.pathToDLLs = pathToDLLs;
		}

		/// <summary>
		/// Get all of the DLL names from the EnemyAgents folder
		/// </summary>
		/// <returns>list of agent DLL names (without path/extension)</returns>
		public List<string> GetDLLNamesFromDir(GameObject logContext)
		{
			GameManager.Instance.Log("NOTICE: Playing single round against all DLLs", logContext);
			List<string> dllNames = new List<string>();

			string[] files = Directory.GetFiles(pathToDLLs);
			Debug.Log("pathToDLLs: " + pathToDLLs);
			Debug.Log("files: " + files.Length);
			Regex rx = new Regex(@"^.*PlanningAgent_(\w+)\.dll",
			  RegexOptions.Compiled | RegexOptions.IgnoreCase);
			foreach (string name in files)
			{
				Debug.Log("filename: " + name);
				MatchCollection matches = rx.Matches(name);

				if (matches.Count > 0 && matches[0].Groups.Count > 1 && matches[0].Groups[1].Value != "Mine")
				{
					string dllName = matches[0].Groups[1].Value;
					dllNames.Add(dllName);
				}
			}

			Debug.Log("dllNames: " + dllNames.Count);
			return dllNames;
		}

		/// <summary>
		/// Load a DLL for a PlanningAgent and create a GameObject from its exported type
		/// </summary>
		/// <param name="playerName">player name (for error messages)</param>
		/// <param name="dllName">name of the DLL to load</param>
		/// <param name="logContext">GameObject for debug log context</param>
		/// <returns>a new GameObject with the agent component</returns>
		public GameObject LoadDLL(string playerName, string dllName, GameObject logContext)
		{
			GameObject go = null;

			string filename = pathToDLLs + Path.AltDirectorySeparatorChar + "PlanningAgent_" + dllName + ".dll";
			Debug.Log("Opening dll file: " + filename);

			// Load from bytes so the file is never locked on disk.
			// This lets students rebuild their DLL without Unity holding a lock.
			var DLL = Assembly.Load(File.ReadAllBytes(filename));
			if (DLL == null)
			{
				GameManager.Instance.Log("ERROR: Cannot find file: " + filename, logContext);
			}

			foreach (Type type in DLL.GetExportedTypes())
			{
				if (typeof(IPlanningAgent).IsAssignableFrom(type) && !type.IsAbstract)
				{
					// SDK pattern: plain C# class implementing IPlanningAgent
					var agent = (IPlanningAgent)Activator.CreateInstance(type);
					go = new GameObject(type.Name);
					var bridge = go.AddComponent<AgentBridge>();
					bridge.SetPlanningAgent(agent);
				}
				else if (typeof(Agent).IsAssignableFrom(type) && !type.IsAbstract)
				{
					// Legacy pattern: MonoBehaviour extending Agent
					go = new GameObject(type.Name, type);
				}
			}

			if (go == null)
			{
				GameManager.Instance.Log("ERROR: Cannot instantiate " + filename, logContext);
			}
			return go;
		}

		/// <summary>
		/// The path to the DLLs directory
		/// </summary>
		public string PathToDLLs => pathToDLLs;
	}
}
