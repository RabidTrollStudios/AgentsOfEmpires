using System;
using System.Collections.Generic;
using AgentSDK;
using UnityEngine;

namespace GameManager.GameElements
{
	/// <summary>
	/// Visual state machine for unit animations. Drives animator state integers
	/// based on transition conditions gated on IsVisuallyMoving, eliminating
	/// race conditions from multiple FixedUpdates between render frames.
	///
	/// Core invariant: Run states require IsVisuallyMoving == true.
	/// Action states (Mining, Building, Attack) require IsVisuallyMoving == false.
	/// </summary>
	public class UnitVisualStateMachine
	{
		public class State
		{
			public string Name;
			public int AnimatorInt;
			public Action<Unit> OnEnter;
			public Action<Unit> OnUpdate;
			public Action<Unit> OnExit;
			internal List<(Func<Unit, bool> condition, State target)> Transitions
				= new List<(Func<Unit, bool>, State)>();
		}

		private readonly Dictionary<string, State> _states = new Dictionary<string, State>();
		private State _current;

		// Snapshot of action/phase when visual movement started.
		// Used by transition conditions to select the correct Run variant.
		internal UnitAction MoveAction;
		internal GatherPhase MoveGatherPhase;
		internal BuildPhase MoveBuildPhase;

		public State CurrentState => _current;
		public int AnimatorStateInt => _current?.AnimatorInt ?? 0;

		public State AddState(string name, int animatorInt,
			Action<Unit> onEnter = null, Action<Unit> onUpdate = null, Action<Unit> onExit = null)
		{
			var state = new State
			{
				Name = name,
				AnimatorInt = animatorInt,
				OnEnter = onEnter,
				OnUpdate = onUpdate,
				OnExit = onExit
			};
			_states[name] = state;
			return state;
		}

		public void AddTransition(State from, State to, Func<Unit, bool> condition)
		{
			from.Transitions.Add((condition, to));
		}

		/// <summary>Set state without firing callbacks. Used during initialization.</summary>
		public void SetInitialState(State state)
		{
			_current = state;
		}

		/// <summary>Called when StartVisualMove fires — snapshot current action/phase.</summary>
		public void NotifyMoveStarted(UnitAction action, GatherPhase gatherPhase, BuildPhase buildPhase)
		{
			MoveAction = action;
			MoveGatherPhase = gatherPhase;
			MoveBuildPhase = buildPhase;
		}

		/// <summary>
		/// Evaluate transitions and run current state's update. Called each frame.
		/// Returns the animator state integer for the current (possibly new) state.
		/// </summary>
		public int Update(Unit unit)
		{
			if (_current == null) return 0;

			// Evaluate transitions — first match wins
			foreach (var (condition, target) in _current.Transitions)
			{
				if (condition(unit))
				{
					_current.OnExit?.Invoke(unit);
					_current = target;
					_current.OnEnter?.Invoke(unit);
					break;
				}
			}

			_current.OnUpdate?.Invoke(unit);
			return _current.AnimatorInt;
		}
	}

	/// <summary>
	/// Partial class extension for Unit — visual state machine integration.
	/// </summary>
	public partial class Unit
	{
		private UnitVisualStateMachine _vsm;
		private int _lastLancerStateInt = -1;

		internal void InitializeVisualStateMachine()
		{
			if (!CanMove) return;

			switch (UnitType)
			{
				case UnitType.PAWN:     _vsm = CreatePawnVSM(); break;
				case UnitType.WARRIOR:  _vsm = CreateWarriorVSM(); break;
				case UnitType.ARCHER:   _vsm = CreateArcherVSM(); break;
				case UnitType.LANCER:   _vsm = CreateLancerVSM(); break;
				case UnitType.MONK:     _vsm = CreateMonkVSM(); break;
			}
		}

		#region Pawn

