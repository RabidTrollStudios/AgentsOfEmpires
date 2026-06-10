using UnityEngine;

namespace GameManager
{
	/// <summary>
	/// Marks a serialized field as read-only in the inspector. The field is still
	/// shown and updated each frame from script, but cannot be edited by the user.
	/// Used for live-state debug fields on Unit.cs and other components.
	///
	/// Pairs with InspectorReadOnlyDrawer in Scripts/Editor/.
	/// </summary>
	public class InspectorReadOnlyAttribute : PropertyAttribute { }
}
