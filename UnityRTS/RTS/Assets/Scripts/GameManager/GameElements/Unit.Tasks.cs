using System.Collections.Generic;
using AgentSDK;
using GameManager.EnumTypes;
using UnityEngine;

namespace GameManager.GameElements
{
	public partial class Unit
	{
		/// <summary>
		/// Update the attack task
		/// </summary>
		private void UpdateAttack()
		{
			// If this unit we are attacking no longer exists, go to idle
			if (AttackUnit == null
				|| GameManager.Instance.Units.GetUnit(AttackUnit.GetComponent<Unit>().UnitNbr) == null
				|| AttackUnit.GetComponent<Unit>().Health <= 0)
			{
				path.Clear();
				CurrentAction = UnitAction.IDLE;
				damage = 0.0f;
				return;
			}

			// If we're close enough to the unit, attack it and stop moving
			if (Vector3.Distance(AttackUnit.GetComponent<Unit>().CenterGridPosition, CenterGridPosition)
				< GameConstants.EffectiveAttackRange(UnitType, AttackUnit.GetComponent<Unit>().UnitType))
			{
				path.Clear();

				// Attack this unit (apply armor/damage-type multiplier)
				damage += (Time.deltaTime * Constants.DAMAGE[UnitType]
					* GameConstants.DamageMultiplier(UnitType, AttackUnit.GetComponent<Unit>().UnitType));
				if (damage > 1)
				{
					AttackUnit.GetComponent<Unit>().Health -= (int)damage;
					totalDamage += damage;
					damage -= (int)damage;
				}

				// If the enemy unit is dead, stop attacking immediately
				if (AttackUnit.GetComponent<Unit>().Health <= 0)
				{
					CurrentAction = UnitAction.IDLE;
					damage = 0.0f;
					return;
				}
			}
			// Otherwise, we're too far — pursue the assigned target
			else
			{
				TargetGridPos = AttackUnit.GetComponent<Unit>().GridPosition;

				// Use normal cooldown for pursuit re-pathing. pathUpdateCounter already
				// accumulated while the unit followed its previous path, so the first
				// re-path after a path expires is nearly instant. Subsequent retries use
				// exponential backoff, which prevents rapid pathFailCount cascading that
				// caused archers to flicker between ATTACK and IDLE.
				UpdatePath(GridPosition, TargetUnitType, TargetGridPos);

				// After sustained failure, try to find a reachable alternative target
				if (path.Count == 0 && pathFailCount >= 3)
				{
					Unit alt = FindClosestReachableEnemy(AttackUnit);
					if (alt != null)
					{
						AttackUnit = alt;
						TargetGridPos = alt.GridPosition;
						TargetUnitType = alt.UnitType;
						damage = 0.0f;
						pathFailCount = 0;
						pathBackoffMultiplier = 1;
						UpdatePath(GridPosition, TargetUnitType, TargetGridPos, forceImmediate: true);
					}
					else
					{
						// Can't reach any enemy right now — keep trying the current target.
						// The unit stays in ATTACK and retries via the cooldown/backoff mechanism.
						// It only goes IDLE when the target actually dies (handled above).
						pathFailCount = 0;
						pathBackoffMultiplier = 1;
					}
				}
			}
		}

		/// <summary>
		/// Find the closest enemy unit that we can actually pathfind to.
		/// Tries up to 3 closest enemies by distance. Returns null if none are reachable.
		/// </summary>
		private Unit FindClosestReachableEnemy(Unit excluding)
		{
			int myAgentNbr = Agent.GetComponent<AgentController>().Agent.AgentNbr;
			var enemies = new List<(Unit unit, float dist)>();

			foreach (var kvp in GameManager.Instance.Units.GetAllUnits())
			{
				Unit enemy = kvp.Value.GetComponent<Unit>();
				if (enemy == excluding) continue;
				if (enemy.Agent.GetComponent<AgentController>().Agent.AgentNbr == myAgentNbr) continue;
				if (enemy.UnitType == UnitType.MINE) continue;
				if (enemy.Health <= 0) continue;

				float dist = Vector3.Distance(enemy.CenterGridPosition, CenterGridPosition);
				enemies.Add((enemy, dist));
			}

			enemies.Sort((a, b) => a.dist.CompareTo(b.dist));

			int tried = 0;
			foreach (var (enemy, dist) in enemies)
			{
				if (tried >= 3) break;
				tried++;

				var testPath = GameManager.Instance.Map.GetPathToUnit(GridPosition, enemy.UnitType, enemy.GridPosition);
				if (testPath.Count > 0)
					return enemy;
			}

			return null;
		}

