using System.Collections.Generic;

namespace AgentSDK
{
    /// <summary>
    /// Optional convenience base class for agents.
    /// Provides unit tracking lists and an UpdateGameState() helper
    /// so you don't have to query unit lists manually each frame.
    ///
    /// To use: extend this class and override Update() and InitializeMatch().
    /// Call UpdateGameState(state) at the start of your Update() method.
    /// </summary>
    public abstract class PlanningAgentBase : IPlanningAgent
    {
        #region Unit Tracking Fields

        /// <summary>The enemy's agent number</summary>
        protected int enemyAgentNbr;

        /// <summary>Your primary mine number (-1 if not set)</summary>
        protected int mainMineNbr;

        /// <summary>Your primary base number (-1 if not set)</summary>
        protected int mainBaseNbr;

        /// <summary>All gold mines on the map</summary>
        protected List<int> mines;

        /// <summary>Your workers</summary>
        protected List<int> myWorkers;
        /// <summary>Your soldiers</summary>
        protected List<int> mySoldiers;
        /// <summary>Your archers</summary>
        protected List<int> myArchers;
        /// <summary>Your bases</summary>
        protected List<int> myBases;
        /// <summary>Your barracks</summary>
        protected List<int> myBarracks;
        /// <summary>Your archeries</summary>
        protected List<int> myArchery;

        /// <summary>Enemy workers</summary>
        protected List<int> enemyWorkers;
        /// <summary>Enemy soldiers</summary>
        protected List<int> enemySoldiers;
        /// <summary>Enemy archers</summary>
        protected List<int> enemyArchers;
        /// <summary>Enemy bases</summary>
        protected List<int> enemyBases;
        /// <summary>Enemy barracks</summary>
        protected List<int> enemyBarracks;
        /// <summary>Enemy archeries</summary>
        protected List<int> enemyArchery;

        /// <summary>Pre-computed valid build positions for 3x3 structures</summary>
        protected List<Position> buildPositions;

        /// <summary>
        /// Debug text displayed on the Custom Debug UI overlay.
        /// Set this in your Update() method to show agent state info.
        /// </summary>
        public string DebugText { get; protected set; } = "";

        #endregion

        /// <summary>
        /// Called once per match. Override to set up match-level state.
        /// </summary>
        public abstract void InitializeMatch();

        /// <summary>
        /// Called at the start of each round. Initializes all unit tracking lists
        /// and finds prospective build positions.
        /// If you override this, call base.InitializeRound(state) first.
        /// </summary>
        public virtual void InitializeRound(IGameState state)
        {
            buildPositions = new List<Position>(state.FindProspectiveBuildPositions(UnitType.BASE));

            mainMineNbr = -1;
            mainBaseNbr = -1;

            mines = new List<int>();
            myWorkers = new List<int>();
            mySoldiers = new List<int>();
            myArchers = new List<int>();
            myBases = new List<int>();
            myBarracks = new List<int>();
            myArchery = new List<int>();

            enemyWorkers = new List<int>();
            enemySoldiers = new List<int>();
            enemyArchers = new List<int>();
            enemyBases = new List<int>();
            enemyBarracks = new List<int>();
            enemyArchery = new List<int>();
        }

        /// <summary>
        /// Your main AI logic. Override this to implement your strategy.
        /// Call UpdateGameState(state) at the beginning to refresh unit lists.
        /// </summary>
        public abstract void Update(IGameState state, IAgentActions actions);

        /// <summary>
        /// Called after each round ends. Override to implement learning.
        /// Default implementation does nothing.
        /// </summary>
        public virtual void Learn(IGameState state)
        {
        }

        /// <summary>
        /// Returns true if any unit in the list is fully built.
        /// Use this to check dependencies before building/training
        /// (e.g., HasBuiltUnit(myBases, state) before building a barracks).
        /// </summary>
        protected bool HasBuiltUnit(List<int> unitNbrs, IGameState state)
        {
            foreach (int unitNbr in unitNbrs)
            {
                UnitInfo? info = state.GetUnit(unitNbr);
                if (info.HasValue && info.Value.IsBuilt)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Refreshes all unit tracking lists from the current game state.
        /// Call this at the start of your Update() method.
        /// </summary>
        protected void UpdateGameState(IGameState state)
        {
            mines = new List<int>(state.GetAllUnits(UnitType.MINE));

            myWorkers = new List<int>(state.GetMyUnits(UnitType.WORKER));
            mySoldiers = new List<int>(state.GetMyUnits(UnitType.SOLDIER));
            myArchers = new List<int>(state.GetMyUnits(UnitType.ARCHER));
            myBarracks = new List<int>(state.GetMyUnits(UnitType.BARRACKS));
            myArchery = new List<int>(state.GetMyUnits(UnitType.ARCHERY));
            myBases = new List<int>(state.GetMyUnits(UnitType.BASE));
            enemyAgentNbr = state.EnemyAgentNbr;
            enemyWorkers = new List<int>(state.GetEnemyUnits(UnitType.WORKER));
            enemySoldiers = new List<int>(state.GetEnemyUnits(UnitType.SOLDIER));
            enemyArchers = new List<int>(state.GetEnemyUnits(UnitType.ARCHER));
            enemyBarracks = new List<int>(state.GetEnemyUnits(UnitType.BARRACKS));
            enemyArchery = new List<int>(state.GetEnemyUnits(UnitType.ARCHERY));
            enemyBases = new List<int>(state.GetEnemyUnits(UnitType.BASE));
        }
    }
}
