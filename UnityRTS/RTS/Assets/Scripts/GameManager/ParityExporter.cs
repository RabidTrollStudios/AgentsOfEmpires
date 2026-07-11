using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AgentSDK;
using GameManager.GameElements;
using UnityEngine;

namespace GameManager
{
    /// <summary>
    /// Exports per-tick commands and periodic state checkpoints from the Unity game
    /// for cross-verification against AgentTestHarness SimGame.
    ///
    /// Output files (in project root):
    ///   ParityCommands_{timestamp}.csv  — every command issued, with tick number
    ///   ParityState_{timestamp}.csv     — periodic state snapshots (unit positions, health, gold)
    ///
    /// A test in PlanningAgent.Tests reads these files, replays the commands in SimGame,
    /// and verifies the state checkpoints match within tolerance.
    ///
    /// Enable by adding this component to the GameManager GameObject in the Inspector.
    /// </summary>
    public class ParityExporter : MonoBehaviour
    {
        [Tooltip("Maximum number of ticks to record (0 = unlimited)")]
        public int MaxTicks = 2000;

        [Tooltip("Ticks between state snapshots (used only when SnapshotEveryTick is off)")]
        public int SnapshotInterval = 50;

        [Tooltip("Record a snapshot on EVERY tick (fine-grained parity debugging). " +
                 "Files stay small; leave on to pinpoint the exact divergence tick.")]
        public bool SnapshotEveryTick = true;

        [Tooltip("Enable/disable export")]
        public bool IsEnabled = true;

        private StreamWriter cmdWriter;
        private StreamWriter stateWriter;
        private int currentTick;
        private bool finished;
        private bool wroteInitialSnapshot;

        private void Start()
        {
            Debug.Log($"[ParityExporter] Start() called. IsEnabled={IsEnabled}");

            if (!IsEnabled) return;

            currentTick = 0;
            finished = false;

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string root = Path.Combine(Application.dataPath, "..");

            string cmdPath = Path.Combine(root, "ParityCommands_" + timestamp + ".csv");
            cmdWriter = new StreamWriter(cmdPath, false);
            cmdWriter.WriteLine("tick,agent,type,unit,target_x,target_y,target_unit,building_type,mine,base");

            string statePath = Path.Combine(root, "ParityState_" + timestamp + ".csv");
            stateWriter = new StreamWriter(statePath, false);
            // First line: metadata for reconstructing the game in SimGame
            var gm = GameManager.Instance;
            int mapW = gm != null ? gm.Map.MapSize.x : 30;
            int mapH = gm != null ? gm.Map.MapSize.y : 30;
            string mapMeta;
            if (gm != null && gm.MapConfigMode == MapMode.PROCEDURAL)
            {
                mapMeta = $"# map={mapW}x{mapH} speed={Constants.GAME_SPEED}" +
                    $" mode=Procedural template={gm.MapConfigTemplate}" +
                    $" density={gm.MapConfigDensity:F4} seed={gm.MapConfigSeed}" +
                    $" symmetry={gm.MapConfigSymmetry}" +
                    $" genW={gm.MapConfigWidth} genH={gm.MapConfigHeight}";
            }
            else
            {
                mapMeta = $"# map={mapW}x{mapH} speed={Constants.GAME_SPEED} mode=HandMade";
            }
            // Append agent DLL names and starting gold for true parity tests
            if (gm != null)
            {
                mapMeta += $" blue={gm.BlueDllName} red={gm.RedDllName}";
                mapMeta += $" gold={gm.StartingPlayerGold}";
            }
            stateWriter.WriteLine(mapMeta);

            // Export the actual blocked (unwalkable) cells so SimGame uses the exact same grid.
            // Format: # blocked=x1,y1;x2,y2;... (only terrain-blocked cells, before any units placed)
            if (gm != null && gm.Map != null)
            {
                var blocked = new System.Text.StringBuilder("# blocked=");
                bool first = true;
                for (int x = 0; x < gm.Map.MapSize.x; x++)
                {
                    for (int y = 0; y < gm.Map.MapSize.y; y++)
                    {
                        if (!gm.Map.Grid.IsPositionWalkable(new AgentSDK.Position(x, y)))
                        {
                            if (!first) blocked.Append(';');
                            blocked.Append(x).Append(',').Append(y);
                            first = false;
                        }
                    }
                }
                stateWriter.WriteLine(blocked.ToString());
            }

            stateWriter.WriteLine("tick,gold0,gold1,unit_count,units");

            Debug.Log($"[ParityExporter] Writing to: {cmdPath}");
        }

