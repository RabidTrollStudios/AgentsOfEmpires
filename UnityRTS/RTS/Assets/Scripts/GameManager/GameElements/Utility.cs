using System;
using GameManager.EnumTypes;
using UnityEngine;

namespace GameManager.GameElements
{
	/// <summary>
	/// Class with helper functions to convert between coordinate systems
	/// </summary>
	public class Utility
	{
		/// <summary>
		/// Convert a Vector3 to a Vector3Int in the grid
		/// </summary>
		/// <param name="position"></param>
		/// <returns></returns>
		public static Vector3Int WorldToGrid(Vector3 position)
		{
			return new Vector3Int((int)position.x, (int)position.y, 0);
		}

		/// <summary>
		/// Convert a Vector3Int in the grid to a vector2
		/// </summary>
		/// <param name="gridPosition"></param>
		/// <returns></returns>
		public static Vector3 GridToWorld(Vector3Int gridPosition)
		{
			return new Vector3(gridPosition.x, gridPosition.y, 0);

		}

		/// <summary>
		/// Convert a gridcell's number to a Vector3Int in the grid
		/// </summary>
		public static Vector3Int IntToGrid(int gridNbr)
			=> IntToGrid(gridNbr, GameManager.Instance.Map.MapSize);

		/// <summary>
		/// Convert a gridcell's number to a Vector3Int using an explicit map size
		/// </summary>
		public static Vector3Int IntToGrid(int gridNbr, Vector3Int mapSize)
		{
			return new Vector3Int(gridNbr / mapSize.y, gridNbr % mapSize.y, 0);
		}

		/// <summary>
		/// Convert a Vector3Int in the grid to the gridcell's number
		/// </summary>
		public static int GridToInt(Vector3Int position)
			=> GridToInt(position, GameManager.Instance.Map.MapSize);

		/// <summary>
		/// Convert a Vector3Int in the grid to the gridcell's number using an explicit map size
		/// </summary>
		public static int GridToInt(Vector3Int position, Vector3Int mapSize)
		{
			return position.x * mapSize.y + position.y;
		}

		/// <summary>
		/// Safely normalize a vector2, preventing divide
		/// by zero errors or infinitely large vectors
		/// </summary>
		/// <param name="vector"></param>
		/// <returns></returns>
		public static Vector3 SafeNormalize(Vector3 vector)
		{
			Vector3 output = Vector3.zero;
			if (Math.Abs(vector.x) > 0.0f && Math.Abs(vector.y) > 0.0f)
			{
				output = vector.normalized;
			}
			else if (Math.Abs(vector.y) > 0.0f)
			{
				output.y = Math.Sign(vector.y);
			}
			else if (Math.Abs(vector.x) > 0.0f)
			{
				output.x = Math.Sign(vector.x);
			}
			return output;
		}

		/// <summary>
		/// Is the Vector3Int a valid position in the world
		/// </summary>
		public static bool IsValidGridLocation(Vector3Int gridPosition)
			=> IsValidGridLocation(gridPosition, GameManager.Instance.Map.MapSize);

		/// <summary>
		/// Is the Vector3Int a valid position in the world using an explicit map size
		/// </summary>
		public static bool IsValidGridLocation(Vector3Int gridPosition, Vector3Int mapSize)
		{
			return !(gridPosition.x < 0 || gridPosition.x >= mapSize.x
					|| gridPosition.y < 0 || gridPosition.y >= mapSize.y);
		}

		/// <summary>
		/// Used by the animation system to convert between the actual direction the agent is moving
		/// and the direction of its animation
		/// </summary>
		/// <param name="startPosition">starting grid position</param>
		/// <param name="endPosition">ending grid position</param>
		/// <returns></returns>
		public static Direction ConvertPositionToDirection(Vector3Int startPosition, Vector3Int endPosition)
		{
			Vector3Int direction = endPosition - startPosition;

			// Clamp to unit steps so large deltas like (5,3) map correctly
			int cx = direction.x == 0 ? 0 : (direction.x > 0 ? 1 : -1);
			int cy = direction.y == 0 ? 0 : (direction.y > 0 ? 1 : -1);

			if (cx == 0 && cy == 0) return Direction.None;
			if (cx == 0 && cy == -1) return Direction.S;
			if (cx == 1 && cy == -1) return Direction.SE;
			if (cx == 1 && cy == 0) return Direction.E;
			if (cx == 1 && cy == 1) return Direction.NE;
			if (cx == 0 && cy == 1) return Direction.N;
			if (cx == -1 && cy == 1) return Direction.NW;
			if (cx == -1 && cy == 0) return Direction.W;
			return Direction.SW;
		}
	}
}
