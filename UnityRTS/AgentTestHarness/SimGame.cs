using System;
using System.Collections.Generic;
using System.Linq;
using AgentSDK;

namespace AgentTestHarness
{
    /// <summary>
    /// Tick-based game loop simulator. Orchestrates the full agent lifecycle
    /// without any Unity dependencies.
    ///
    /// Tick order:
    /// 1. Call agent.Update(state, actions) for each agent
    /// 2. Process queued commands (validate, deduct gold, set unit actions)
    /// 3. Advance all in-progress unit tasks (move, train, build, gather, attack)
    /// 4. Remove dead units (health &lt;= 0)
    /// </summary>
    public partial class SimGame : ITickParticipant
    {
        public SimMap Map { get; }
        public SimConfig Config { get; }
        public int CurrentTick { get; private set; }

        // Per-agent state
        internal int[] Gold;
        internal int[] Wins;

        // All units keyed by UnitNbr
        internal Dictionary<int, SimUnit> Units = new Dictionary<int, SimUnit>();
        internal int NextUnitNbr;

        // Agent references
        private IPlanningAgent[] agents = new IPlanningAgent[2];
        private SimGameState[] states = new SimGameState[2];
        private SimAgentActions[] actions = new SimAgentActions[2];

        // Optional command recording (null when disabled)
        private CommandRecorder[] recorders;


        // Commands that failed during Phase 1 processing (cleared each tick)
        internal List<FailedCommand>[] FailedCommands = new List<FailedCommand>[2]
        {
            new List<FailedCommand>(),
            new List<FailedCommand>(),
        };

        // Derived timing constants (computed from Config.GameSpeed via shared DerivedGameConstants)
        internal DerivedGameConstants derived;
        internal float scalarCreationTime => derived.ScalarCreationTime;
        internal Dictionary<UnitType, float> creationTime => derived.CreationTime;
        internal Dictionary<UnitType, float> damage => derived.Damage;
        internal Dictionary<UnitType, float> movingSpeed => derived.MovingSpeed;
        internal float miningSpeed => derived.MiningSpeed;
        internal float miningCapacity => derived.MiningCapacity;
        internal float manaRegen => derived.ManaRegen;

        internal SimGame(SimConfig config, SimMap map)
        {
            Config = config;
            Map = map;
            Gold = new int[] { config.StartingGold, config.StartingGold };
            Wins = new int[] { 0, 0 };
            CurrentTick = 0;

            ComputeDerivedConstants();

            states[0] = new SimGameState(this, 0);
            states[1] = new SimGameState(this, 1);
            actions[0] = new SimAgentActions(this, 0);
            actions[1] = new SimAgentActions(this, 1);
        }

        private void ComputeDerivedConstants()
        {
            derived = DerivedGameConstants.Compute(Config.GameSpeed);
        }

        /// <summary>
        /// Recalculate all game-speed-dependent constants. Call when game speed changes mid-game.
        /// Mirrors Unity's Constants.CalculateGameConstants().
        /// </summary>
        public void SetGameSpeed(int newSpeed)
        {
            Config.GameSpeed = newSpeed;
            ComputeDerivedConstants();
        }

        #region Agent Setup

        public void SetAgent(int agentNbr, IPlanningAgent agent)
        {
            agents[agentNbr] = agent;
        }

        /// <summary>
        /// Place a unit on the map. Updates grid buildability.
        /// </summary>
        internal SimUnit PlaceUnit(int ownerAgentNbr, UnitType unitType, Position position, float health, bool isBuilt)
        {
            var unit = new SimUnit(NextUnitNbr++, unitType, ownerAgentNbr, position, health, isBuilt);
            Units[unit.UnitNbr] = unit;
            Map.Grid.SetUnitFootprint(unitType, position, true);
            return unit;
        }

        #endregion

        /// <summary>
        /// Enable command recording for all agents. Call before Run/Tick.
        /// </summary>
        public void EnableRecording()
        {
            recorders = new CommandRecorder[2];
            for (int a = 0; a < 2; a++)
                recorders[a] = new CommandRecorder(actions[a], a, () => CurrentTick);
        }

        /// <summary>Get recorded commands for an agent (null if recording not enabled).</summary>
        public List<CommandRecord> GetRecordedCommands(int agentNbr)
        {
            return recorders?[agentNbr]?.Records;
        }

        #region Game Lifecycle

        /// <summary>
        /// Call InitializeMatch on both agents.
        /// </summary>
        public void InitializeMatch()
        {
            agents[0]?.InitializeMatch();
            agents[1]?.InitializeMatch();
        }

