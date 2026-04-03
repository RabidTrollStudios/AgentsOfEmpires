using System;
using System.Collections.Generic;
using System.Linq;
using AgentSDK;

namespace PlanningAgent
{
	/// <summary>
	/// [EXPERIMENTAL/INCOMPLETE] Extended influence-map agent with heuristic scoring.
	/// Builds on the same dual-map pattern as ExampleHeuristics (territory vs. enemy
	/// influence) but adds weighted heuristic evaluation for action selection.
	/// Trains archers (up to 20) and warriors. Update logic is incomplete —
	/// this agent exists as a development reference, not a competitive opponent.
	/// </summary>
	public class PlanningAgent : PlanningAgentBase
	{
		private const int MAX_NBR_ARCHERS = 20;
		private const float MAX_ARCHER_MULTIPLIER = 2.0f;
		private const int MAX_NBR_WARRIORS = 10;
		private const float MAX_WARRIOR_MULTIPLIER = 2.0f;
		private const int MAX_NBR_PAWNS = 15;

		#region Private Fields

		public bool lastFighterWasWarrior { get; set; }

		// Added list for heuristic value storage
		private List<float> heuristics;
		private int maxIndex;
		private float highestCost;

		// Influence Maps
		private float[,] territoryMap;
		private float[,] enemyMap;

		#endregion

		#region Private Methods

		/// <summary>
		/// Finds the closest build position to the gridPosition.
		/// </summary>
		public Position FindClosestBuildPosition(Position gridPosition, UnitType unitType, IGameState state)
		{
			float minDist = float.MaxValue;
			Position minBuildPosition = gridPosition;

			foreach (Position buildPosition in buildPositions)
			{
				if (Position.Distance(gridPosition, buildPosition) < minDist && state.IsBoundedAreaBuildable(unitType, buildPosition))
				{
					minDist = Position.Distance(gridPosition, buildPosition);
					minBuildPosition = buildPosition;
				}
			}

			return minBuildPosition;
		}

		/// <summary>
		/// Method compares the influence maps to the prospective build locations list
		/// to find an optimal location to build a structure.
		/// </summary>
		public Position FindBestBuildPosition(UnitType unitType, IGameState state)
		{
			float minInf = float.MaxValue;
			Position minBuildPosition = new Position(0, 0);

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
				if (influence < minInf && state.IsBoundedAreaBuildable(unitType, buildPosition))
				{
					minInf = influence;
					minBuildPosition = buildPosition;
				}
			}

			return minBuildPosition;
		}

		/// <summary>
		/// Find the closest unit to the gridPosition out of a list of units.
		/// </summary>
		public int FindClosestUnit(Position gridPosition, List<int> unitNbrs, IGameState state)
		{
			int closestUnitNbr = -1;
			float closestUnitDist = float.MaxValue;

			foreach (int unitNbr in unitNbrs)
			{
				UnitInfo? info = state.GetUnit(unitNbr);
				if (!info.HasValue) continue;
				float unitDist = Position.Distance(info.Value.GridPosition, gridPosition);

				if (!(unitDist < closestUnitDist)) continue;
				closestUnitDist = unitDist;
				closestUnitNbr = unitNbr;
			}

			return closestUnitNbr;
		}

		/// <summary>
		/// Method compares the influence maps to the list of units provided
		/// to find an optimally placed unit to attack or move to.
		/// </summary>
		public int FindBestPlacedUnit(List<int> unitNbrs, IGameState state)
		{
			int bestUnitNbr = -1;
			float bestUnitInf = float.MaxValue;

			foreach (int unitNbr in unitNbrs)
			{
				UnitInfo? info = state.GetUnit(unitNbr);
				if (!info.HasValue) continue;
				Position pos = info.Value.GridPosition;
				float influence;
				if (territoryMap[pos.X, pos.Y] < 1)
				{
					influence = enemyMap[pos.X, pos.Y] - territoryMap[pos.X, pos.Y];
				}
				else
				{
					influence = enemyMap[pos.X, pos.Y];
				}

				if (!(influence < bestUnitInf)) continue;
				bestUnitInf = influence;
				bestUnitNbr = unitNbr;
			}

			return bestUnitNbr;
		}

