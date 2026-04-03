using UnityEngine;

namespace GameManager.Graph
{
	/// <summary>
	/// Marks a graph node element as having a world-space position.
	/// Used by <see cref="Graph{T}"/> to compute the Euclidean heuristic
	/// for A* pathfinding.
	/// </summary>
	internal interface IPositionable
	{
		/// <summary>Returns this element's world-space position for distance calculations.</summary>
		Vector3 GetPosition();
	}
}