        /// <summary>
        /// Call InitializeRound on both agents.
        /// </summary>
        public void InitializeRound()
        {
            agents[0]?.InitializeRound(states[0]);
            agents[1]?.InitializeRound(states[1]);
        }

        /// <summary>
        /// Call Learn on both agents.
        /// </summary>
        public void Learn()
        {
            agents[0]?.Learn(states[0]);
            agents[1]?.Learn(states[1]);
        }

        /// <summary>
        /// Advance the simulation by one tick.
        ///
        /// Tick order matches Unity's FixedUpdate lifecycle:
        ///   Phase 1: Process commands queued during PREVIOUS tick's agent Update
        ///   Phase 2: Advance tasks (movement, building, training, combat, mana, death)
        ///   Phase 3: Agent Update (agents see post-advance state, queue commands for next tick)
        ///
        /// In Unity, FixedUpdate fires before Update on the first frame, so the
        /// first SimulateTick runs with an empty command queue. Agents issue their
        /// first commands in the subsequent Update(), processed in the next FixedUpdate.
        /// </summary>
        public void Tick()
        {
            // Drive one tick through the canonical shared phase order (see AgentSDK.TickSequence).
            // Identical ordering is used by Unity's GameManager, guaranteeing per-tick parity.
            TickSequence.RunOneTick(this, CurrentTick);
            CurrentTick++;
        }

        // --- ITickParticipant: the four canonical tick phases ---

        /// <summary>Phase 0: the headless sim has no exporter; state is observed by the
        /// caller after Tick(). No-op unless a snapshot sink is later added.</summary>
        void ITickParticipant.RecordSnapshot(int tick) { /* headless: nothing to export */ }

        /// <summary>Phase 1: process commands queued during the previous tick's agent Update.</summary>
        void ITickParticipant.ProcessQueuedCommands() => ProcessCommandsSorted();

        /// <summary>Phase 2: advance tasks/movement, mana regen, remove dead (shared TickEngine).</summary>
        void ITickParticipant.AdvanceUnits() => AdvanceAllUnits();

        /// <summary>Phase 3: run each agent's Update; agents queue commands for the next tick.</summary>
        void ITickParticipant.RunAgentUpdates()
        {
            for (int a = 0; a < 2; a++)
            {
                actions[a].ClearPending();
                IAgentActions agentActions = recorders != null ? (IAgentActions)recorders[a] : actions[a];
                agents[a]?.Update(states[a], agentActions);
            }
        }

        /// <summary>Run the simulation for a fixed number of ticks.</summary>
        public void Run(int ticks)
        {
            for (int i = 0; i < ticks; i++)
                Tick();
        }

        /// <summary>
        /// Run until a predicate is true or maxTicks is exceeded.
        /// Returns true if predicate was satisfied.
        /// </summary>
        public bool RunUntil(Func<SimGame, bool> predicate, int maxTicks = 10000)
        {
            for (int i = 0; i < maxTicks; i++)
            {
                if (predicate(this)) return true;
                Tick();
            }
            return predicate(this);
        }

        #endregion

        #region Query Helpers

        public int GetGold(int agentNbr) => Gold[agentNbr];
        public int GetWins(int agentNbr) => Wins[agentNbr];

        public int CountUnits() => Units.Count;

        public SimUnit GetUnit(int unitNbr)
        {
            Units.TryGetValue(unitNbr, out var unit);
            return unit;
        }

        public List<SimUnit> GetUnitsByType(int agentNbr, UnitType type)
        {
            return Units.Values
                .Where(u => u.OwnerAgentNbr == agentNbr && u.UnitType == type)
                .ToList();
        }

        public List<string> GetLogs(int agentNbr)
        {
            return actions[agentNbr].LogMessages;
        }

        #endregion

        #region Helpers

        private void RemoveUnit(SimUnit unit)
        {
            Map.Grid.SetUnitFootprint(unit.UnitType, unit.GridPosition, false);
            Units.Remove(unit.UnitNbr);
        }

        /// <summary>Public wrapper for ITickWorld.RemoveUnit.</summary>
        internal void RemoveUnitPublic(SimUnit unit) => RemoveUnit(unit);

        private void RemoveDeadUnits()
        {
            var dead = Units.Values.Where(u => u.Health <= 0).ToList();

            foreach (var unit in dead)
                RemoveUnit(unit);
        }

        #endregion
    }
}