		/// <summary>
		/// Update the build task
		/// </summary>
		private void UpdateBuild()
		{
			if (path.Count != 0)
				return;

			// If we are moving to the position
			if (buildPhase == BuildPhase.TO_POSITION)
			{
				// If we're at the end of our path, start building
				if (path.Count == 0)
				{
					path.Clear();
					// Don't reset progress — resume from where the building left off
					buildPhase = BuildPhase.BUILDING;
				}
			}
			else if (buildPhase == BuildPhase.BUILDING)
			{
				if (currentBuilding == null)
				{
					CurrentAction = UnitAction.IDLE;
					return;
				}

				var buildUnit = currentBuilding.GetComponent<Unit>();
				buildUnit.BuildProgress += Time.deltaTime;

				// if we're building a unit and we have finished the task
				if (buildUnit.BuildProgress >= Constants.CREATION_TIME[taskUnitType])
				{
					buildUnit.IsBuilt = true;
					currentBuilding.GetComponent<Animator>().SetBool("IsBuilt", buildUnit.IsBuilt);
					path.Clear();
					CurrentAction = UnitAction.IDLE;
					currentBuilding = null;
				}
			}
		}

		/// <summary>
		/// Update the train task
		/// </summary>
		private void UpdateTrain()
		{
			taskTime += Time.deltaTime;

			// if we're training an agent and we have finished the task
			if (taskTime >= Constants.CREATION_TIME[taskUnitType])
			{
				var positions = GameManager.Instance.Map.GetBuildableGridPositionsNearUnit(UnitType, GridPosition);

				// Find a random cell near us to spawn the trained troop
				if (positions.Count > 0)
				{
					Vector3Int spawnPos = positions[UnityEngine.Random.Range(0, positions.Count)];
					GameManager.Instance.Units.PlaceUnit(Agent, spawnPos, taskUnitType, Color);
					path.Clear();
					CurrentAction = UnitAction.IDLE;
				}
			}
		}

		/// <summary>
		/// Hide the worker inside the mine: free its grid cell and disable its sprite.
		/// </summary>
		private void EnterMine()
		{
			isInsideMine = true;
			mineEntryGridPos = MineUnit.GetComponent<Unit>().GridPosition;

			// Free the worker's grid cell so other units can walk through
			GameManager.Instance.Map.SetAreaBuildability(UnitType, GridPosition, true);

			// Hide the worker sprite
			var sr = GetComponent<SpriteRenderer>();
			if (sr != null) sr.enabled = false;
		}

		/// <summary>
		/// Reappear at a random buildable cell neighboring the mine.
		/// </summary>
		private void ExitMine()
		{
			if (!isInsideMine) return;
			isInsideMine = false;

			// Find a random buildable neighbor of the mine
			var neighbors = GameManager.Instance.Map.GetBuildableGridPositionsNearUnit(UnitType.MINE, mineEntryGridPos);
			if (neighbors.Count > 0)
			{
				Vector3Int spawnPos = neighbors[UnityEngine.Random.Range(0, neighbors.Count)];
				GridPosition = spawnPos;
				WorldPosition = (Vector3)spawnPos + new Vector3(0.5f, 0.5f, 0);
			}
			// else: stay at current GridPosition (fallback)

			// Mark the new position as occupied
			GameManager.Instance.Map.SetAreaBuildability(UnitType, GridPosition, false);

			// Show the worker sprite
			var sr = GetComponent<SpriteRenderer>();
			if (sr != null) sr.enabled = true;
		}

