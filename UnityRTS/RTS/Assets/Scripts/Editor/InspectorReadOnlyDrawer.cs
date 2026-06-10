using UnityEditor;
using UnityEngine;

namespace GameManager.EditorTools
{
	/// <summary>
	/// Property drawer for [InspectorReadOnly]. Renders the field with GUI.enabled
	/// disabled, so the value is visible (and updates each frame from script) but
	/// cannot be edited by the user.
	/// </summary>
	[CustomPropertyDrawer(typeof(global::GameManager.InspectorReadOnlyAttribute))]
	public class InspectorReadOnlyDrawer : PropertyDrawer
	{
		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
			=> EditorGUI.GetPropertyHeight(property, label, true);

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			bool wasEnabled = GUI.enabled;
			GUI.enabled = false;
			EditorGUI.PropertyField(position, property, label, true);
			GUI.enabled = wasEnabled;
		}
	}
}
