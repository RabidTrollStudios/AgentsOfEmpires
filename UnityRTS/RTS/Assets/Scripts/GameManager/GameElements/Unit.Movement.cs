using System;
using System.Collections.Generic;
using System.Linq;
using AgentSDK;
using GameManager.EnumTypes;
using GameManager.Graph;
using UnityEngine;
using UnityEngine.UI;

namespace GameManager.GameElements
{
	public partial class Unit
	{
		#region Update Methods

		/// <summary>
		/// Update this unit
		/// </summary>
		internal void Update()
		{
			MineUnit = GameManager.Instance.Units.GetUnit(mineUnit);
			BaseUnit = GameManager.Instance.Units.GetUnit(baseUnit);

			pathUpdateCounter++;
			HasDebugging = GameManager.Instance.HasUnitDebugging;

			UpdateDebuggingInfo();
			UpdatePathVisualization();
			UpdateTargetVisualization();

			// If this unit is dead, destroy it
			if (Health <= 0)
			{
				// Release building reference — progress is safely on the building itself
				if (currentBuilding != null)
					currentBuilding.GetComponent<Unit>().ActiveBuilderNbr = -1;
				currentBuilding = null;
				GameManager.Instance.Units.DestroyUnit(gameObject);
			}
			// Otherwise, if this unit is idle
			else if (CurrentAction == UnitAction.IDLE)
			{
				if (currentBuilding != null)
					currentBuilding.GetComponent<Unit>().ActiveBuilderNbr = -1;
				currentBuilding = null;
				path.Clear();
				TargetGridPos = GridPosition; // TODO
				TargetUnitType = UnitType.WORKER;
				AttackUnit = null;
				MineUnit = null;
				BaseUnit = null;
			}
			else //if (!isWandering)
			{
				// If we were ordered to gather and we can gather
				if (CurrentAction == UnitAction.GATHER && CanGather)
				{
					UpdateGather();
				}
				else if (CurrentAction == UnitAction.ATTACK && CanAttack)
				{
					UpdateAttack();
				}
				else if (CurrentAction == UnitAction.BUILD && CanBuild)
				{
					UpdateBuild();
				}
				else if (CurrentAction == UnitAction.MOVE && CanMove)
				{
					UpdateMove();
				}
				else if (CurrentAction == UnitAction.TRAIN && CanTrain)
				{
					UpdateTrain();
				}
			}

		}

		/// <summary>
		/// Apply action-based tinting after the Animator has finished
		/// updating the sprite, so our color override is not clobbered.
		/// </summary>
		internal void LateUpdate()
		{
			UpdateStateColor();
		}

		/// <summary>
		/// Tint the sprite based on the unit's current action for visual debugging.
		/// IDLE = normal, MOVE = blue, ATTACK = red.
		/// Orc archers/soldiers use cyan (MOVE) and magenta (ATTACK) for contrast
		/// against their darker base sprites.
		/// </summary>
		private void UpdateStateColor()
		{
			bool showAttack = CurrentAction == UnitAction.ATTACK && GameManager.Instance.HasAttackTint;
			bool showMove = CurrentAction == UnitAction.MOVE && GameManager.Instance.HasMoveTint;
			bool showGather = CurrentAction == UnitAction.GATHER && GameManager.Instance.HasGatherTint;

			// Don't show indicators when the worker is hidden inside a mine
			if (isInsideMine)
			{
				showAttack = false;
				showMove = false;
				showGather = false;
			}

			bool anyIndicator = showAttack || showMove || showGather;

			if (attackIndicator != null) attackIndicator.enabled = showAttack;
			if (moveIndicator != null) moveIndicator.enabled = showMove;
			if (gatherIndicator != null) gatherIndicator.enabled = showGather;

			// Fade the root unit sprite to 50% alpha when an indicator is active.
			// Only modify the root SpriteRenderer to avoid interfering with
			// Animator-controlled child renderers.
			float alpha = anyIndicator ? 0.5f : 1f;
			var rootSr = GetComponent<SpriteRenderer>();
			if (rootSr != null && rootSr.enabled)
			{
				var c = rootSr.color;
				if (System.Math.Abs(c.a - alpha) > 0.01f)
					rootSr.color = new Color(c.r, c.g, c.b, alpha);
			}
		}

