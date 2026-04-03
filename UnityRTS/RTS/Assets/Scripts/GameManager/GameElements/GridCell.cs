using UnityEngine;
using UnityEngine.Tilemaps;
using GameManager.Graph;

namespace GameManager.GameElements
{
	/// <summary>
	/// Represents a single cell in the Unity-side tilemap grid.
	/// Implements <see cref="IBuildable"/>, <see cref="IColorable"/>, and <see cref="IPositionable"/>
	/// so it can serve as the node type for the <see cref="Graph.Graph{T}"/> A* pathfinder.
	///
	/// Tracks three passability states:
	///   - buildable: cell is free for construction (no unit, no structure)
	///   - walkable: cell can be traversed by mobile units (open or passage)
	///   - passage: a building's top row — walkable but not buildable
	/// </summary>
	internal class GridCell : IColorable, IBuildable, IPositionable
	{
		/// <summary>Grid coordinates of this cell on the tilemap.</summary>
		internal Vector3Int Position { get; set; }

		/// <summary>The tilemap this cell belongs to.</summary>
		internal Tilemap TileMap { get; set; }

		/// <summary>The tile asset rendered at this cell's position.</summary>
		internal TileBase Tile { get; set; }

		private bool isBuildable;
		private bool isWalkable;
		private bool isPassage;

		#region Interface Implementations
		public void ChangeColor(Color color)
		{
		}

		public bool IsBuildable()
		{
			return this.isBuildable;
		}

		public void SetBuildable(bool isBuildable)
		{
			this.isBuildable = isBuildable;
		}

		public bool IsWalkable()
		{
			return this.isWalkable;
		}

		public void SetWalkable(bool isWalkable)
		{
			this.isWalkable = isWalkable;
		}

		/// <summary>
		/// A passage cell is walkable but not buildable, and units should move through
		/// it freely without triggering collision avoidance (e.g., building top rows).
		/// </summary>
		public bool IsPassage()
		{
			return this.isPassage;
		}

		public void SetPassage(bool isPassage)
		{
			this.isPassage = isPassage;
		}

		public Vector3 GetPosition()
		{
			return Position;
		}

		#endregion

		internal GridCell(Tilemap tileMap, Vector3Int position)
		{
			this.TileMap = tileMap;
			this.Position = position;
			this.Tile = tileMap.GetTile(Position);
			this.isBuildable = true;
			this.isWalkable = true;
		}
	}
}
