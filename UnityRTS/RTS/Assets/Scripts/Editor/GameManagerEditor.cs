using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace GameManager.EditorTools
{
	[CustomEditor(typeof(global::GameManager.GameManager))]
	public class GameManagerEditor : Editor
	{
		private string[] dllNames = new string[0];
		private bool dllsScanned;

		private void ScanDLLs()
		{
			string pathToDLLs = Path.GetFullPath(
				Path.Combine(Application.dataPath, "..", "..", "EnemyAgents"));

			var names = new List<string>();

			if (Directory.Exists(pathToDLLs))
			{
				var rx = new Regex(@"PlanningAgent_(\w+)\.dll",
					RegexOptions.Compiled | RegexOptions.IgnoreCase);

				foreach (string file in Directory.GetFiles(pathToDLLs, "*.dll"))
				{
					var match = rx.Match(Path.GetFileName(file));
					if (match.Success && match.Groups[1].Value != "Mine")
						names.Add(match.Groups[1].Value);
				}
			}

			names.Sort();
			dllNames = names.ToArray();
			dllsScanned = true;
		}

		public override void OnInspectorGUI()
		{
			if (!dllsScanned)
				ScanDLLs();

			serializedObject.Update();

			// Draw Player Settings header with dropdown overrides
			EditorGUILayout.LabelField("Player Settings", EditorStyles.boldLabel);

			DrawDllDropdown("BlueDllName", "Blue DLL");
			DrawDllDropdown("RedDllName", "Red DLL");

			// Draw RandomizeAgentsAsRed normally
			EditorGUILayout.PropertyField(serializedObject.FindProperty("RandomizeAgentsAsRed"));

			// Draw the rest of the inspector, skipping fields we already drew
			DrawPropertiesExcluding(serializedObject,
				"m_Script", "BlueDllName", "RedDllName", "RandomizeAgentsAsRed");

			if (GUILayout.Button("Rescan DLLs"))
				ScanDLLs();

			serializedObject.ApplyModifiedProperties();
		}

		private void DrawDllDropdown(string propertyName, string label)
		{
			var prop = serializedObject.FindProperty(propertyName);

			if (dllNames.Length == 0)
			{
				EditorGUILayout.PropertyField(prop, new GUIContent(label));
				return;
			}

			int currentIndex = System.Array.IndexOf(dllNames, prop.stringValue);
			if (currentIndex < 0)
				currentIndex = 0;

			int newIndex = EditorGUILayout.Popup(label, currentIndex, dllNames);
			prop.stringValue = dllNames[newIndex];
		}
	}
}