		/// <summary>
		/// Map the current velocity to the direction the unit is moving
		/// South is 0, directions are counter-clockwise
		/// </summary>
		/// <returns></returns>
		private void MapVelocityToDirection()
		{
			if (animator == null)
				return;

			// TODO: Keep working on this to flesh out all the directions, this math seems wrong....
			// If south
			if (Math.Abs(velocity.x - 0) < .1f && Math.Abs(velocity.y - 1) < .1f)
			{
				animator.SetInteger("Direction", 0);
			}
			else if (Math.Abs(velocity.x - 0) > .1f && Math.Abs(velocity.y - 1) < .1f)
			{
				animator.SetInteger("Direction", 0);
			}

		}

		internal void FixedUpdate()
		{
			// If we have a path, move along it
			if (path.Count > 0)
			{
				// If the next cell in the path is buildable (truly empty), move forward
				Vector3Int nextTarget = path[0];
				if (GameManager.Instance.Map.IsGridPositionBuildable(nextTarget))
				{
					localAvoidWaitFrames = 0;

					// Units snap to cell centers: grid cell (i,j) has world center (i+0.5, j+0.5)
					Vector3 nextCenter = (Vector3)nextTarget + new Vector3(0.5f, 0.5f, 0);
					velocity = nextCenter - WorldPosition;
					velocity = Utility.SafeNormalize(velocity);

					// Determine how far we are from our current target
					float distToTarget = Vector3.Distance(nextCenter, WorldPosition);

					// If we're close to our target but we're in the middle of the path
					// Move to the target and then move toward the next point
					if (distToTarget <= Speed)
					{
						GameManager.Instance.Map.SetAreaBuildability(gameObject.GetComponent<Unit>().UnitType, nextTarget, false);
						GameManager.Instance.Map.SetAreaBuildability(gameObject.GetComponent<Unit>().UnitType, GridPosition, true);
						GridPosition = nextTarget;
						WorldPosition = nextCenter;
						path.RemoveAt(0);
						if (path.Count > 0)
						{
							nextTarget = path[0];
							nextCenter = (Vector3)nextTarget + new Vector3(0.5f, 0.5f, 0);
							velocity = Utility.SafeNormalize(nextCenter - WorldPosition);
							WorldPosition += velocity * (Speed - distToTarget);
						}
					}
					// Otherwise, we're just moving along the path and not close to our target
					else
					{
						WorldPosition += velocity * Speed;
					}
				}
				// Next cell is occupied — use local avoidance for mobile units, full re-path for terrain/buildings
				else
				{
					// Walkable but not buildable = blocked by a mobile unit (temporary obstacle)
					if (GameManager.Instance.Map.IsGridPositionWalkable(nextTarget))
					{
						// For MOVE commands: if close to the destination and blocked by a mobile
						// unit, stop here rather than endlessly sidestepping and re-pathing.
						// This prevents rally/retreat units from wandering around congested areas.
						if (CurrentAction == UnitAction.MOVE)
						{
							float distToTarget = Vector3.Distance((Vector3)GridPosition, (Vector3)TargetGridPos);
							if (path.Count == 1 || distToTarget <= 3.0f)
							{
								path.Clear();
								localAvoidWaitFrames = 0;
								return;
							}
						}

						localAvoidWaitFrames++;

						// Phase 1: Wait a few frames — the blocker may move on its own.
						// MOVE actions (retreats, rallies) skip the wait and try to sidestep immediately
						// so retreating units aren't pinned down by adjacent blockers.
						if (CurrentAction != UnitAction.MOVE && localAvoidWaitFrames <= 3)
							return;

						// Phase 2: Try to sidestep around the blocker
						Vector3Int? sidestep = FindLocalSidestep();
						if (sidestep.HasValue)
						{
							path.Insert(0, sidestep.Value);
							localAvoidWaitFrames = 0;
							return;
						}

						// Phase 3: After extended wait, re-path avoiding unit-occupied cells
						// so the unit finds a route around the congestion.
						// MOVE actions use a shorter wait threshold for faster response.
						int rePathThreshold = CurrentAction == UnitAction.MOVE ? 4 : 10;
						if (localAvoidWaitFrames > rePathThreshold)
						{
							// For MOVE actions: preserve the existing path if re-path fails.
							// UpdatePath replaces path unconditionally; an empty result would
							// cause UpdateMove → IDLE, cutting retreats/rallies short.
							if (CurrentAction == UnitAction.MOVE)
							{
								var savedPath = new List<Vector3Int>(path);
								UpdatePath(GridPosition, TargetUnitType, TargetGridPos, forceImmediate: true, avoidUnits: true);
								if (path.Count == 0)
									path = savedPath;
							}
							else
							{
								UpdatePath(GridPosition, TargetUnitType, TargetGridPos, forceImmediate: true, avoidUnits: true);
							}
							localAvoidWaitFrames = 0;
						}
					}
					else
					{
						// Not walkable = terrain or building — full re-path immediately
						localAvoidWaitFrames = 0;
						UpdatePath(GridPosition, TargetUnitType, TargetGridPos);
					}
				}
			}
			else if (CanMove)
			{
				// Snap to grid cell center when not actively moving
				WorldPosition = (Vector3)GridPosition + new Vector3(0.5f, 0.5f, 0);
			}
		}

