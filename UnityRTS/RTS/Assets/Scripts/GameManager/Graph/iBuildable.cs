namespace GameManager.Graph
{
	internal interface IBuildable
    {
        bool IsBuildable();
		void SetBuildable(bool isBuildable);

		bool IsWalkable();
		void SetWalkable(bool isWalkable);
    }
}