		private UnitVisualStateMachine CreatePawnVSM()
		{
			var sm = new UnitVisualStateMachine();

			var idle       = sm.AddState("Idle", 0);
			var run        = sm.AddState("Run", 1);
			var runGold    = sm.AddState("RunGold", 2);
			var building   = sm.AddState("Building", 3,
				onUpdate: u => FaceTowardBuilding(u));
			var runHammer  = sm.AddState("RunHammer", 4);
			var runPickaxe = sm.AddState("RunPickaxe", 5);
			var mining     = sm.AddState("Mining", 6,
				onEnter: u => { goldNuggetSpawnedThisCycle = false; },
				onUpdate: u => UpdateGoldNuggetSpawn(u));

			// From Idle — enter run states when visual movement starts
			sm.AddTransition(idle, runGold,    u => u.IsVisuallyMoving && sm.MoveAction == UnitAction.GATHER && sm.MoveGatherPhase == GatherPhase.TO_BASE);
			sm.AddTransition(idle, runPickaxe, u => u.IsVisuallyMoving && sm.MoveAction == UnitAction.GATHER && sm.MoveGatherPhase == GatherPhase.TO_MINE);
			sm.AddTransition(idle, runHammer,  u => u.IsVisuallyMoving && (sm.MoveAction == UnitAction.BUILD || sm.MoveAction == UnitAction.REPAIR));
			sm.AddTransition(idle, run,        u => u.IsVisuallyMoving);
			// From Idle — enter stationary action states
			sm.AddTransition(idle, mining,     u => !u.IsVisuallyMoving && u.CurrentAction == UnitAction.GATHER && u.gatherPhase == GatherPhase.MINING);
			sm.AddTransition(idle, building,   u => !u.IsVisuallyMoving && (u.CurrentAction == UnitAction.BUILD || u.CurrentAction == UnitAction.REPAIR) && u.buildPhase == BuildPhase.BUILDING);

			// From Mining — start carrying gold or go idle
			sm.AddTransition(mining, runGold,    u => u.IsVisuallyMoving && sm.MoveAction == UnitAction.GATHER && sm.MoveGatherPhase == GatherPhase.TO_BASE);
			sm.AddTransition(mining, runPickaxe, u => u.IsVisuallyMoving);
			sm.AddTransition(mining, idle,       u => u.CurrentAction != UnitAction.GATHER || u.gatherPhase != GatherPhase.MINING);

			// From Building — start running or go idle
			sm.AddTransition(building, runHammer, u => u.IsVisuallyMoving);
			sm.AddTransition(building, idle,      u => u.CurrentAction != UnitAction.BUILD && u.CurrentAction != UnitAction.REPAIR);

			// From any Run state — stop when visual arrives
			sm.AddTransition(runGold,    mining,   u => !u.IsVisuallyMoving && u.CurrentAction == UnitAction.GATHER && u.gatherPhase == GatherPhase.MINING);
			sm.AddTransition(runGold,    idle,     u => !u.IsVisuallyMoving);
			sm.AddTransition(runPickaxe, mining,   u => !u.IsVisuallyMoving && u.CurrentAction == UnitAction.GATHER && u.gatherPhase == GatherPhase.MINING);
			sm.AddTransition(runPickaxe, idle,     u => !u.IsVisuallyMoving);
			sm.AddTransition(runHammer,  building, u => !u.IsVisuallyMoving && (u.CurrentAction == UnitAction.BUILD || u.CurrentAction == UnitAction.REPAIR) && u.buildPhase == BuildPhase.BUILDING);
			sm.AddTransition(runHammer,  idle,     u => !u.IsVisuallyMoving);
			sm.AddTransition(run,        idle,     u => !u.IsVisuallyMoving);

			sm.SetInitialState(idle);
			return sm;
		}

