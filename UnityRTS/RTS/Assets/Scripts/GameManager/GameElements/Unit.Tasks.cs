using System.Collections.Generic;
using AgentSDK;
using GameManager.EnumTypes;
using UnityEngine;

namespace GameManager.GameElements
{
	/// <summary>
	/// Unit partial — legacy per-frame task update methods (UpdateAttack, etc.).
	///
	/// These are the Unity-side task advancement methods that were used before the
	/// shared TickEngine was introduced. Some remain as supplementary visual/state
	/// logic that runs alongside the deterministic TickEngine updates.
	/// </summary>
	public partial class Unit
	{
		/// <summary>
		/// Update the attack task
		/// </summary>
		private void UpdateAttack()
		{
			// If this unit we are attacking no longer exists, go to idle
			var attackTarget = AttackUnit;
			if (attackTarget == null
				|| GameManager.Instance.Units.GetUnit(attackTarget.UnitNbr) == null
				|| attackTarget.Health <= 0)
			{
				path.Clear();
				pathIndex = 0;
				CurrentAction = UnitAction.IDLE;
				damage = 0.0f;
				return;
			}

			// If we're close enough to the unit, attack it and stop moving
			// Use closest-cell distance for buildings so archers fire at the nearest edge
			float distToTarget = DistanceToClosestCell(attackTarget);
			if (distToTarget < Constants.ATTACK_RANGE[UnitType] + 0.5f)
			{
				path.Clear();
				pathIndex = 0;

				// Attack this unit (shared damage formula for parity with SimGame)
				damage += AgentSDK.TaskEngine.ComputeDamagePerTick(
					UnitType, attackTarget.UnitType,
					Constants.DAMAGE[UnitType], Time.fixedDeltaTime);
				if (damage > 1)
				{
					int dmg = (int)damage;
					if (!attackTarget.IsBuilt)
					{
						// Unbuilt buildings: damage reduces construction progress.
						// Convert HP damage to build-time damage so the same total
						// damage destroys a building regardless of build state.
						float maxHp = Constants.HEALTH[attackTarget.UnitType];
						float creationTime = Constants.CREATION_TIME[attackTarget.UnitType];
						float progressDmg = dmg * creationTime / maxHp;
						attackTarget.BuildProgress -= progressDmg;
						if (attackTarget.BuildProgress <= 0)
							attackTarget.Health = 0; // triggers normal death
					}
					else
					{
						attackTarget.Health -= dmg;
					}
					totalDamage += damage;
					damage -= dmg;

					// Record damage analytics for attacker and defender
					GetRoundStats()?.RecordDamageDealt(dmg);
					var defenderStats = attackTarget.Agent?.GetComponent<AgentController>()?.Agent?.Analytics?.CurrentRound;
					defenderStats?.RecordDamageReceived(dmg);
				}

				// Spawn arrow projectile on frame 5 of the shoot animation (once per loop)
				if (UnitType == UnitType.ARCHER && animator != null && animator.runtimeAnimatorController != null)
				{
					float loopPos = animator.GetCurrentAnimatorStateInfo(0).normalizedTime % 1f;
					if (loopPos >= 0.625f && !arrowFiredThisCycle)
					{
						arrowFiredThisCycle = true;
						SpawnArrow(attackTarget);
					}
					else if (loopPos < 0.625f)
					{
						arrowFiredThisCycle = false;
					}
				}

				// If the enemy unit is dead, stop attacking immediately
				if (attackTarget.Health <= 0)
				{
					CurrentAction = UnitAction.IDLE;
					damage = 0.0f;
					return;
				}
			}
			// Otherwise, we're too far — pursue the assigned target
			else
			{
				TargetGridPos = attackTarget.GridPosition;

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
		/// Compute the distance from this unit to the closest occupied cell of the target.
		/// For 1x1 units this is equivalent to CenterGridPosition distance.
		/// For buildings, this measures to the nearest edge cell rather than the center.
		/// </summary>
		private float DistanceToClosestCell(Unit target)
		{
			var size = Constants.UNIT_SIZE[target.UnitType];
			if (size.x <= 1 && size.y <= 1)
				return Vector3.Distance(target.CenterGridPosition, CenterGridPosition);

			float bestDist = float.MaxValue;
			for (int dx = 0; dx < size.x; dx++)
			{
				for (int dy = 0; dy < size.y; dy++)
				{
					var cell = new Vector3Int(target.GridPosition.x + dx,
					                          target.GridPosition.y - dy, 0);
					float dist = Vector3.Distance((Vector3)cell, (Vector3)GridPosition);
					if (dist < bestDist)
						bestDist = dist;
				}
			}
			return bestDist;
		}

		/// <summary>
		/// Spawn a visual arrow projectile from this archer toward the target.
		/// Arrow spawns at the bow tip position on frame 5 of the shoot animation.
		/// For buildings, aims at the closest occupied cell. Adds random perturbation
		/// so arrows don't all land on the exact same pixel.
		/// </summary>
		private void SpawnArrow(Unit target)
		{
			Sprite arrowSprite = GameManager.Instance.ArrowSprite;
			if (arrowSprite == null) return;

			var arrowGo = new GameObject("Arrow");
			var sr = arrowGo.AddComponent<SpriteRenderer>();
			sr.sprite = arrowSprite;
			sr.sortingLayerName = "Agents";
			sr.sortingOrder = 0;

			// Bow tip offset on frame 5: upper-forward relative to sprite center
			float offsetX = facingRight ? 0.2f : -0.2f;
			Vector3 spawnPos = WorldPosition + new Vector3(offsetX, 0.3f, 0);

			// For buildings, aim at the closest cell; for units, aim at their center cell
			Vector3Int aimCell;
			var size = Constants.UNIT_SIZE[target.UnitType];
			if (size.x > 1 || size.y > 1)
			{
				// Find closest occupied cell of the building to this archer
				float bestDist = float.MaxValue;
				aimCell = target.CenterGridPosition;
				for (int dx = 0; dx < size.x; dx++)
				{
					for (int dy = 0; dy < size.y; dy++)
					{
						var cell = new Vector3Int(target.GridPosition.x + dx,
						                          target.GridPosition.y - dy, 0);
						float dist = Vector3.Distance((Vector3)cell, (Vector3)GridPosition);
						if (dist < bestDist)
						{
							bestDist = dist;
							aimCell = cell;
						}
					}
				}
			}
			else
			{
				aimCell = target.CenterGridPosition;
			}

			// Cell center + random perturbation within the cell
			Vector3 targetPos = (Vector3)aimCell + new Vector3(
				0.5f + UnityEngine.Random.Range(-0.3f, 0.3f),
				0.5f + UnityEngine.Random.Range(-0.3f, 0.3f), 0);

			// Inset by half a cell from building edges so arrows don't land in empty corners
			if (size.x > 1 || size.y > 1)
			{
				float inset = 0.5f;
				float left   = target.GridPosition.x + inset;
				float right  = target.GridPosition.x + size.x - inset;
				float bottom = target.GridPosition.y - size.y + 1 + inset;
				float top    = target.GridPosition.y + 1 - inset;
				targetPos.x = Mathf.Clamp(targetPos.x, left, right);
				targetPos.y = Mathf.Clamp(targetPos.y, bottom, top);
			}

			var arrow = arrowGo.AddComponent<ArrowProjectile>();
			arrow.Launch(spawnPos, targetPos, ARROW_SPEED);
			arrow.SetTarget(target.transform, GameManager.Instance.ExplosionAnimatorController);
			arrow.AttachFire(GameManager.Instance.FireAnimatorController);
		}

		/// <summary>
		/// Spawn a gold nugget at the pickaxe head that arcs up and bounces down to the pawn.
		/// Also triggers the mine's highlight animation.
		/// </summary>
		private void SpawnGoldNugget()
		{
			Sprite goldSprite = GameManager.Instance.GoldResourceSprite;
			if (goldSprite == null) return;

			// Trigger the mine's highlight animation from the beginning
			var mineUnit = MineUnit;
			if (mineUnit != null)
			{
				var mineAnimator = mineUnit.GetComponent<Animator>();
				if (mineAnimator != null && mineAnimator.runtimeAnimatorController != null)
					mineAnimator.Play(0, 0, 0f);
			}

			var nuggetGo = new GameObject("GoldNugget");
			var sr = nuggetGo.AddComponent<SpriteRenderer>();
			sr.sprite = goldSprite;
			sr.sortingLayerName = "Agents";
			sr.sortingOrder = 0;

			// Pickaxe tip at frame 3 downswing: pixel (150,115) in 192x192 frame, PPU=80, center pivot
			// Y offset raised so the coin's bottom edge aligns with the tip of the pickaxe
			float tipX = facingRight ? 0.675f : -0.675f;
			Vector3 spawnPos = WorldPosition + new Vector3(tipX, -0.14f, 0);

			// Target: pawn's center (slightly above feet)
			Vector3 targetPos = WorldPosition + new Vector3(0f, 0.2f, 0);

			var nugget = nuggetGo.AddComponent<GoldNuggetProjectile>();
			nugget.Launch(spawnPos, targetPos);
		}

		/// <summary>
		/// Find the closest enemy unit that we can actually pathfind to.
		/// Tries up to 3 closest enemies by distance. Returns null if none are reachable.
		/// </summary>
		private Unit FindClosestReachableEnemy(Unit excluding)
		{
			int myAgentNbr = OwnerAgentNbr;
			var enemies = new List<(Unit unit, float dist)>();

			foreach (var kvp in GameManager.Instance.Units.GetAllUnits())
			{
				Unit enemy = kvp.Value.GetComponent<Unit>();
				if (enemy == excluding) continue;
				if (enemy.OwnerAgentNbr == myAgentNbr) continue;
				if (enemy.UnitType == UnitType.MINE) continue;
				if (enemy.Health <= 0) continue;

				float dist = DistanceToClosestCell(enemy);
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
				pathIndex = 0;
					// Transition to BUILDING and fall through to start the timer
					// on the same tick (matches SimGame's AdvanceBuild which
					// decrements immediately after path consumption).
					buildPhase = BuildPhase.BUILDING;
				}
			}
			if (buildPhase == BuildPhase.BUILDING)
			{
				if (currentBuilding == null)
				{
					CurrentAction = UnitAction.IDLE;
					return;
				}

				var buildUnit = currentBuilding.GetComponent<Unit>();
				buildUnit.BuildProgress += Time.fixedDeltaTime;

				// Lerp building opacity from 30% to 70% based on progress
				var buildSprite = currentBuilding.GetComponent<SpriteRenderer>();
				if (buildSprite != null)
				{
					float progress = Mathf.Clamp01(buildUnit.BuildProgress / Constants.CREATION_TIME[taskUnitType]);
					var c = buildSprite.color;
					c.a = 0.3f + progress * 0.4f;
					buildSprite.color = c;
				}

				// if we're building a unit and we have finished the task
				if (buildUnit.BuildProgress >= Constants.CREATION_TIME[taskUnitType])
				{
					buildUnit.IsBuilt = true;
					buildUnit.buildPulseFrames = BUILD_PULSE_TOTAL;
					GetRoundStats()?.RecordBuildingConstructed(taskUnitType);
					// Set full opacity on completion
					if (buildSprite != null)
					{
						var c = buildSprite.color;
						c.a = 1.0f;
						buildSprite.color = c;
					}
					var buildAnimator = currentBuilding.GetComponent<Animator>();
					if (buildAnimator != null && buildAnimator.runtimeAnimatorController != null)
						buildAnimator.SetBool("IsBuilt", buildUnit.IsBuilt);
					path.Clear();
				pathIndex = 0;
					CurrentAction = UnitAction.IDLE;
					currentBuilding = null;
				}
			}
		}

		/// <summary>
		/// Update the repair task — heals the building at the same rate it was built.
		/// </summary>
		private void UpdateRepair()
		{
			if (path.Count != 0)
				return;

			if (buildPhase == BuildPhase.TO_POSITION)
			{
				if (path.Count == 0)
				{
					path.Clear();
				pathIndex = 0;
					buildPhase = BuildPhase.BUILDING;
				}
			}
			if (buildPhase == BuildPhase.BUILDING)
			{
				if (currentBuilding == null)
				{
					CurrentAction = UnitAction.IDLE;
					return;
				}

				var buildUnit = currentBuilding.GetComponent<Unit>();
				float maxHp = Constants.HEALTH[buildUnit.UnitType];

				// Building was destroyed while repairing
				if (buildUnit.Health <= 0)
				{
					currentBuilding = null;
					CurrentAction = UnitAction.IDLE;
					return;
				}

				// Heal at 110% of the build rate (shared formula for parity)
				float repairAmount = AgentSDK.TaskEngine.ComputeRepairPerTick(
					buildUnit.UnitType, Constants.CREATION_TIME[buildUnit.UnitType], Time.fixedDeltaTime);
				buildUnit.Health = Mathf.Min(buildUnit.Health + repairAmount, maxHp);

				// Done — building is at full health
				if (buildUnit.Health >= maxHp)
				{
					path.Clear();
				pathIndex = 0;
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
			taskTime += Time.fixedDeltaTime;

			// if we're training an agent and we have finished the task
			if (taskTime >= Constants.CREATION_TIME[taskUnitType])
			{
				var positions = GameManager.Instance.Map.GetBuildableGridPositionsNearUnit(UnitType, GridPosition);

				// Spawn at the first available cell (deterministic, matches SimGame)
				if (positions.Count > 0)
				{
					Vector3Int spawnPos = positions[0];
					GameManager.Instance.Units.PlaceUnit(Agent, spawnPos, taskUnitType, Color);
					var prodStats = GetRoundStats();
					prodStats?.RecordUnitProduced(taskUnitType);
					if (Constants.CAN_ATTACK[taskUnitType])
						prodStats?.RecordMilestone("MILITARY", GameManager.Instance.TotalGameTime);
					path.Clear();
				pathIndex = 0;
					CurrentAction = UnitAction.IDLE;
				}
			}
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
				pathIndex = 0;
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
				}
				else
				{
					UpdatePath(GridPosition, TargetUnitType, TargetGridPos, forceImmediate: true);
				}
			}
			// if we're currently mining
			else if (gatherPhase == GatherPhase.MINING)
			{
				// If the mine is empty and gone
				if (MineUnit == null || MineUnit.GetComponent<Unit>().Health <= 0)
				{
					if (BaseUnit == null)
					{
						path.Clear();
				pathIndex = 0;
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
					taskTime += Time.fixedDeltaTime;
					minedGold += Time.fixedDeltaTime * MiningSpeed;
					if (minedGold >= 1)
					{
						MineUnit.GetComponent<Unit>().Health -= (int)minedGold;
						totalGold += (int)minedGold;
						minedGold -= (int)minedGold;
					}

					// Spawn gold nugget at the pickaxe downswing (frame 3 of 6, normalized ~0.5)
					if (animator != null && animator.runtimeAnimatorController != null)
					{
						float loopPos = animator.GetCurrentAnimatorStateInfo(0).normalizedTime % 1f;
						if (loopPos >= 0.5f && !goldNuggetSpawnedThisCycle)
						{
							goldNuggetSpawnedThisCycle = true;
							SpawnGoldNugget();
						}
						else if (loopPos < 0.5f)
						{
							goldNuggetSpawnedThisCycle = false;
						}
					}

					// If we've reached our mining capacity
					if (totalGold >= MiningCapacity && BaseUnit != null)
					{
						gatherPhase = GatherPhase.TO_BASE;
						TargetUnitType = UnitType.BASE;
						TargetGridPos = BaseUnit.GetComponent<Unit>().GridPosition;
						UpdatePath(GridPosition, TargetUnitType, TargetGridPos, forceImmediate: true, avoidUnits: true);
					}
					else if (totalGold >= MiningCapacity && BaseUnit == null)
					{
						path.Clear();
				pathIndex = 0;
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
					var gatherStats = GetRoundStats();
					gatherStats?.RecordGoldGathered(totalGold);
					gatherStats?.UpdatePeakGold(Agent.GetComponent<AgentController>().Agent.Gold);
					totalGold = 0;

					// Go back to the mine
					if (MineUnit != null)
					{
						gatherPhase = GatherPhase.TO_MINE;
						TargetUnitType = UnitType.MINE;
						TargetGridPos = MineUnit.GetComponent<Unit>().GridPosition;
						UpdatePath(GridPosition, TargetUnitType, TargetGridPos, forceImmediate: true);
					}
					else
					{
						path.Clear();
				pathIndex = 0;
						CurrentAction = UnitAction.IDLE;
					}
				}
				else if (BaseUnit != null)
				{
					TargetUnitType = UnitType.BASE;
					TargetGridPos = BaseUnit.GetComponent<Unit>().GridPosition;
					UpdatePath(GridPosition, TargetUnitType, TargetGridPos, forceImmediate: true);
				}
				else if (BaseUnit == null)
				{
					path.Clear();
				pathIndex = 0;
					CurrentAction = UnitAction.IDLE;
				}
			}
		}

		/// <summary>
		/// Update the heal task — move toward target then apply heal when in range.
		/// </summary>
		private void UpdateHeal()
		{
			var healTarget = GameManager.Instance.Units.GetUnit(healTargetNbr);
			if (healTarget == null || healTarget.Health <= 0)
			{
				path.Clear();
				pathIndex = 0;
				CurrentAction = UnitAction.IDLE;
				healTargetNbr = -1;
				return;
			}

			float dist = DistanceToClosestCell(healTarget);
			float healRange = Constants.HEAL_RANGE[UnitType];

			if (dist < healRange + 0.5f)
			{
				path.Clear();
				pathIndex = 0;

				// Check mana and threshold via shared logic
				if (!AgentSDK.TaskEngine.CanHeal(Mana, healTarget.Health, healTarget.UnitType))
				{
					CurrentAction = UnitAction.IDLE;
					healTargetNbr = -1;
					return;
				}

				// Apply heal via shared formula
				float healAmount = AgentSDK.TaskEngine.ComputeHealAmount(healTarget.UnitType);
				float targetMaxHealth = Constants.HEALTH[healTarget.UnitType];
				healTarget.Health = Mathf.Min(healTarget.Health + healAmount, targetMaxHealth);
				Mana -= GameConstants.MANA_COST;

				// Spawn heal VFX on target
				SpawnHealEffect(healTarget);

				GetCmdLog()?.LogCommand("HEAL", $"MONK#{UnitNbr} -> {healTarget.UnitType}#{healTarget.UnitNbr}",
					$"HEALED ({healAmount:F0} HP, target now {healTarget.Health:F0}/{targetMaxHealth:F0}, mana={Mana:F0})");

				CurrentAction = UnitAction.IDLE;
				healTargetNbr = -1;
			}
		}

		/// <summary>
		/// Spawn a heal VFX on the target unit.
		/// </summary>
		internal void SpawnHealEffect(Unit target)
		{
			var controller = GameManager.Instance.HealEffectAnimatorController;
			if (controller == null) return;

			var healGo = new GameObject("HealEffect");
			healGo.transform.SetParent(target.transform);
			healGo.transform.localPosition = new Vector3(0f, 0.5f, 0f);
			healGo.transform.localScale = Vector3.one;

			var sr = healGo.AddComponent<SpriteRenderer>();
			sr.sortingLayerName = "Agents";
			sr.sortingOrder = 2; // above units (sortingOrder 0-1) but Y-sorted with them
			sr.spriteSortPoint = SpriteSortPoint.Pivot;

			var anim = healGo.AddComponent<Animator>();
			anim.runtimeAnimatorController = controller;

			// Auto-destroy after 1 second (animation duration)
			Object.Destroy(healGo, 1.1f);
		}
	}
}