		/// <summary>
		/// Update the gather task
		/// </summary>
		private void UpdateGather()
		{
			// If the assigned mine is gone and we're still heading toward it, stop immediately
			if (gatherPhase == GatherPhase.TO_MINE
				&& (MineUnit == null || MineUnit.GetComponent<Unit>().Health <= 0))
			{
				path.Clear();
				CurrentAction = UnitAction.IDLE;
				return;
			}

			if (path.Count != 0)
				return;

			// If we're headed to the mine (mine is guaranteed alive by the early check above)
			if (gatherPhase == GatherPhase.TO_MINE)
			{
				// If we've just arrived at the mine
				if (GameManager.Instance.Map.IsNeighborOfUnit(GridPosition, TargetUnitType, TargetGridPos))
				{
					gatherPhase = GatherPhase.MINING;
					minedGold = 0.0f;
					taskTime = 0f;
					EnterMine();
				}
				else
				{
					UpdatePath(GridPosition, TargetUnitType, TargetGridPos);
				}
			}
			// if we're currently mining
			else if (gatherPhase == GatherPhase.MINING)
			{
				// If the mine is empty and gone
				if (MineUnit == null || MineUnit.GetComponent<Unit>().Health <= 0)
				{
					ExitMine();

					if (BaseUnit == null)
					{
						path.Clear();
						CurrentAction = UnitAction.IDLE;
						return;
					}

					gatherPhase = GatherPhase.TO_BASE;
					TargetUnitType = UnitType.BASE;
					TargetGridPos = BaseUnit.GetComponent<Unit>().GridPosition;
					UpdatePath(GridPosition, TargetUnitType, TargetGridPos, forceImmediate: true, avoidUnits: true);
				}
				// Otherwise if there is a mine, collect totalGold
				else if (MineUnit.GetComponent<Unit>().Health > 0)
				{
					taskTime += Time.deltaTime;
					minedGold += Time.deltaTime
								 * (MiningSpeed + (MiningSpeed * Constants.MINING_BOOST
												   * GameManager.Instance.Units.GetUnitNbrsOfType(UnitType.REFINERY, Agent.GetComponent<AgentController>().Agent.AgentNbr).Count));
					if (minedGold >= 1)
					{
						MineUnit.GetComponent<Unit>().Health -= (int)minedGold;
						totalGold += (int)minedGold;
						minedGold -= (int)minedGold;
					}

					// If we've reached our mining capacity
					if (totalGold >= MiningCapacity && BaseUnit != null)
					{
						ExitMine();
						gatherPhase = GatherPhase.TO_BASE;
						TargetUnitType = UnitType.BASE;
						TargetGridPos = BaseUnit.GetComponent<Unit>().GridPosition;
						UpdatePath(GridPosition, TargetUnitType, TargetGridPos, forceImmediate: true, avoidUnits: true);
					}
					else if (totalGold >= MiningCapacity && BaseUnit == null)
					{
						ExitMine();
						path.Clear();
						CurrentAction = UnitAction.IDLE;
					}
				}
			}
			else // if (gatherPhase == GatherPhase.TO_BASE
			{
				// If we're at the base, deposit any totalGold, head back to the mine
				if (BaseUnit != null
					&& GameManager.Instance.Map.IsNeighborOfUnit(GridPosition, TargetUnitType, TargetGridPos))
				{
					Agent.GetComponent<AgentController>().Agent.Gold += totalGold;
					totalGold = 0;

					// Go back to the mine
					if (MineUnit != null)
					{
						gatherPhase = GatherPhase.TO_MINE;
						TargetUnitType = UnitType.MINE;
						TargetGridPos = MineUnit.GetComponent<Unit>().GridPosition;
						UpdatePath(GridPosition, TargetUnitType, TargetGridPos);
					}
					else
					{
						path.Clear();
						CurrentAction = UnitAction.IDLE;
					}
				}
				else if (BaseUnit != null)
				{
					TargetUnitType = UnitType.BASE;
					TargetGridPos = BaseUnit.GetComponent<Unit>().GridPosition;
					UpdatePath(GridPosition, TargetUnitType, TargetGridPos);
				}
				else if (BaseUnit == null)
				{
					path.Clear();
					CurrentAction = UnitAction.IDLE;
				}
			}
		}
	}
}