		/// <summary>
		/// Update heuristics for evaluating potential actions
		/// </summary>
		private void UpdateHeuristics(IGameState state)
		{
			float gold = state.MyGold;
			int count = 0;

			// Build a Base
			heuristics[count++] = Clamp01(gold / GameConstants.COST[UnitType.BASE])
								  * Clamp01((mines.Count - myBases.Count) / 2.0f);

			// Build a Barracks
			heuristics[count++] = Clamp01(gold / GameConstants.COST[UnitType.BARRACKS])
								  * gold / ((GameConstants.COST[UnitType.BARRACKS]
								  * (myBarracks.Count + ((myWarriors.Count + myArchers.Count) / 10))) + gold + 1);

			// Gather
			heuristics[count++] = (GameConstants.COST[UnitType.BASE] + GameConstants.COST[UnitType.BARRACKS])
								  / (gold + GameConstants.COST[UnitType.BASE] + GameConstants.COST[UnitType.BARRACKS]);

			// Move
			heuristics[count++] = 0;

			// Train a Pawn
			heuristics[count++] = Clamp01(myBases.Count) * Clamp01(gold / GameConstants.COST[UnitType.PAWN])
								  * 1 - ((highestCost * myPawns.Count)
								  / (gold * ((enemyPawns.Count * 3) + myPawns.Count + 1)));

			// Train a Warrior
			heuristics[count++] = Clamp01(myBarracks.Count) * Clamp01(gold / GameConstants.COST[UnitType.WARRIOR])
								  * 1 - (myWarriors.Count / ((myArchers.Count * 3) + myWarriors.Count + 1));

			// Train an Archer
			heuristics[count++] = Clamp01(myBarracks.Count) * Clamp01(gold / GameConstants.COST[UnitType.ARCHER])
								  * 1 - (myArchers.Count / ((enemyWarriors.Count + enemyArchers.Count) + myArchers.Count + 1));

			// Attack the Enemy
			heuristics[count++] = 1 - (1 / (myWarriors.Count + myArchers.Count - 1));

			// Calculate next decision
			maxIndex = heuristics.IndexOf(heuristics.Max());
		}

		/// <summary>
		/// Update the influence map based on your structures
		/// </summary>
		private void UpdateTerritoryMap(IGameState state)
		{
			for (int i = 0; i < state.MapSize.X; i++)
			{
				for (int j = 0; j < state.MapSize.Y; j++)
				{
					Position gridPosition = new Position(i, j);
					float total = 0;
					if (i >= 0 && i < state.MapSize.X && j >= 0 && j < state.MapSize.Y)
					{
						if (myBases.Count + myBarracks.Count + mines.Count > 0)
						{
							foreach (int unitID in myBases)
							{
								UnitInfo? info = state.GetUnit(unitID);
								if (!info.HasValue) continue;
								total += 3 / (Position.Distance(gridPosition, info.Value.GridPosition) - 1);
							}
							foreach (int unitID in myBarracks)
							{
								UnitInfo? info = state.GetUnit(unitID);
								if (!info.HasValue) continue;
								total += 2 / (Position.Distance(gridPosition, info.Value.GridPosition) - 1);
							}
							foreach (int mineID in mines)
							{
								UnitInfo? info = state.GetUnit(mineID);
								if (!info.HasValue) continue;
								total += 2 / (Position.Distance(gridPosition, info.Value.GridPosition) - 1);
							}
							total /= (myBases.Count * 3) + (myBarracks.Count * 2) + (mines.Count * 2);
						}
					}
					else
					{
						total = 1;
					}
					territoryMap[i, j] = total;
				}
			}
		}

		/// <summary>
		/// Update the influence map based on the opponent's structures
		/// </summary>
		private void UpdateEnemyMap(IGameState state)
		{
			for (int i = 0; i < state.MapSize.X; i++)
			{
				for (int j = 0; j < state.MapSize.Y; j++)
				{
					Position gridPosition = new Position(i, j);
					float total = 0;
					if (i >= 0 && i < state.MapSize.X && j >= 0 && j < state.MapSize.Y)
					{
						if (enemyBases.Count + enemyBarracks.Count > 0)
						{
							foreach (int unitID in enemyBases)
							{
								UnitInfo? info = state.GetUnit(unitID);
								if (!info.HasValue) continue;
								total += 3 / (Position.Distance(gridPosition, info.Value.GridPosition) - 1);
							}
							foreach (int unitID in enemyBarracks)
							{
								UnitInfo? info = state.GetUnit(unitID);
								if (!info.HasValue) continue;
								total += 2 / (Position.Distance(gridPosition, info.Value.GridPosition) - 1);
							}
							total /= (enemyBases.Count * 3) + (enemyBarracks.Count * 2);
						}
					}
					else
					{
						total = 1;
					}
					enemyMap[i, j] = total;
				}
			}
		}