        /// <summary>
        /// Records one tick's state snapshot. Driven explicitly by
        /// <see cref="GameManager.SimulateTick"/> at the TOP of the tick, BEFORE
        /// DeferredCommandQueue.ProcessAll(), so each snapshot captures the
        /// pre-processing state. This matches SimGame's tick semantics, where the
        /// state observed at tick N is the state before tick N's command processing.
        ///
        /// Previously this ran in FixedUpdate at default execution order (0), i.e.
        /// AFTER GameManager (order -100) had already processed the tick — which made
        /// every snapshot one processing-phase ahead of SimGame and broke parity at
        /// tick 1 (Unity showed the opening base already built; SimGame had only just
        /// queued it). See docs/parity-base-build-desync investigation.
        /// </summary>
        internal void RecordTick()
        {
            if (!IsEnabled || finished || cmdWriter == null) return;

            // Write initial state snapshot (tick 0) before the first tick is processed.
            if (!wroteInitialSnapshot)
            {
                wroteInitialSnapshot = true;
                WriteStateSnapshot();
                return; // tick 0 is the pristine pre-processing state; do not advance yet
            }

            currentTick++;

            // Record EVERY tick for fine-grained parity analysis — pinpoints the exact
            // tick a divergence first appears. Full-resolution CSVs are small (well under
            // 1 MB for a few hundred ticks). Set SnapshotEveryTick=false to fall back to
            // the sampled cadence (<=55, 300-350, then every SnapshotInterval).
            bool record = SnapshotEveryTick
                || currentTick <= 55 || (currentTick >= 300 && currentTick <= 350)
                || currentTick % SnapshotInterval == 0;
            if (record)
                WriteStateSnapshot();

            if (MaxTicks > 0 && currentTick >= MaxTicks)
            {
                finished = true;
                cmdWriter.Flush(); cmdWriter.Close(); cmdWriter = null;
                stateWriter.Flush(); stateWriter.Close(); stateWriter = null;
                if (GameManager.Instance != null)
                    GameManager.Instance.Log(
                        $"ParityExporter: finished {currentTick} ticks", gameObject);
            }
        }

        /// <summary>
        /// Called by the command system to record each command as it's issued.
        /// Hook this from AgentActionsAdapter or Agent.Commands.
        /// </summary>
        internal void RecordCommand(int agentNbr, string type, int unitNbr,
            int targetX = 0, int targetY = 0, int targetUnit = -1,
            string buildingType = "", int mineNbr = -1, int baseNbr = -1)
        {
            if (cmdWriter == null || finished) return;
            // Record at the current tick. Commands issued during Update() are
            // processed at the start of the NEXT FixedUpdate in both Unity and SimGame.
            // CommandPlayer matches commands by tick number to replay at the right time.
            cmdWriter.WriteLine($"{currentTick},{agentNbr},{type},{unitNbr},{targetX},{targetY},{targetUnit},{buildingType},{mineNbr},{baseNbr}");
        }

        private void WriteStateSnapshot()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            int gold0 = 0, gold1 = 0;
            var agents = GetAgents();
            foreach (var kvp in agents)
            {
                var agent = kvp.Value.GetComponent<AgentController>().Agent;
                if (agent.AgentNbr == 0) gold0 = agent.Gold;
                else gold1 = agent.Gold;
            }

            var allUnits = gm.Units.GetAllUnits();
            var unitParts = new List<string>();
            foreach (var kvp in allUnits.OrderBy(k => k.Key))
            {
                var u = kvp.Value.GetComponent<Unit>();
                if (u == null) continue;
                int owner = u.OwnerAgentNbr;
                // Format: unitNbr:type:owner:x:y:health:isBuilt:action:moveAcc
                unitParts.Add($"{u.UnitNbr}:{u.UnitType}:{owner}:{u.GridPosition.x}:{u.GridPosition.y}:{u.Health:F1}:{(u.IsBuilt?1:0)}:{u.CurrentAction}:{u.PathProgress:F4}");
            }

            string units = string.Join("|", unitParts);
            stateWriter.WriteLine($"{currentTick},{gold0},{gold1},{allUnits.Count},{units}");
        }

        private Dictionary<int, GameObject> GetAgents()
        {
            try
            {
                var prop = typeof(GameManager).GetProperty("Agents",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (prop != null)
                    return (Dictionary<int, GameObject>)prop.GetValue(GameManager.Instance);
            }
            catch { }
            return new Dictionary<int, GameObject>();
        }

        private void OnDestroy()
        {
            cmdWriter?.Flush(); cmdWriter?.Close(); cmdWriter = null;
            stateWriter?.Flush(); stateWriter?.Close(); stateWriter = null;
        }
    }
}
