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
		private bool facingRight = true;
		private bool useAttack2 = false;
		private float lastAttackNormTime = 0f;

		#region Update Methods

		/// <summary>
		/// Per-frame update: animations, debug visualization, and visual effects only.
		/// Game logic runs in FixedUpdate for deterministic parity with SimGame.
		/// </summary>
		internal void Update()
		{
			// Walk the visual through queued waypoints at the unit's speed.
			// FixedUpdate enqueues grid positions; we smoothly traverse them here.
			if (CanMove)
			{
				// Advance current segment
				if (visualSegmentT < 1.0f)
				{
					float segDist = Vector3.Distance(visualMoveFrom, visualMoveTo);
					if (segDist > 0.001f && visualSpeed > 0.001f)
					{
						float segDuration = segDist / visualSpeed; // seconds for this segment
						visualSegmentT += Time.deltaTime / segDuration;
					}
					else
					{
						visualSegmentT = 1.0f;
					}
					if (visualSegmentT >= 1.0f)
					{
						visualSegmentT = 1.0f;
						WorldPosition = visualMoveTo;
					}
					else
					{
						WorldPosition = Vector3.Lerp(visualMoveFrom, visualMoveTo, visualSegmentT);
					}
					velocity = Utility.SafeNormalize(visualMoveTo - visualMoveFrom);
				}

				// If current segment is done, pop next waypoint
				if (visualSegmentT >= 1.0f && visualWaypoints.Count > 0)
				{
					Vector3Int nextWp = visualWaypoints.Dequeue();
					visualMoveFrom = WorldPosition;
					visualMoveTo = (Vector3)nextWp + new Vector3(0.5f, 0f, 0);
					visualSegmentT = 0f;
					velocity = Utility.SafeNormalize(visualMoveTo - visualMoveFrom);
				}

				// If no segments active and no waypoints, snap to grid
				if (visualSegmentT >= 1.0f && visualWaypoints.Count == 0)
				{
					WorldPosition = (Vector3)GridPosition + new Vector3(0.5f, 0f, 0);
				}
			}

			UpdateAnimation();

			if (GameManager.Instance == null || !GameManager.Instance.IsPlaying) return;

			HasDebugging = GameManager.Instance.HasUnitDebugging;
			UpdateDebuggingInfo();
			UpdatePathVisualization();
			UpdateTargetVisualization();
		}

		/// <summary>
		/// Fixed-timestep game logic: task dispatch, death, mana regen, and movement.
		/// Runs at Time.fixedDeltaTime (0.05s = 20 Hz) matching SimGame's tick rate.
		/// </summary>
		internal void GameLogicTick()
		{
			if (GameManager.Instance == null || !GameManager.Instance.IsPlaying) return;

			MineUnit = GameManager.Instance.Units.GetUnit(mineUnit);
			BaseUnit = GameManager.Instance.Units.GetUnit(baseUnit);
			pathUpdateCounter++;

			// If this unit is dead, destroy it
			if (Health <= 0)
			{
				var cmdLog = GetCmdLog();
				string killedBy = "";
				if (attackUnitNbr >= 0)
				{
					var killer = GameManager.Instance.Units.GetUnit(attackUnitNbr);
					if (killer != null)
						killedBy = $" by {killer.UnitType}#{killer.UnitNbr}";
				}
				cmdLog?.LogCommand("DEATH", $"{UnitType}#{UnitNbr} at {GridPosition} (action={CurrentAction}){killedBy}",
					$"DESTROYED (health={Health:F0})");

				GetRoundStats()?.RecordUnitLost(UnitType);
				if (attackUnitNbr >= 0)
				{
					var killerUnit = GameManager.Instance.Units.GetUnit(attackUnitNbr);
					var killerStats = killerUnit?.Agent?.GetComponent<AgentController>()?.Agent?.Analytics?.CurrentRound;
					killerStats?.RecordEnemyKill(UnitType);
				}

				SpawnDeathDust();

				if (currentBuilding != null)
					currentBuilding.GetComponent<Unit>().ActiveBuilders.Remove(UnitNbr);
				currentBuilding = null;
				GameManager.Instance.Units.DestroyUnit(gameObject);
			}
			else if (CurrentAction == UnitAction.IDLE)
			{
				if (currentBuilding != null)
					currentBuilding.GetComponent<Unit>().ActiveBuilders.Remove(UnitNbr);
				currentBuilding = null;
				path.Clear();
				pathIndex = 0;
				MoveAccumulator = 0f;
				visualWaypoints.Clear();
				visualSegmentT = 1.0f;
				TargetGridPos = GridPosition;
				TargetUnitType = UnitType.PAWN;
				AttackUnit = null;
				MineUnit = null;
				BaseUnit = null;
				arrowFiredThisCycle = false;
			}
			else
			{
				if (CurrentAction == UnitAction.GATHER && CanGather) UpdateGather();
				else if (CurrentAction == UnitAction.ATTACK && CanAttack) UpdateAttack();
				else if (CurrentAction == UnitAction.BUILD && CanBuild) UpdateBuild();
				else if (CurrentAction == UnitAction.MOVE && CanMove) UpdateMove();
				else if (CurrentAction == UnitAction.TRAIN && CanTrain) UpdateTrain();
				else if (CurrentAction == UnitAction.REPAIR && CanBuild) UpdateRepair();
				else if (CurrentAction == UnitAction.HEAL && CanHeal) UpdateHeal();
			}

			// Passive mana regeneration (shared formula for parity)
			Mana = AgentSDK.TaskEngine.RegenMana(Mana, MaxMana, Constants.MANA_REGEN, Time.fixedDeltaTime);
		}

		/// <summary>
		/// Spawn a dust 2 poof effect at the unit's position on death.
		/// </summary>
		private void SpawnDeathDust()
		{
			var controller = GameManager.Instance.Dust2AnimatorController;
			if (controller == null) return;

			var dustGo = new GameObject("DeathDust");
			dustGo.transform.position = WorldPosition;
			dustGo.transform.localScale = Vector3.one; // 64x64 at PPU 64 = 1x1 cell

			var sr = dustGo.AddComponent<SpriteRenderer>();
			sr.sortingLayerName = "Agents";
			sr.sortingOrder = 0;

			var animator = dustGo.AddComponent<Animator>();
			animator.runtimeAnimatorController = controller;

			UnityEngine.Object.Destroy(dustGo, 1.0f); // animation is 1 second
		}

		/// <summary>
		/// Apply action-based tinting after the Animator has finished
		/// updating the sprite, so our color override is not clobbered.
		/// </summary>
		internal void LateUpdate()
		{
			if (GameManager.Instance == null) return;

			UpdateStateColor();
			UpdateHealthBar();
			UpdateTrainingBar();
			UpdateManaBar();
			UpdateBuildPulse();
		}

		/// <summary>
		/// Pulse the building sprite scale on construction completion: 3 pulses over 12 frames.
		/// Each pulse is a sine half-wave that peaks at 110% scale.
		/// </summary>
		private void UpdateBuildPulse()
		{
			if (buildPulseFrames <= 0) return;

			buildPulseFrames--;
			// 3 sine pulses with decreasing amplitude: 0.15, 0.10, 0.05
			float t = 1f - (float)buildPulseFrames / BUILD_PULSE_TOTAL;
			int pulseIndex = Mathf.Min((int)(t * 3f), 2); // 0, 1, 2
			float amplitude = 0.15f - pulseIndex * 0.05f;
			float localT = t * 3f - pulseIndex; // 0..1 within each pulse
			float scale = 1f + amplitude * Mathf.Sin(localT * Mathf.PI);
			transform.localScale = new Vector3(scale, scale, 1f);

			if (buildPulseFrames <= 0)
				transform.localScale = Vector3.one;
		}

		/// <summary>
		/// Tint the sprite based on the unit's current action for visual debugging.
		/// IDLE = normal, MOVE = blue, ATTACK = red.
		/// Orc archers/warriors use cyan (MOVE) and magenta (ATTACK) for contrast
		/// against their darker base sprites.
		/// </summary>
		private void UpdateStateColor()
		{
			bool showAttack = CurrentAction == UnitAction.ATTACK && GameManager.Instance.HasAttackTint;
			bool showMove   = CurrentAction == UnitAction.MOVE   && GameManager.Instance.HasMoveTint;
			bool showGather = CurrentAction == UnitAction.GATHER && GameManager.Instance.HasGatherTint;
			bool showBuild  = CurrentAction == UnitAction.BUILD  && GameManager.Instance.HasBuildTint;


			if (attackIndicator != null) attackIndicator.enabled = showAttack;
			if (moveIndicator   != null) moveIndicator.enabled   = showMove;
			if (gatherIndicator != null) gatherIndicator.enabled = showGather;
			if (buildIndicator  != null) buildIndicator.enabled  = showBuild;
		}

		/// <summary>
		/// Scale the health bar fill based on current health and color it green→red.
		/// The bar stays world-aligned (no flip) above the unit's head.
		/// </summary>
		private void UpdateHealthBar()
		{
			if (healthBarFill == null || maxHealth <= 0) return;

			float ratio;
			if (!IsBuilt)
			{
				ratio = Mathf.Clamp01(BuildProgress / Constants.CREATION_TIME[UnitType]);
				// Keep sprite opacity in sync with build progress (damage can reduce it)
				if (unitSprite != null)
				{
					var c = unitSprite.color;
					c.a = 0.3f + ratio * 0.4f;
					unitSprite.color = c;
				}
			}
			else
				ratio = Mathf.Clamp01(Health / maxHealth);

			if (usesBigBar)
			{
				float fillScaleX = BIG_BAR_FILL_SCALE_X * ratio;
				float fullFillW = BIG_BAR_FILL_SCALE_X * (90f / 64f);
				float leftEdgeOffset = fullFillW * (ratio - 1f) * 0.5f;

				healthBarFill.localScale = new Vector3(fillScaleX, BIG_BAR_FILL_SCALE_Y, 1f);
				healthBarFill.localPosition = new Vector3(
					BIG_BAR_FILL_X_OFFSET + leftEdgeOffset, BIG_BAR_FILL_Y_OFFSET, 0f);

				healthBarBg.localScale = new Vector3(BIG_BAR_SCALE_X, BIG_BAR_SCALE_Y, 1f);
				healthBarBg.localPosition = new Vector3(0f, BIG_BAR_Y_OFFSET, 0f);
			}
			else
			{
				float fillScaleX = SM_BAR_FILL_SCALE_X * ratio;
				float fullFillW = SM_BAR_FILL_SCALE_X * (82f / 64f);
				float leftEdgeOffset = fullFillW * (ratio - 1f) * 0.5f;

				healthBarFill.localScale = new Vector3(fillScaleX, SM_BAR_FILL_SCALE_Y, 1f);
				healthBarFill.localPosition = new Vector3(
					SM_BAR_FILL_X_OFFSET + leftEdgeOffset, SM_BAR_FILL_Y_OFFSET, 0f);

				healthBarBg.localScale = new Vector3(SM_BAR_SCALE, SM_BAR_SCALE, 1f);
				healthBarBg.localPosition = new Vector3(0f, SM_BAR_Y_OFFSET, 0f);
			}

			// Stratified color: green > 60%, yellow 30–60%, red < 30%
			int tier = ratio > 0.6f ? 2 : ratio > 0.3f ? 1 : 0;

			// Trigger effects when color tier changes
			if (healthBarColorTier != -1 && tier != healthBarColorTier)
				tierChangeFrames = TIER_FLASH_FRAMES;
			healthBarColorTier = tier;

			Color tierColor = tier == 2 ? Color.green : tier == 1 ? Color.yellow : Color.red;
			Color barColor;

			// Building opacity (read before any sprite color changes)
			float alpha = (!IsBuilt && unitSprite != null) ? unitSprite.color.a : 1f;

			if (tierChangeFrames > 0)
			{
				tierChangeFrames--;
				float t = (float)tierChangeFrames / TIER_FLASH_FRAMES;

				// 1) Health bar fill flashes white, fading back to tier color
				barColor = Color.Lerp(tierColor, Color.white, t);

				// 2) Unit sprite flashes white, fading back to normal
				if (unitSprite != null)
				{
					var c = Color.Lerp(Color.white, Color.white, t);
					c.a = alpha;
					unitSprite.color = c;
				}

				// 3) Health bar shake — random positional and rotational jitter that decays
				float shake = t * 0.12f;
				float offsetX = UnityEngine.Random.Range(-shake, shake);
				float offsetY = UnityEngine.Random.Range(-shake, shake);
				healthBarBg.localPosition += new Vector3(offsetX, offsetY, 0f);
				healthBarFill.localPosition += new Vector3(offsetX, offsetY, 0f);

				float angle = UnityEngine.Random.Range(-20f, 20f) * t;
				healthBarBg.localRotation = Quaternion.Euler(0f, 0f, angle);
				healthBarFill.localRotation = Quaternion.Euler(0f, 0f, angle);

				// 1b) Health bar scale pulse — grows then shrinks back
				float pulse = 1f + 0.4f * t;
				healthBarBg.localScale = new Vector3(
					healthBarBg.localScale.x * pulse, healthBarBg.localScale.y * pulse, 1f);
				healthBarFill.localScale = new Vector3(
					healthBarFill.localScale.x * pulse, healthBarFill.localScale.y * pulse, 1f);
			}
			else
			{
				barColor = tierColor;
				// Reset rotation from shake
				healthBarBg.localRotation = Quaternion.identity;
				healthBarFill.localRotation = Quaternion.identity;
				// Restore unit sprite color
				if (unitSprite != null)
				{
					var c = Color.white;
					c.a = alpha;
					unitSprite.color = c;
				}
			}

			barColor.a = alpha;
			healthBarFill.GetComponent<SpriteRenderer>().color = barColor;

			var bgSr = healthBarBg.GetComponent<SpriteRenderer>();
			var bgColor = bgSr.color;
			bgColor.a = alpha;
			bgSr.color = bgColor;

			// Spawn fires on buildings as health drops (every 2%)
			if (usesBigBar && IsBuilt && UnitType != UnitType.MINE)
				UpdateBuildingFires(ratio);
		}

		/// <summary>
		/// Show/hide and scale the training progress bar for buildings currently training.
		/// </summary>
		private void UpdateTrainingBar()
		{
			if (trainingBarFrame == null) return;

			bool isTraining = CurrentAction == UnitAction.TRAIN;
			trainingBarFrame.gameObject.SetActive(isTraining);
			trainingBarFill.gameObject.SetActive(isTraining);

			if (!isTraining) return;

			float creationTime = Constants.CREATION_TIME != null && Constants.CREATION_TIME.ContainsKey(taskUnitType)
				? Constants.CREATION_TIME[taskUnitType] : 0f;
			float ratio = creationTime > 0f ? Mathf.Clamp01(taskTime / creationTime) : 0f;
			float fillScaleX = BIG_BAR_FILL_SCALE_X * ratio;
			float fullFillW = BIG_BAR_FILL_SCALE_X * (90f / 64f);
			float leftEdgeOffset = fullFillW * (ratio - 1f) * 0.5f;

			float trainFillY = BIG_BAR_FILL_Y_OFFSET - TRAIN_BAR_Y_GAP;
			trainingBarFill.localScale = new Vector3(fillScaleX, BIG_BAR_FILL_SCALE_Y, 1f);
			trainingBarFill.localPosition = new Vector3(
				BIG_BAR_FILL_X_OFFSET + leftEdgeOffset, trainFillY, 0f);
		}

		/// <summary>
		/// Scale the mana bar fill based on current mana (monks only).
		/// </summary>
		private void UpdateManaBar()
		{
			if (manaBarFill == null) return;

			float ratio = MaxMana > 0 ? Mathf.Clamp01(Mana / MaxMana) : 0f;
			float fillScaleX = SM_BAR_FILL_SCALE_X * ratio;
			float fullFillW = SM_BAR_FILL_SCALE_X * (82f / 64f);
			float leftEdgeOffset = fullFillW * (ratio - 1f) * 0.5f;

			float manaFillY = SM_BAR_FILL_Y_OFFSET - MANA_BAR_Y_GAP;
			manaBarFill.localScale = new Vector3(fillScaleX, SM_BAR_FILL_SCALE_Y, 1f);
			manaBarFill.localPosition = new Vector3(
				SM_BAR_FILL_X_OFFSET + leftEdgeOffset, manaFillY, 0f);
		}

		/// <summary>
		/// Spawn a fire at a random location on the building for every 2% health lost.
		/// Fires are children of this unit and destroyed when the building dies.
		/// </summary>
		private void UpdateBuildingFires(float healthRatio)
		{
			int currentThreshold = Mathf.FloorToInt(healthRatio * 50f); // 50 = 100% / 2%
			while (currentThreshold < lastFireThreshold)
			{
				lastFireThreshold--;
				SpawnBuildingFire();
			}
			while (currentThreshold > lastFireThreshold)
			{
				lastFireThreshold++;
				RemoveOneBuildingFire();
			}
		}

		private void RemoveOneBuildingFire()
		{
			// Remove the last BuildingFire child (LIFO order)
			for (int i = transform.childCount - 1; i >= 0; i--)
			{
				var child = transform.GetChild(i);
				if (child.name == "BuildingFire")
				{
					UnityEngine.Object.Destroy(child.gameObject);
					return;
				}
			}
		}

		private void SpawnBuildingFire()
		{
			var controllers = GameManager.Instance.BuildingFireControllers;
			if (controllers == null || controllers.Length == 0) return;

			var controller = controllers[UnityEngine.Random.Range(0, controllers.Length)];
			if (controller == null) return;

			// Random position within the building footprint
			var size = Constants.UNIT_SIZE[UnitType];
			float halfW = size.x * 0.5f;
			float halfH = size.y * 0.5f;
			float rx = UnityEngine.Random.Range(-halfW, halfW);
			float ry = UnityEngine.Random.Range(-halfH, halfH);

			var fireGo = new GameObject("BuildingFire");
			fireGo.transform.SetParent(transform);
			fireGo.transform.localPosition = new Vector3(rx, ry, 0f);
			fireGo.transform.localScale = Vector3.one;
			fireGo.transform.localRotation = Quaternion.identity;

			var sr = fireGo.AddComponent<SpriteRenderer>();
			sr.sortingLayerName = "UnitUI";
			sr.sortingOrder = 20;

			var animator = fireGo.AddComponent<Animator>();
			animator.runtimeAnimatorController = controller;
		}

		/// <summary>
		/// Drive the animator's "State" integer parameter based on the unit's
		/// current action and sub-phase so the correct TinySwords animation plays.
		///
		/// Pawn (Pawn) states:  0=Idle, 1=Run, 2=RunGold, 3=InteractHammer, 4=RunHammer, 5=RunPickaxe, 6=InteractPickaxe
		/// Warrior (Warrior) states: 0=Idle, 1=Run, 2=Attack1, 3=Attack2, 4=Guard
		/// Archer states: 0=Idle, 1=Run, 2=Shoot
		/// Lancer states: 0=Idle, 1=Run,
		///   2=Right_Attack, 3=Up_Attack, 4=Down_Attack, 5=UpRight_Attack, 6=DownRight_Attack,
		///   7=Right_Defence, 8=Up_Defence, 9=Down_Defence, 10=UpRight_Defence, 11=DownRight_Defence
		/// </summary>
		private void UpdateAnimation()
		{
			if (animator == null || animator.runtimeAnimatorController == null || !CanMove)
				return;

			int state = 0; // default: Idle

			if (UnitType == UnitType.WARRIOR)
			{
				switch (CurrentAction)
				{
					case UnitAction.MOVE:
						if (path.Count > 0)
							state = 1; // Run
						else
							state = 4; // Guard (arrived at rally point)
						break;

					case UnitAction.ATTACK:
						if (path.Count > 0)
						{
							state = 1; // Run (chasing)
							lastAttackNormTime = 0f;
						}
						else
						{
							state = useAttack2 ? 3 : 2;
							// Alternate attack animation only after the clip completes one loop
							var info = animator.GetCurrentAnimatorStateInfo(0);
							if (lastAttackNormTime < 1.0f && info.normalizedTime >= 1.0f)
								useAttack2 = !useAttack2;
							// Reset tracking when normalizedTime drops (new state entered)
							lastAttackNormTime = info.normalizedTime < lastAttackNormTime
								? 0f : info.normalizedTime;
						}
						break;
				}
			}
			else if (UnitType == UnitType.ARCHER)
			{
				switch (CurrentAction)
				{
					case UnitAction.MOVE:
						if (path.Count > 0)
							state = 1; // Run
						break;

					case UnitAction.ATTACK:
						if (path.Count > 0)
							state = 1; // Run (pursuing target)
						else
							state = 2; // Shoot (in range)
						break;
				}
			}
			else if (UnitType == UnitType.LANCER)
			{
				// Lancer uses the same State integer parameter as other units.
				// The controller's AnyState transitions map:
				//   0=Idle, 1=Run (unconditional fallback), 2-6=directional attacks.
				int idx = GetLancerStateIndex();
				animator.SetInteger("State", idx);

				// Handle facing/flip, then return early (skip generic facing at end)
				UpdateLancerFacing();
				if (unitSprite != null)
					unitSprite.flipX = !facingRight;
				return;
			}
			else if (UnitType == UnitType.MONK)
			{
				switch (CurrentAction)
				{
					case UnitAction.MOVE:
						if (path.Count > 0)
							state = 1; // Run
						break;

					case UnitAction.HEAL:
						if (path.Count > 0)
							state = 1; // Run (moving toward target)
						else
							state = 2; // Heal (in range)
						break;
				}
			}
			else // PAWN
			{
				switch (CurrentAction)
				{
					case UnitAction.MOVE:
						if (path.Count > 0)
							state = 1;
						break;

					case UnitAction.BUILD:
					case UnitAction.REPAIR:
						if (buildPhase == BuildPhase.TO_POSITION && path.Count > 0)
							state = 4; // running with hammer to build/repair site
						else if (buildPhase == BuildPhase.BUILDING)
							state = 3; // swinging the hammer
						break;

					case UnitAction.GATHER:
						if (gatherPhase == GatherPhase.TO_BASE && path.Count > 0)
							state = 2; // carrying gold home (Run Gold)
						else if (gatherPhase == GatherPhase.MINING)
							state = 6; // mining at the mine (Interact Pickaxe)
						else if (path.Count > 0)
							state = 5; // running to mine (Run Pickaxe)
						break;

					case UnitAction.ATTACK:
						if (path.Count > 0)
							state = 1;
						break;
				}
			}

			// Flip sprite to face the direction of horizontal movement.
			// Sprite faces right by default; flipX mirrors it to face left.
			if (velocity.x > 0.05f)
				facingRight = true;
			else if (velocity.x < -0.05f)
				facingRight = false;

			// Face toward building when building or repairing
			if ((CurrentAction == UnitAction.BUILD || CurrentAction == UnitAction.REPAIR) && buildPhase == BuildPhase.BUILDING && currentBuilding != null)
			{
				float dx = currentBuilding.GetComponent<Unit>().CenterGridPosition.x - CenterGridPosition.x;
				if (dx > 0.01f)
					facingRight = true;
				else if (dx < -0.01f)
					facingRight = false;
			}

			// Face toward mine when mining
			if (CurrentAction == UnitAction.GATHER && gatherPhase == GatherPhase.MINING && MineUnit != null)
			{
				float dx = MineUnit.GetComponent<Unit>().CenterGridPosition.x - CenterGridPosition.x;
				if (dx > 0.01f)
					facingRight = true;
				else if (dx < -0.01f)
					facingRight = false;
			}

			// Face toward attack target when stationary in attack range
			if (CurrentAction == UnitAction.ATTACK && AttackUnit != null && path.Count == 0)
			{
				float dx = AttackUnit.CenterGridPosition.x - CenterGridPosition.x;
				if (dx > 0.01f)
					facingRight = true;
				else if (dx < -0.01f)
					facingRight = false;
			}

			if (unitSprite != null)
				unitSprite.flipX = !facingRight;

			animator.SetInteger("State", state);
		}

		/// <summary>
		/// Lancer animation clip name lookup. Populated once during InitializeUnit
		/// by scanning the animator controller for clip names matching known suffixes.
		/// Index: 0=Idle, 1=Run,
		///   2=Right_Attack, 3=Up_Attack, 4=Down_Attack, 5=UpRight_Attack, 6=DownRight_Attack,
		///   7=Right_Defence, 8=Up_Defence, 9=Down_Defence, 10=UpRight_Defence, 11=DownRight_Defence
		/// </summary>
		private int[] lancerStateHashes;

		/// <summary>Suffixes used to identify lancer animation states (order matches index).</summary>
		private static readonly string[] LancerStateSuffixes = new[]
		{
			"Idle",                   // 0
			"Run",                    // 1
			"Right_Attack",           // 2
			"Up_Attack",              // 3
			"Down_Attack",            // 4
			"UpRight_Attack",         // 5
			"DownRight_Attack",       // 6
			"Right_Defence",          // 7
			"Up_Defence",             // 8
			"Down_Defence",           // 9
			"UpRight_Defence",        // 10
			"DownRight_Defence",      // 11
		};

		internal void InitLancerStateHashes()
		{
			lancerStateHashes = new int[LancerStateSuffixes.Length];
			// Build hashes from the clip names in the animator controller.
			// State names in the controller follow the pattern: "Lancer_<Suffix>_<Color>"
			// or "Lancer_ <Suffix>_<Color>" (with a space after underscore — asset pack quirk).
			// Animator.StringToHash uses the short state name (m_Name in the controller).
			if (animator == null || animator.runtimeAnimatorController == null) return;
			var clips = animator.runtimeAnimatorController.animationClips;
			for (int i = 0; i < LancerStateSuffixes.Length; i++)
			{
				string suffix = LancerStateSuffixes[i];
				lancerStateHashes[i] = -1;
				foreach (var clip in clips)
				{
					// Match suffix case-insensitively (e.g., clip "Lancer_ Run_Blue" matches suffix "Run")
					string clipName = clip.name.Replace(" ", ""); // normalize spaces
					string normalizedSuffix = suffix.Replace(" ", "");
					if (clipName.IndexOf(normalizedSuffix, System.StringComparison.OrdinalIgnoreCase) >= 0)
					{
						// The animator state name matches the clip name (they share names in the controller)
						lancerStateHashes[i] = Animator.StringToHash(clip.name);
						break;
					}
				}
			}
			// Fallback: if any hash wasn't found, use Idle
			for (int i = 0; i < lancerStateHashes.Length; i++)
			{
				if (lancerStateHashes[i] == -1)
					lancerStateHashes[i] = lancerStateHashes[0];
			}
		}

		/// <summary>
		/// Get the lancer state hash index for the current action.
		/// </summary>
		private int GetLancerStateIndex()
		{
			if (CurrentAction == UnitAction.MOVE && path.Count > 0)
				return 1; // Run

			if (CurrentAction == UnitAction.ATTACK)
			{
				if (path.Count > 0)
					return 1; // Run (pursuing)
				return GetLancerDirectionalAttackIndex();
			}

			return 0; // Idle
		}

		/// <summary>
		/// Pick the directional attack animation index based on angle to the target.
		/// Indices: 2=Right, 3=Up, 4=Down, 5=UpRight, 6=DownRight
		/// </summary>
		private int GetLancerDirectionalAttackIndex()
		{
			if (AttackUnit == null) return 2;

			float dx = AttackUnit.CenterGridPosition.x - CenterGridPosition.x;
			float dy = AttackUnit.CenterGridPosition.y - CenterGridPosition.y;
			float absDx = Mathf.Abs(dx);
			float angle = Mathf.Atan2(dy, absDx) * Mathf.Rad2Deg;

			if (angle > 67.5f)  return 3;  // Up
			if (angle > 22.5f)  return 5;  // UpRight
			if (angle > -22.5f) return 2;  // Right
			if (angle > -67.5f) return 6;  // DownRight
			return 4;                       // Down
		}

		/// <summary>
		/// Handle lancer-specific sprite flipping based on movement or target direction.
		/// </summary>
		private void UpdateLancerFacing()
		{
			if (velocity.x > 0.05f)
				facingRight = true;
			else if (velocity.x < -0.05f)
				facingRight = false;

			if (CurrentAction == UnitAction.ATTACK && AttackUnit != null && path.Count == 0)
			{
				float dx = AttackUnit.CenterGridPosition.x - CenterGridPosition.x;
				if (dx > 0.01f)
					facingRight = true;
				else if (dx < -0.01f)
					facingRight = false;
			}
		}

		/// <summary>
		/// Called by GameManager in deterministic UnitNbr order.
		/// NOT a MonoBehaviour callback — named to avoid Unity auto-calling it.
		/// </summary>
		internal void TickFixedUpdate()
		{
			if (GameManager.Instance == null || !GameManager.Instance.IsPlaying) return;

			// Run all game logic at fixed timestep for SimGame parity
			GameLogicTick();

			// Discrete accumulator-based movement matching SimGame exactly.
			// GridPosition transitions are deterministic; WorldPosition is interpolated in Update().
			if (path != null && pathIndex < path.Count)
			{
				Vector3Int tickStartPos = GridPosition;
				float speed = Speed; // = Constants.MOVING_SPEED[UnitType]
				MoveAccumulator += speed;

				while (path != null && pathIndex < path.Count)
				{
					Vector3Int nextPos = path[pathIndex];
					var curSDK = new AgentSDK.Position(GridPosition.x, GridPosition.y);
					var nxtSDK = new AgentSDK.Position(nextPos.x, nextPos.y);

					var result = AgentSDK.TaskEngine.TryMoveToCell(
						curSDK, nxtSDK, MoveAccumulator,
						GameManager.Instance.Map.Grid, out float distCost);

					switch (result)
					{
						case AgentSDK.TaskEngine.MoveResult.Moved:
							MoveAccumulator -= distCost;
							localAvoidWaitFrames = 0;

							if (CanMove)
							{
								var map = GameManager.Instance.Map;
								var oldP = new AgentSDK.Position(GridPosition.x, GridPosition.y);
								var newP = new AgentSDK.Position(nextPos.x, nextPos.y);
								// Claim new cell BEFORE releasing old — prevents
								// a window where both cells appear OPEN to other units.
								map.Grid.SetCellOccupied(newP, true);
								map.Grid.SetCellOccupied(oldP, false);
								// Sync legacy GridCells: restore old cell unless it's a passage
								if (!map.GridCells[GridPosition.x, GridPosition.y].IsPassage())
									map.GridCells[GridPosition.x, GridPosition.y].SetBuildable(true);
								map.GridCells[nextPos.x, nextPos.y].SetBuildable(false);
							}

							GridPosition = nextPos;
							pathIndex++;

							// Path consumed — clear immediately.
							// Visual queue handles smooth catch-up independently.
							if (pathIndex >= path.Count)
							{
								path.Clear();
								pathIndex = 0;
								if (CurrentAction == UnitAction.MOVE)
									CurrentAction = UnitAction.IDLE;
							}
							continue; // try to consume more cells this tick

						case AgentSDK.TaskEngine.MoveResult.BlockedByUnit:
							// Final cell occupied — don't overlap
							if (pathIndex == path.Count - 1)
							{
								// MOVE: stop here (close enough)
								// Other actions: consume path so task system can
								// check adjacency and re-path to a different neighbor
								path.Clear();
								pathIndex = 0;
								if (CurrentAction == UnitAction.MOVE)
									CurrentAction = UnitAction.IDLE;
								goto doneMoving;
							}
							// Mid-path: pass through
							goto case AgentSDK.TaskEngine.MoveResult.Moved;

						case AgentSDK.TaskEngine.MoveResult.BlockedByTerrain:
							MoveAccumulator -= distCost;
							localAvoidWaitFrames = 0;
							UpdatePath(GridPosition, TargetUnitType, TargetGridPos);
							goto doneMoving;

						default: // InsufficientMovement
							goto doneMoving;
					}
				}
				doneMoving:

				// Visual speed = cells per second. Speed is per-fixedUpdate,
				// so cells/sec = Speed / fixedDeltaTime.
				visualSpeed = speed / Time.fixedDeltaTime;

				// Determine what cell the visual is currently heading toward.
				Vector3Int visualCurrentTarget = GridPosition;
				if (visualWaypoints.Count > 0)
					visualCurrentTarget = visualWaypoints.Peek();
				else if (visualSegmentT < 1.0f)
					visualCurrentTarget = new Vector3Int(
						Mathf.RoundToInt(visualMoveTo.x - 0.5f),
						Mathf.RoundToInt(visualMoveTo.y), 0);

				if (GridPosition != tickStartPos)
				{
					// Grid moved this tick. Only enqueue if the visual isn't
					// already heading to this cell (proactive enqueue from prior tick).
					if (visualCurrentTarget != GridPosition)
						visualWaypoints.Enqueue(GridPosition);
				}
				else if (path != null && pathIndex < path.Count
					&& visualWaypoints.Count == 0 && visualSegmentT >= 1.0f)
				{
					// Grid hasn't moved yet but there's a next cell coming.
					// Enqueue it now so the visual starts moving immediately.
					visualWaypoints.Enqueue(path[pathIndex]);
				}
			}
		}

	

		private void UpdateDebuggingInfo()
		{
			// Enable/disable debugging
			var canvas = gameObject.GetComponentInChildren<Canvas>();
			if (canvas == null) return;
			canvas.enabled = HasDebugging;
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
								textArea.text = (path.Count - pathIndex).ToString();
								break;
							case UnitAction.TRAIN:
								textArea.text = taskTime.ToString("0.0");
								break;
							case UnitAction.REPAIR:
								if (currentBuilding != null)
									textArea.text = currentBuilding.GetComponent<Unit>().Health.ToString("0.0");
								else
									textArea.text = "";
								break;
							case UnitAction.HEAL:
								textArea.text = Mana.ToString("0");
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
				path = GameManager.Instance.Map.GetPathToUnit(gridPosition, targetUnitType, targetGridPos, avoidUnits);
				pathIndex = 0;
				if (CanMove)
				{
					visualWaypoints.Clear();
					visualSegmentT = 1.0f;
					WorldPosition = (Vector3)GridPosition + new Vector3(0.5f, 0f, 0);
				}

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

			bool show = GameManager.Instance.HasPathTint;

			// Hide attack pursuit paths when the attack tint toggle is off
			if (show && CurrentAction == UnitAction.ATTACK && !GameManager.Instance.HasAttackTint)
				show = false;

			if (!show)
			{
				pathLineRenderer.positionCount = 0;
				return;
			}

			if (path != null && pathIndex < path.Count)
			{
				int remaining = path.Count - pathIndex;
				pathLineRenderer.positionCount = remaining + 1;
				pathLineRenderer.SetPosition(0, WorldPosition);
				for (int i = 0; i < remaining; i++)
				{
					Vector3 cellCenter = (Vector3)path[pathIndex + i] + new Vector3(0.5f, 0f, 0);
					pathLineRenderer.SetPosition(i + 1, cellCenter);
				}
			}
			else
			{
				pathLineRenderer.positionCount = 0;
			}
		}

		/// <summary>
		/// Create a small triangle mesh to use as an arrowhead at the end of a line.
		/// </summary>
		private GameObject CreateArrowhead(string name, Color color, string sortingLayer, int sortingOrder)
		{
			var go = new GameObject(name);
			go.transform.SetParent(transform);
			go.transform.localPosition = Vector3.zero;

			var mesh = new Mesh();
			mesh.vertices = new Vector3[]
			{
				new Vector3(0f, 0.12f, 0f),
				new Vector3(-0.08f, -0.06f, 0f),
				new Vector3(0.08f, -0.06f, 0f),
			};
			mesh.triangles = new int[] { 0, 1, 2 };
			mesh.RecalculateNormals();

			var mf = go.AddComponent<MeshFilter>();
			mf.mesh = mesh;

			var mr = go.AddComponent<MeshRenderer>();
			mr.material = new Material(Shader.Find("Sprites/Default"));
			mr.material.color = color;
			mr.sortingLayerName = sortingLayer;
			mr.sortingOrder = sortingOrder;

			go.SetActive(false);
			return go;
		}

		/// <summary>
		/// Position and rotate an arrowhead so it sits at the end point facing the direction of travel.
		/// </summary>
		private static void PositionArrowhead(GameObject arrowhead, Vector3 from, Vector3 to)
		{
			if (arrowhead == null) return;
			arrowhead.SetActive(true);
			arrowhead.transform.position = to;
			Vector3 dir = (to - from).normalized;
			float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
			arrowhead.transform.rotation = Quaternion.Euler(0f, 0f, angle);
		}

		/// <summary>
		/// Draw a red line from this unit to its attack target, or a green line
		/// from a monk to its heal target, when target line tint is enabled.
		/// </summary>
		private void UpdateTargetVisualization()
		{
			// --- Attack line (red) ---
			if (targetLineRenderer != null)
			{
				bool showAttack = GameManager.Instance.HasTargetLineTint
				                  && CurrentAction == UnitAction.ATTACK
				                  && AttackUnit != null;

				targetLineRenderer.gameObject.SetActive(showAttack);
				if (showAttack)
				{
					Vector3 targetPos = AttackUnit.WorldPosition;
					targetLineRenderer.positionCount = 2;
					targetLineRenderer.SetPosition(0, WorldPosition);
					targetLineRenderer.SetPosition(1, targetPos);
					PositionArrowhead(targetArrowhead, WorldPosition, targetPos);
				}
				else if (targetArrowhead != null)
				{
					targetArrowhead.SetActive(false);
				}
			}

			// --- Heal line (green) ---
			if (healLineRenderer != null)
			{
				Unit healTarget = null;
				if (CurrentAction == UnitAction.HEAL && healTargetNbr >= 0)
					healTarget = GameManager.Instance.Units.GetUnit(healTargetNbr);

				bool showHeal = GameManager.Instance.HasTargetLineTint
				                && healTarget != null;

				healLineRenderer.gameObject.SetActive(showHeal);
				if (showHeal)
				{
					Vector3 targetPos = healTarget.WorldPosition;
					healLineRenderer.positionCount = 2;
					healLineRenderer.SetPosition(0, WorldPosition);
					healLineRenderer.SetPosition(1, targetPos);
					PositionArrowhead(healArrowhead, WorldPosition, targetPos);
				}
				else if (healArrowhead != null)
				{
					healArrowhead.SetActive(false);
				}
			}
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
