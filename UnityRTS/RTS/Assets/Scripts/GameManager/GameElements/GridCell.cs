using UnityEngine;
using UnityEngine.Tilemaps;
using GameManager.Graph;

namespace GameManager.GameElements
{
	internal class GridCell : IColorable, IBuildable, IPositionable
	{
		internal Vector3Int Position { get; set; }

		internal Tilemap TileMap { get; set; }

		internal TileBase Tile { get; set; }

		private bool isBuildable;
		private bool isWalkable;

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
