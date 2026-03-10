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

        /// <summary>Your pawns</summary>
        protected List<int> myPawns;
        /// <summary>Your warriors</summary>
        protected List<int> myWarriors;
        /// <summary>Your archers</summary>
        protected List<int> myArchers;
        /// <summary>Your bases</summary>
        protected List<int> myBases;
        /// <summary>Your barracks</summary>
        protected List<int> myBarracks;
        /// <summary>Your archeries</summary>
        protected List<int> myArchery;

        /// <summary>Enemy pawns</summary>
        protected List<int> enemyPawns;
        /// <summary>Enemy warriors</summary>
        protected List<int> enemyWarriors;
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
            myPawns = new List<int>();
            myWarriors = new List<int>();
            myArchers = new List<int>();
            myBases = new List<int>();
            myBarracks = new List<int>();
            myArchery = new List<int>();

            enemyPawns = new List<int>();
            enemyWarriors = new List<int>();
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

            myPawns = new List<int>(state.GetMyUnits(UnitType.PAWN));
            myWarriors = new List<int>(state.GetMyUnits(UnitType.WARRIOR));
            myArchers = new List<int>(state.GetMyUnits(UnitType.ARCHER));
            myBarracks = new List<int>(state.GetMyUnits(UnitType.BARRACKS));
            myArchery = new List<int>(state.GetMyUnits(UnitType.ARCHERY));
            myBases = new List<int>(state.GetMyUnits(UnitType.BASE));
            enemyAgentNbr = state.EnemyAgentNbr;
            enemyPawns = new List<int>(state.GetEnemyUnits(UnitType.PAWN));
            enemyWarriors = new List<int>(state.GetEnemyUnits(UnitType.WARRIOR));
            enemyArchers = new List<int>(state.GetEnemyUnits(UnitType.ARCHER));
            enemyBarracks = new List<int>(state.GetEnemyUnits(UnitType.BARRACKS));
            enemyArchery = new List<int>(state.GetEnemyUnits(UnitType.ARCHERY));
            enemyBases = new List<int>(state.GetEnemyUnits(UnitType.BASE));
        }
    }
}
