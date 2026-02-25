using UnityEngine;
using GameManager.Graph;

namespace GameManager.Tests
{
	/// <summary>
	/// Mock cell implementing all three Graph interfaces without requiring a Tilemap.
	/// Used for standalone Graph/A* testing.
	/// </summary>
	internal class TestCell : IColorable, IBuildable, IPositionable
	{
		public Vector3Int Position;
		private bool _buildable = true;
		private bool _walkable = true;

		public TestCell(Vector3Int position)
		{
			Position = position;
		}

		public void ChangeColor(Color color) { }

		public bool IsBuildable() => _buildable;
		public void SetBuildable(bool value) { _buildable = value; }

		public bool IsWalkable() => _walkable;
		public void SetWalkable(bool value) { _walkable = value; }

		public Vector3 GetPosition() => Position;
	}
}