		/// <summary>
		/// Process the pawns
		/// </summary>
		public void ProcessPawns(IGameState state, IAgentActions actions)
		{
			foreach (int pawn in myPawns)
			{
				UnitInfo? info = state.GetUnit(pawn);
				if (info.HasValue && info.Value.CurrentAction == UnitAction.IDLE)
				{
					UpdateTerritoryMap(state);
					UpdateEnemyMap(state);
					if (maxIndex == 2)
					{
						Position toBuild = FindBestBuildPosition(UnitType.BASE, state);
						if (toBuild != new Position(0, 0))
						{
							actions.Build(pawn, toBuild, UnitType.BASE);
						}
					}
					else if (maxIndex == 3)
					{
						Position toBuild;
						if (enemyBases.Count > 0)
						{
							int closestEnemyBase = FindClosestUnit(info.Value.GridPosition, enemyBases, state);
							UnitInfo? enemyBaseInfo = state.GetUnit(closestEnemyBase);
							if (enemyBaseInfo.HasValue)
							{
								toBuild = FindClosestBuildPosition(
									enemyBaseInfo.Value.GridPosition,
									UnitType.BARRACKS, state);
							}
							else
							{
								toBuild = FindBestBuildPosition(UnitType.BARRACKS, state);
							}
						}
						else
						{
							toBuild = FindBestBuildPosition(UnitType.BARRACKS, state);
						}
						if (toBuild != new Position(0, 0))
						{
							actions.Build(pawn, toBuild, UnitType.BARRACKS);
						}
					}
					else if (mainBaseNbr >= 0 && mainMineNbr >= 0)
					{
						UnitInfo? mineInfo = state.GetUnit(mainMineNbr);
						UnitInfo? baseInfo = state.GetUnit(mainBaseNbr);
						if (mineInfo.HasValue && baseInfo.HasValue)
						{
							actions.Gather(pawn, mainMineNbr, mainBaseNbr);
						}
					}
				}
			}
		}

		/// <summary>
		/// Process the bases
		/// </summary>
		public void ProcessBases(IGameState state, IAgentActions actions)
		{
			foreach (int baseNbr in myBases)
			{
				UnitInfo? info = state.GetUnit(baseNbr);
				if (info.HasValue && info.Value.IsBuilt
					&& info.Value.CurrentAction == UnitAction.IDLE && maxIndex == 5)
				{
					actions.Train(baseNbr, UnitType.PAWN);
				}
			}
		}

		/// <summary>
		/// Process the barracks
		/// </summary>
		public void ProcessBarracks(IGameState state, IAgentActions actions)
		{
			foreach (int barracksNbr in myBarracks)
			{
				UnitInfo? info = state.GetUnit(barracksNbr);
				if (info.HasValue && info.Value.IsBuilt
					&& info.Value.CurrentAction == UnitAction.IDLE
					&& maxIndex == 6)
				{
					actions.Train(barracksNbr, UnitType.WARRIOR);
					lastFighterWasWarrior = true;
				}
				else if (info.HasValue && info.Value.IsBuilt
						 && info.Value.CurrentAction == UnitAction.IDLE
						 && maxIndex == 7)
				{
					actions.Train(barracksNbr, UnitType.ARCHER);
					lastFighterWasWarrior = false;
				}
			}
		}

		/// <summary>
		/// Process the warriors
		/// </summary>
		public void ProcessWarriors(IGameState state, IAgentActions actions)
		{
			foreach (int warriorNbr in myWarriors)
			{
				UnitInfo? info = state.GetUnit(warriorNbr);
				if (info.HasValue && info.Value.CurrentAction == UnitAction.IDLE)
				{
					Position warriorPos = info.Value.GridPosition;
					bool tooClose = false;
					foreach (int enemy in enemyWarriors)
					{
						UnitInfo? enemyInfo = state.GetUnit(enemy);
						if (!enemyInfo.HasValue) continue;
						var size = GameConstants.UNIT_SIZE[UnitType.WARRIOR];
						float sqrMag = size.X * size.X + size.Y * size.Y;
						if (Position.Distance(warriorPos, enemyInfo.Value.GridPosition)
							< sqrMag && !tooClose)
						{
							tooClose = true;
							actions.Attack(warriorNbr, enemy);
						}
					}
					if (enemyArchers.Count > 0 && !tooClose)
					{
						int target = FindBestPlacedUnit(enemyArchers, state);
						if (target >= 0) actions.Attack(warriorNbr, target);
					}
					else if (enemyWarriors.Count > 0 && !tooClose)
					{
						int target = FindBestPlacedUnit(enemyWarriors, state);
						if (target >= 0) actions.Attack(warriorNbr, target);
					}
					else if (enemyBases.Count > 0 && !tooClose)
					{
						int target = FindBestPlacedUnit(enemyBases, state);
						if (target >= 0) actions.Attack(warriorNbr, target);
					}
					else if (enemyPawns.Count > 0 && !tooClose)
					{
						int target = FindBestPlacedUnit(enemyPawns, state);
						if (target >= 0) actions.Attack(warriorNbr, target);
					}
					else if (enemyBarracks.Count > 0 && !tooClose)
					{
						int target = FindBestPlacedUnit(enemyBarracks, state);
						if (target >= 0) actions.Attack(warriorNbr, target);
					}
					else if (enemyArchery.Count > 0 && !tooClose)
					{
						int target = FindBestPlacedUnit(enemyArchery, state);
						if (target >= 0) actions.Attack(warriorNbr, target);
					}
					else if (enemyLancers.Count > 0 && !tooClose)
					{
						int target = FindBestPlacedUnit(enemyLancers, state);
						if (target >= 0) actions.Attack(warriorNbr, target);
					}
					else if (enemyTowers.Count > 0 && !tooClose)
					{
						int target = FindBestPlacedUnit(enemyTowers, state);
						if (target >= 0) actions.Attack(warriorNbr, target);
					}
				}
			}
		}

