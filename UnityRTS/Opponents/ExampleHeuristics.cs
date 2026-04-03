using System.Collections.Generic;
using System.Linq;
using AgentSDK;

namespace PlanningAgent
{
	/// <summary>
	/// [EXPERIMENTAL] Influence-map-based decision making.
	/// Uses two layered maps (territoryMap, enemyMap) with linear distance falloff:
	///   - Territory map weights bases (3x), barracks (2x), and mines (2x)
	///   - Enemy map weights hostile structures similarly
	///   - Decision functions subtract territory from enemy influence
	/// Pawns use influence to choose build locations; combat units use it to
	/// prioritize targets. Partially implemented — serves as a reference for
	/// influence-map AI patterns.
	/// </summary>
	public class PlanningAgent : PlanningAgentBase
	{

        private enum HeuristicTasks {
            BASE_BUILDING,
            BARRACKS_BUILDING,
            ATTACKING,
            TRAIN_ARCHER,
            TRAIN_WARRIOR,
            TRAIN_PAWN,
            GATHER,
            MOVE
        }

		private const int MAX_NBR_ARCHERS = 20;
		private const float MAX_ARCHER_MULTIPLIER = 2.0f;
		private const int MAX_NBR_WARRIORS = 10;
		private const float MAX_WARRIOR_MULTIPLIER = 2.0f;
		private const int MAX_NBR_PAWNS = 15;

		#region Private Fields

		public List<int> enemyUnits { get; set; }
		public List<int> enemyBuildings { get; set; }

        // Added list for heuristic value storage
        private Dictionary<HeuristicTasks, float> heuristics;
        private HeuristicTasks maxIndex;
        private float highestCost;

        // Influence Maps
        private float[,] territoryMap;
        private float[,] enemyMap;

		/// <summary>
		/// Method compares the influence maps to the prospective build locations list
		/// to find an optimal location to build a structure.
		/// </summary>
		/// <param name="unitType">type of unit you want to build</param>
		/// <param name="state">current game state</param>
		/// <returns></returns>
		public Position FindBestBuildPosition(UnitType unitType, IGameState state)
		{
			// Variables to store the optimal position as we find it
			float minInf = float.MaxValue;
			Position minBuildPosition = Position.Zero;

			// For all the possible build postions that we already found
			foreach (Position buildPosition in buildPositions)
			{
				float influence;
				if (territoryMap[buildPosition.X, buildPosition.Y] < 1)
				{
					influence = enemyMap[buildPosition.X, buildPosition.Y] - territoryMap[buildPosition.X, buildPosition.Y];
				}
				else
				{
					influence = enemyMap[buildPosition.X, buildPosition.Y];
				}
				// if the influence on that build position is more advantageous for the player than any other so far
				if (influence < minInf && state.IsAreaBuildable(unitType, buildPosition))
				{
					// Store this build position as the best seen so far
					minInf = influence;
					minBuildPosition = buildPosition;
				}
			}

			// Return the best build position
			return minBuildPosition;
		}

        /// <summary>
        /// Update heuristics for evaluating potential actions
        /// </summary>
        /// <param name="state">current game state</param>
        private void UpdateHeuristics(IGameState state)
        {
             // Build a Base
            if (state.MyGold > GameConstants.COST[UnitType.BASE])
            {
                heuristics[HeuristicTasks.BASE_BUILDING] = 1;
            }
            else
            {
                heuristics[HeuristicTasks.BASE_BUILDING] = 0;
            }

            // 3: Build a Barracks
            if (state.MyGold > GameConstants.COST[UnitType.BARRACKS])
            {
                heuristics[HeuristicTasks.BARRACKS_BUILDING] = 1;
            }
            else
            {
                heuristics[HeuristicTasks.BARRACKS_BUILDING] = 0;
            }

            // 5: Train a Pawn
            if (state.MyGold >= GameConstants.COST[UnitType.PAWN])
            {
                heuristics[HeuristicTasks.TRAIN_PAWN] = 1;
            }
            else
            {
                heuristics[HeuristicTasks.TRAIN_PAWN] = 0;
            }

            // 6: Train a Warrior
            if (state.MyGold >= GameConstants.COST[UnitType.WARRIOR])
            {
                heuristics[HeuristicTasks.TRAIN_WARRIOR] = 1;
            }
            else
            {
                heuristics[HeuristicTasks.TRAIN_WARRIOR] = 0;
            }

            // 7: Train an Archer
            if (state.MyGold >= GameConstants.COST[UnitType.ARCHER])
            {
                heuristics[HeuristicTasks.TRAIN_ARCHER] = 1;
            }
            else
            {
                heuristics[HeuristicTasks.TRAIN_ARCHER] = 0;
            }

            // 8: Attack the Enemy
            if (myWarriors.Count + myArchers.Count > 0)
            {
                heuristics[HeuristicTasks.ATTACKING] = 1;
            }
            else
            {
                heuristics[HeuristicTasks.ATTACKING] = 0;
            }

            // 0: Gather
            heuristics[HeuristicTasks.GATHER] = 0;

            // 1: Move
            heuristics[HeuristicTasks.MOVE] = 0;


            // Calculate next decision
            maxIndex = heuristics.FirstOrDefault(x => x.Value == heuristics.Values.Max()).Key;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Called once per match to initialize match-level state.
        /// </summary>
        public override void InitializeMatch()
        {
        }

        /// <summary>
        /// Called at the start of each round. Initializes all unit tracking lists
        /// and agent-specific state.
        /// </summary>
        public override void InitializeRound(IGameState state)
        {
            base.InitializeRound(state);

            enemyUnits = new List<int>();
            enemyBuildings = new List<int>();

            // Initialize the list of heuristics
            heuristics = new Dictionary<HeuristicTasks, float>();

            // Determine cost for the most expensive type of unit
            highestCost = 0.0f;
            foreach (float cost in GameConstants.COST.Values)
            {
                if (cost > highestCost)
                {
                    highestCost = cost;
                }
            }

            // Initialize influence maps
            territoryMap = new float[state.MapSize.X, state.MapSize.Y];
            enemyMap = new float[state.MapSize.X, state.MapSize.Y];
        }

		/// <summary>
		/// Called after each round ends. Override to implement learning.
		/// </summary>
		public override void Learn(IGameState state)
		{
		}

		// Update the GameManager - called once per frame
		public override void Update(IGameState state, IAgentActions actions)
		{
            UpdateGameState(state);

            // Update composite enemy lists
            enemyUnits = new List<int>();
            enemyBuildings = new List<int>();
            enemyUnits.AddRange(enemyWarriors);
            enemyUnits.AddRange(enemyArchers);
            enemyBuildings.AddRange(enemyBases);
            enemyBuildings.AddRange(enemyBarracks);
            enemyUnits.AddRange(enemyLancers);
            enemyBuildings.AddRange(enemyArchery);
            enemyBuildings.AddRange(enemyTowers);

			// If we have at least one base, assume the first one is our "main" base
			if (myBases.Count > 0)
			{
				mainBaseNbr = myBases[0];
			}
			else
			{
				mainBaseNbr = -1;
			}

			// If we have a base, find the closest mine to the base
			if (mines.Count > 0 && mainBaseNbr >= 0)
			{
                mainMineNbr = mines.First();
			}

            // Update heuristic values for decision making
            UpdateHeuristics(state);
		}

		#endregion
	}
}