		/// <summary>
		/// Find a free adjacent cell that makes progress toward the next path waypoint.
		/// Returns null if no suitable sidestep exists.
		/// </summary>
		private Vector3Int? FindLocalSidestep()
		{
			// Aim toward the cell after the blocked one, or the blocked cell itself
			Vector3Int target = path.Count > 1 ? path[1] : path[0];

			Vector3Int bestStep = Vector3Int.zero;
			float bestDist = float.MaxValue;
			bool found = false;

			for (int dx = -1; dx <= 1; dx++)
			{
				for (int dy = -1; dy <= 1; dy++)
				{
					if (dx == 0 && dy == 0) continue;
					Vector3Int candidate = GridPosition + new Vector3Int(dx, dy, 0);
					if (Utility.IsValidGridLocation(candidate)
						&& GameManager.Instance.Map.IsGridPositionBuildable(candidate))
					{
						float dist = Vector3.Distance((Vector3)candidate, (Vector3)target);
						if (dist < bestDist)
						{
							bestDist = dist;
							bestStep = candidate;
							found = true;
						}
					}
				}
			}

			return found ? (Vector3Int?)bestStep : null;
		}

		private void UpdateDebuggingInfo()
		{
			// Enable/disable debugging
			gameObject.GetComponentInChildren<Canvas>().enabled = HasDebugging;
			if (HasDebugging)
			{
				var textAreas = gameObject.GetComponentsInChildren<Text>().ToList();
				foreach (Text textArea in textAreas)
				{
					if (textArea.name == "Unit Number")
					{
						textArea.text = UnitNbr.ToString();
					}
					else if (textArea.name == "State Label")
					{
						textArea.text = CurrentAction.ToString()[0].ToString();
					}
					else if (textArea.name == "State Variable")
					{
						switch (CurrentAction)
						{
							case UnitAction.IDLE:
								textArea.text = "";
								break;
							case UnitAction.ATTACK:
								textArea.text = totalDamage.ToString("0.0");
								break;
							case UnitAction.BUILD:
								textArea.text = taskTime.ToString("0.0");
								break;
							case UnitAction.GATHER:
								textArea.text = totalGold.ToString("0.0");
								break;
							case UnitAction.MOVE:
								textArea.text = path.Count.ToString();
								break;
							case UnitAction.TRAIN:
								textArea.text = taskTime.ToString("0.0");
								break;
						}
					}
					else if (textArea.name == "Health Value")
					{
						textArea.text = Health.ToString("0.0");
					}
				}
			}
		}