		/// <summary>
		/// Process archers
		/// </summary>
		public void ProcessArchers(IGameState state, IAgentActions actions)
		{
			foreach (int archerNbr in myArchers)
			{
				UnitInfo? info = state.GetUnit(archerNbr);
				if (info.HasValue && info.Value.CurrentAction == UnitAction.IDLE)
				{
					if (enemyWarriors.Count > 0)
					{
						int target = FindBestPlacedUnit(enemyWarriors, state);
						if (target >= 0) actions.Attack(archerNbr, target);
					}
					else if (enemyArchers.Count > 0)
					{
						int target = FindBestPlacedUnit(enemyArchers, state);
						if (target >= 0) actions.Attack(archerNbr, target);
					}
					else if (enemyPawns.Count > 0)
					{
						int target = FindBestPlacedUnit(enemyPawns, state);
						if (target >= 0) actions.Attack(archerNbr, target);
					}
					else if (enemyBases.Count > 0)
					{
						int target = FindBestPlacedUnit(enemyBases, state);
						if (target >= 0) actions.Attack(archerNbr, target);
					}
					else if (enemyBarracks.Count > 0)
					{
						int target = FindBestPlacedUnit(enemyBarracks, state);
						if (target >= 0) actions.Attack(archerNbr, target);
					}
					else if (enemyArchery.Count > 0)
					{
						int target = FindBestPlacedUnit(enemyArchery, state);
						if (target >= 0) actions.Attack(archerNbr, target);
					}
					else if (enemyLancers.Count > 0)
					{
						int target = FindBestPlacedUnit(enemyLancers, state);
						if (target >= 0) actions.Attack(archerNbr, target);
					}
					else if (enemyTowers.Count > 0)
					{
						int target = FindBestPlacedUnit(enemyTowers, state);
						if (target >= 0) actions.Attack(archerNbr, target);
					}
				}
			}
		}

		private static float Clamp01(float x)
		{
			return Math.Max(0f, Math.Min(1f, x));
		}

		#endregion

		#region Public Methods

		/// <summary>
		/// Called before each match between two agents.
		/// </summary>
		public override void InitializeMatch()
		{
			// Match initialization
		}

		/// <summary>
		/// Called at the beginning of each round in a match.
		/// </summary>
		public override void InitializeRound(IGameState state)
		{
			base.InitializeRound(state);

			lastFighterWasWarrior = false;

			// Initialize the list of heuristics
			heuristics = new List<float>();
			for (int i = 0; i < 8; i++)
			{
				heuristics.Add(0.0f);
			}

			// Determine cost for the most expensive type of unit
			highestCost = 0.0f;
			foreach (float cost in GameConstants.COST.Values)
			{
				if (cost > highestCost)
				{
					highestCost = cost;
				}
			}

			// Initialize the influence maps
			territoryMap = new float[state.MapSize.X, state.MapSize.Y];
			enemyMap = new float[state.MapSize.X, state.MapSize.Y];
			UpdateTerritoryMap(state);
			UpdateEnemyMap(state);
		}

		/// <summary>
		/// Called at the end of each round before remaining units are
		/// destroyed to allow the agent to observe the "win/loss" state
		/// </summary>
		public override void Learn(IGameState state)
		{
			// Learning not yet implemented
		}

		/// <summary>
		/// Update the GameManager - called once per frame
		/// </summary>
		public override void Update(IGameState state, IAgentActions actions)
		{
			UpdateGameState(state);

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
				UnitInfo? baseInfo = state.GetUnit(mainBaseNbr);
				if (baseInfo.HasValue)
				{
					mainMineNbr = FindClosestUnit(baseInfo.Value.GridPosition, mines, state);
				}
			}

			// Update heuristic values for decision making
			UpdateHeuristics(state);

			// Process all of the units, prioritize building new structures over
			// training units in terms of spending gold
			ProcessPawns(state, actions);

			ProcessWarriors(state, actions);

			ProcessArchers(state, actions);

			ProcessBarracks(state, actions);

			ProcessBases(state, actions);
		}

		#endregion
	}
}