		private void UpdateGoldNuggetSpawn(Unit u)
		{
			if (animator == null || animator.runtimeAnimatorController == null) return;
			if (MineUnit == null) return;

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

		private void FaceTowardBuilding(Unit u)
		{
			var building = GameManager.Instance?.Units?.GetUnit(((ITickUnit)u).BuildTargetNbr);
			if (building == null) return;
			float dx = building.CenterGridPosition.x - CenterGridPosition.x;
			if (dx > 0.01f) facingRight = true;
			else if (dx < -0.01f) facingRight = false;
		}

		#endregion

		#region Warrior

		private UnitVisualStateMachine CreateWarriorVSM()
		{
			var sm = new UnitVisualStateMachine();

			var idle    = sm.AddState("Idle", 0);
			var run     = sm.AddState("Run", 1);
			var attack1 = sm.AddState("Attack1", 2,
				onEnter: u => { lastAttackNormTime = 0f; });
			var attack2 = sm.AddState("Attack2", 3,
				onEnter: u => { lastAttackNormTime = 0f; });
			var guard   = sm.AddState("Guard", 4);

			// Moving always overrides
			sm.AddTransition(idle, run,    u => u.IsVisuallyMoving);
			sm.AddTransition(guard, run,   u => u.IsVisuallyMoving);
			sm.AddTransition(attack1, run, u => u.IsVisuallyMoving);
			sm.AddTransition(attack2, run, u => u.IsVisuallyMoving);

			// From Run — stop
			sm.AddTransition(run, idle, u => !u.IsVisuallyMoving);

			// From Idle — enter action states
			sm.AddTransition(idle, attack1, u => u.CurrentAction == UnitAction.ATTACK);
			sm.AddTransition(idle, guard,   u => u.CurrentAction == UnitAction.MOVE);

			// Attack alternation
			sm.AddTransition(attack1, attack2, u => AttackAnimLoopComplete(u));
			sm.AddTransition(attack2, attack1, u => AttackAnimLoopComplete(u));

			// Exit attack/guard when action changes
			sm.AddTransition(attack1, idle, u => u.CurrentAction != UnitAction.ATTACK);
			sm.AddTransition(attack2, idle, u => u.CurrentAction != UnitAction.ATTACK);
			sm.AddTransition(guard, idle,   u => u.CurrentAction != UnitAction.MOVE);

			sm.SetInitialState(idle);
			return sm;
		}

		private bool AttackAnimLoopComplete(Unit u)
		{
			if (animator == null) return false;
			var info = animator.GetCurrentAnimatorStateInfo(0);
			bool completed = lastAttackNormTime < 1.0f && info.normalizedTime >= 1.0f;
			lastAttackNormTime = info.normalizedTime < lastAttackNormTime ? 0f : info.normalizedTime;
			return completed;
		}

		#endregion

		#region Archer

		private UnitVisualStateMachine CreateArcherVSM()
		{
			var sm = new UnitVisualStateMachine();

			var idle  = sm.AddState("Idle", 0);
			var run   = sm.AddState("Run", 1);
			var shoot = sm.AddState("Shoot", 2,
				onUpdate: u => UpdateArrowSpawn(u));

			sm.AddTransition(idle, run,   u => u.IsVisuallyMoving);
			sm.AddTransition(shoot, run,  u => u.IsVisuallyMoving);

			sm.AddTransition(run, idle, u => !u.IsVisuallyMoving);

			sm.AddTransition(idle, shoot, u => u.CurrentAction == UnitAction.ATTACK);
			sm.AddTransition(shoot, idle, u => u.CurrentAction != UnitAction.ATTACK);

			sm.SetInitialState(idle);
			return sm;
		}

		private void UpdateArrowSpawn(Unit u)
		{
			if (animator == null || animator.runtimeAnimatorController == null) return;
			if (AttackUnit == null) return;

			float loopPos = animator.GetCurrentAnimatorStateInfo(0).normalizedTime % 1f;
			if (loopPos >= 0.625f && !arrowFiredThisCycle)
			{
				arrowFiredThisCycle = true;
				SpawnArrow(AttackUnit);
			}
			else if (loopPos < 0.625f)
			{
				arrowFiredThisCycle = false;
			}
		}

		#endregion

		#region Lancer

		private UnitVisualStateMachine CreateLancerVSM()
		{
			var sm = new UnitVisualStateMachine();

			var idle = sm.AddState("Idle", 0);
			var run  = sm.AddState("Run", 1);
			// Directional attacks — the AnimatorInt is the lancer state hash index
			var atkRight     = sm.AddState("AtkRight", 2);
			var atkUp        = sm.AddState("AtkUp", 3);
			var atkDown      = sm.AddState("AtkDown", 4);
			var atkUpRight   = sm.AddState("AtkUpRight", 5);
			var atkDownRight = sm.AddState("AtkDownRight", 6);

			var atkStates = new[] { atkRight, atkUp, atkDown, atkUpRight, atkDownRight };

			sm.AddTransition(idle, run, u => u.IsVisuallyMoving);
			foreach (var atk in atkStates)
				sm.AddTransition(atk, run, u => u.IsVisuallyMoving);

			sm.AddTransition(run, idle, u => !u.IsVisuallyMoving);

			// From idle → directional attack
			sm.AddTransition(idle, atkRight,     u => u.CurrentAction == UnitAction.ATTACK && GetLancerDirIndex(u) == 2);
			sm.AddTransition(idle, atkUp,        u => u.CurrentAction == UnitAction.ATTACK && GetLancerDirIndex(u) == 3);
			sm.AddTransition(idle, atkDown,      u => u.CurrentAction == UnitAction.ATTACK && GetLancerDirIndex(u) == 4);
			sm.AddTransition(idle, atkUpRight,   u => u.CurrentAction == UnitAction.ATTACK && GetLancerDirIndex(u) == 5);
			sm.AddTransition(idle, atkDownRight, u => u.CurrentAction == UnitAction.ATTACK && GetLancerDirIndex(u) == 6);

			// Direction changes while attacking
			foreach (var atk in atkStates)
			{
				sm.AddTransition(atk, idle, u => u.CurrentAction != UnitAction.ATTACK);
				if (atk != atkRight)     sm.AddTransition(atk, atkRight,     u => GetLancerDirIndex(u) == 2);
				if (atk != atkUp)        sm.AddTransition(atk, atkUp,        u => GetLancerDirIndex(u) == 3);
				if (atk != atkDown)      sm.AddTransition(atk, atkDown,      u => GetLancerDirIndex(u) == 4);
				if (atk != atkUpRight)   sm.AddTransition(atk, atkUpRight,   u => GetLancerDirIndex(u) == 5);
				if (atk != atkDownRight) sm.AddTransition(atk, atkDownRight, u => GetLancerDirIndex(u) == 6);
			}

			sm.SetInitialState(idle);
			return sm;
		}

		private int GetLancerDirIndex(Unit u)
		{
			return GetLancerDirectionalAttackIndex();
		}

		#endregion

		#region Monk

		private UnitVisualStateMachine CreateMonkVSM()
		{
			var sm = new UnitVisualStateMachine();

			var idle = sm.AddState("Idle", 0);
			var run  = sm.AddState("Run", 1);
			var heal = sm.AddState("Heal", 2);

			sm.AddTransition(idle, run,  u => u.IsVisuallyMoving);
			sm.AddTransition(heal, run,  u => u.IsVisuallyMoving);

			sm.AddTransition(run, idle, u => !u.IsVisuallyMoving);

			sm.AddTransition(idle, heal, u => u.CurrentAction == UnitAction.HEAL || u.healLineTimer > 0f);
			sm.AddTransition(heal, idle, u => u.CurrentAction != UnitAction.HEAL && u.healLineTimer <= 0f);

			sm.SetInitialState(idle);
			return sm;
		}

		#endregion

		#region Animation Driver

		/// <summary>
		/// Drive animation via the visual state machine. Replaces the old
		/// monolithic UpdateAnimation() method.
		/// </summary>
		private void UpdateAnimationVSM()
		{
			if (animator == null || animator.runtimeAnimatorController == null || !CanMove)
				return;

			int stateInt = _vsm.Update(this);

			animator.SetInteger("State", stateInt);
			animator.speed = Constants.GAME_SPEED;

			if (UnitType == UnitType.LANCER)
			{
				UpdateLancerFacing();
				if (unitSprite != null)
					unitSprite.flipX = !facingRight;
			}

			// Facing direction
			if (UnitType != UnitType.LANCER)
			{
				string stateName = _vsm.CurrentState?.Name;

				// Stationary action states handle their own facing via OnUpdate callbacks
				// (Building faces toward building, Mining faces toward mine).
				// Only apply velocity-based facing for movement and other states.
				if (stateName != "Building" && stateName != "Mining")
				{
					if (velocity.x > 0.05f)
						facingRight = true;
					else if (velocity.x < -0.05f)
						facingRight = false;
				}

				// Face toward mine when mining
				if (stateName == "Mining" && MineUnit != null)
				{
					float dx = MineUnit.GetComponent<Unit>().CenterGridPosition.x - CenterGridPosition.x;
					if (dx > 0.01f) facingRight = true;
					else if (dx < -0.01f) facingRight = false;
				}

				// Face toward attack target when stationary attacking
				if ((CurrentAction == UnitAction.ATTACK) && !IsVisuallyMoving && AttackUnit != null)
				{
					float dx = AttackUnit.CenterGridPosition.x - CenterGridPosition.x;
					if (dx > 0.01f) facingRight = true;
					else if (dx < -0.01f) facingRight = false;
				}

				if (unitSprite != null)
					unitSprite.flipX = !facingRight;
			}
		}

		#endregion
	}
}