		/// <summary>
		/// Update the path to the target with exponential backoff on failure.
		/// First retry is fast (transient blocks), subsequent retries slow down.
		/// After repeated failures, the unit goes idle.
		/// Use forceImmediate=true to bypass the cooldown throttle (for initial path computation).
		/// </summary>
		private void UpdatePath(Vector3Int gridPosition, UnitType targetUnitType, Vector3Int targetGridPos, bool forceImmediate = false, bool avoidUnits = false)
		{
			int cooldown = (60 / Constants.GAME_SPEED) * pathBackoffMultiplier;
			if (forceImmediate || pathUpdateCounter > cooldown)
			{
				pathUpdateCounter = 0;
				path = GameManager.Instance.Map.GetPathToUnit(GridPosition, targetUnitType, targetGridPos, avoidUnits);

				if (path.Count == 0)
				{
					pathFailCount++;
					pathBackoffMultiplier = Math.Min(pathBackoffMultiplier * 2, 8);

					if (pathFailCount >= 5)
					{
						// ATTACK pursuit handles its own retarget/idle logic in UpdateAttack;
						// don't yank it to IDLE here — the target may still be shootable from range.
						if (CurrentAction != UnitAction.ATTACK)
							CurrentAction = UnitAction.IDLE;
						pathFailCount = 0;
						pathBackoffMultiplier = 1;
					}
				}
				else
				{
					pathFailCount = 0;
					pathBackoffMultiplier = 1;
				}
			}
		}

		/// <summary>
		/// Draw a blue line along the unit's remaining path when path debugging is enabled.
		/// Falls back to a direct line to the target when path is empty but unit is active.
		/// </summary>
		private void UpdatePathVisualization()
		{
			if (pathLineRenderer == null) return;

			if (!GameManager.Instance.HasPathTint)
			{
				pathLineRenderer.positionCount = 0;
				return;
			}

			if (path != null && path.Count > 0)
			{
				pathLineRenderer.positionCount = path.Count + 1;
				pathLineRenderer.SetPosition(0, WorldPosition);
				for (int i = 0; i < path.Count; i++)
				{
					Vector3 cellCenter = (Vector3)path[i] + new Vector3(0.5f, 0.5f, 0);
					pathLineRenderer.SetPosition(i + 1, cellCenter);
				}
			}
			else
			{
				pathLineRenderer.positionCount = 0;
			}
		}

		/// <summary>
		/// Draw a red line from this unit to its attack target when attack tint is enabled.
		/// </summary>
		private void UpdateTargetVisualization()
		{
			if (targetLineRenderer == null) return;

			if (!GameManager.Instance.HasAttackTint
				|| CurrentAction != UnitAction.ATTACK
				|| AttackUnit == null)
			{
				targetLineRenderer.positionCount = 0;
				return;
			}

			Vector3 targetPos = (Vector3)AttackUnit.GetComponent<Unit>().CenterGridPosition + new Vector3(0.5f, 0.5f, 0);
			targetLineRenderer.positionCount = 2;
			targetLineRenderer.SetPosition(0, WorldPosition);
			targetLineRenderer.SetPosition(1, targetPos);
		}

		/// <summary>
		/// Update the move task
		/// </summary>
		private void UpdateMove()
		{
			if (path == null || path.Count == 0)
			{
				CurrentAction = UnitAction.IDLE;
			}
		}

		#endregion
	}
}
