namespace GameManager.Graph
{
	/// <summary>
	/// Marks a graph node element as having walkability and buildability state.
	/// Implemented by <see cref="GridCell"/> to let the A* search query whether
	/// a cell can be traversed (walkable) or is free for construction (buildable).
	/// </summary>
	internal interface IBuildable
    {
        /// <summary>Returns true if this cell is open for building (no unit or structure occupying it).</summary>
        bool IsBuildable();
		/// <summary>Sets whether this cell is available for building placement.</summary>
		void SetBuildable(bool isBuildable);

		/// <summary>Returns true if a unit can path through this cell (not blocked by terrain or structures).</summary>
		bool IsWalkable();
		/// <summary>Sets whether this cell can be traversed by moving units.</summary>
		void SetWalkable(bool isWalkable);
    }
}
