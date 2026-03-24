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

		// Map config property names we draw manually (excluded from DrawPropertiesExcluding)
		private static readonly string[] mapConfigProps = new[]
		{
			"mapMode", "selectedMapIndex", "mapPrefabs",
			"mapWidth", "mapHeight", "mapTemplate",
			"treeDensity", "mapSeed", "mapSymmetry"
		};

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

			// --- Player Settings ---
			EditorGUILayout.LabelField("Player Settings", EditorStyles.boldLabel);
			DrawDllDropdown("BlueDllName", "Blue DLL");
			DrawDllDropdown("RedDllName", "Red DLL");
			EditorGUILayout.PropertyField(serializedObject.FindProperty("RandomizeAgentsAsRed"));

			// --- Map Configuration (custom layout) ---
			EditorGUILayout.Space(8);
			DrawMapConfiguration();

			// --- Everything else (skip what we already drew) ---
			var excludeList = new List<string>
			{
				"m_Script", "BlueDllName", "RedDllName", "RandomizeAgentsAsRed"
			};
			excludeList.AddRange(mapConfigProps);
			DrawPropertiesExcluding(serializedObject, excludeList.ToArray());

			if (GUILayout.Button("Rescan DLLs"))
				ScanDLLs();

			serializedObject.ApplyModifiedProperties();
		}

		private void DrawMapConfiguration()
		{
			EditorGUILayout.LabelField("Map Configuration", EditorStyles.boldLabel);

			var modeProp = serializedObject.FindProperty("mapMode");
			EditorGUILayout.PropertyField(modeProp, new GUIContent("Map Mode"));

			EditorGUI.indentLevel++;

			if (modeProp.enumValueIndex == (int)MapMode.HandMade)
				DrawHandMadeSettings();
			else
				DrawProceduralSettings();

			EditorGUI.indentLevel--;
		}

		private void DrawHandMadeSettings()
		{
			var prefabsProp = serializedObject.FindProperty("mapPrefabs");
			EditorGUILayout.PropertyField(prefabsProp, new GUIContent("Map Prefabs"), true);

			// Build dropdown entries: "Scene Grid" + prefab names
			var entries = new List<string> { "Scene Grid (default)" };
			for (int i = 0; i < prefabsProp.arraySize; i++)
			{
				var elem = prefabsProp.GetArrayElementAtIndex(i);
				string name = elem.objectReferenceValue != null
					? elem.objectReferenceValue.name
					: $"(empty slot {i + 1})";
				entries.Add(name);
			}

			var indexProp = serializedObject.FindProperty("selectedMapIndex");
			int current = Mathf.Clamp(indexProp.intValue, 0, entries.Count - 1);
			int newIdx = EditorGUILayout.Popup("Active Map", current, entries.ToArray());
			indexProp.intValue = newIdx;
		}

		private int lastTemplateIndex = -1;

		private static float MaxDensityForTemplate(int templateIndex)
		{
			// MapTemplate enum: 0=OpenField, 1=Maze, 2=Forest
			switch (templateIndex)
			{
				case 0: return 0.20f; // OpenField
				case 2: return 0.35f; // Forest
				default: return 0.35f; // Maze and others
			}
		}

		private void DrawProceduralSettings()
		{
			var templateProp = serializedObject.FindProperty("mapTemplate");
			EditorGUILayout.PropertyField(templateProp, new GUIContent("Template"));

			EditorGUILayout.PropertyField(
				serializedObject.FindProperty("mapWidth"), new GUIContent("Width"));
			EditorGUILayout.PropertyField(
				serializedObject.FindProperty("mapHeight"), new GUIContent("Height"));

			// Dynamic density slider — max depends on template
			float maxDensity = MaxDensityForTemplate(templateProp.enumValueIndex);

			// When template changes, snap density to the new max
			if (lastTemplateIndex != templateProp.enumValueIndex)
			{
				lastTemplateIndex = templateProp.enumValueIndex;
				var densityProp = serializedObject.FindProperty("treeDensity");
				densityProp.floatValue = maxDensity;
			}

			var density = serializedObject.FindProperty("treeDensity");
			density.floatValue = EditorGUILayout.Slider("Tree Density", density.floatValue, 0f, maxDensity);

			// Seed with Randomize button
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.PropertyField(
				serializedObject.FindProperty("mapSeed"), new GUIContent("Seed"));
			if (GUILayout.Button("Randomize", GUILayout.Width(80)))
			{
				serializedObject.FindProperty("mapSeed").intValue =
					UnityEngine.Random.Range(0, int.MaxValue);
			}
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.PropertyField(
				serializedObject.FindProperty("mapSymmetry"), new GUIContent("Symmetry"));
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
