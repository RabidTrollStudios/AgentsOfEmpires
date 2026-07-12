using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AgentSDK;
using AgentTestHarness;
using Xunit;
using Xunit.Abstractions;

namespace Parity.Tests
{
    /// <summary>
    /// True cross-engine parity test: runs the SAME agents on the SAME map
    /// in SimGame and compares tick-by-tick state against Unity's exported snapshots.
    ///
    /// Workflow:
    /// 1. Unity runs a match with ParityExporter → produces ParityState_*.csv
    /// 2. This test reads the export metadata (agent DLLs, map config, game speed)
    /// 3. Loads the same agent DLLs into SimGame with identical map setup
    /// 4. Runs SimGame independently (agents make their own decisions)
    /// 5. At each snapshot tick, compares SimGame state against Unity's snapshot
    ///
    /// No command replay — both engines run the same agents independently.
    /// Divergences indicate genuine behavioral differences between engines.
    /// </summary>
    public class EngineParityTests
    {
        private readonly ITestOutputHelper _output;

        public EngineParityTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void SameAgents_ProduceIdenticalState()
        {
            var stateFile = FindLatestStateExport();
            if (stateFile == null)
            {
                _output.WriteLine("SKIP: No ParityState export files found. Run the Unity game " +
                    "with ParityExporter enabled to generate them.");
                return;
            }

            _output.WriteLine($"Loading: {Path.GetFileName(stateFile)}");

            var meta = ParseMetadata(stateFile);
            _output.WriteLine($"Map: {meta.MapW}x{meta.MapH} speed={meta.GameSpeed} mode={meta.Mode}");
            _output.WriteLine($"Agents: blue={meta.BlueDll} red={meta.RedDll} gold={meta.StartingGold}");

            // Load agent DLLs
            var blueAgent = LoadAgentDll(meta.BlueDll);
            var redAgent = LoadAgentDll(meta.RedDll);

            if (blueAgent == null || redAgent == null)
            {
                _output.WriteLine($"SKIP: Could not load agent DLLs (blue={meta.BlueDll}, red={meta.RedDll}).");
                return;
            }

            // Build SimGame with the same configuration
            var builder = new SimGameBuilder();
            builder.WithConfig(new SimConfig
            {
                GameSpeed = meta.GameSpeed,
                StartingGold = meta.StartingGold,
            });

            if (meta.IsProcedural)
            {
                Enum.TryParse(meta.Template, out MapTemplate template);
                Enum.TryParse(meta.Symmetry, out SymmetryType symmetry);
                builder.WithGeneratedMap(new MapGeneratorConfig
                {
                    Seed = meta.Seed,
                    Width = meta.GenW,
                    Height = meta.GenH,
                    Template = template,
                    ObstacleDensity = meta.Density,
                    Symmetry = symmetry,
                });
                _output.WriteLine($"Procedural: seed={meta.Seed} template={meta.Template} " +
                    $"{meta.GenW}x{meta.GenH} density={meta.Density} symmetry={meta.Symmetry}");
            }
            else
            {
                builder.WithMapSize(meta.MapW, meta.MapH);
                foreach (var cell in meta.BlockedCells)
                    builder.WithWall(new Position(cell.X, cell.Y), new Position(cell.X, cell.Y));
            }

            // Agent-slot assignment is deterministic from the map seed (U5): if
            // BlueIsAgent0, blue is AgentNbr 0 / spawn slot 0, else red is. Unity now
            // makes the SAME choice from the shared seed, so assign slots to match —
            // otherwise every unit's owner is swapped from tick 1.
            bool blueIsAgent0 = !meta.IsProcedural || MapGenCore.ComputeBlueIsAgent0(meta.Seed);
            builder.WithAgent(0, blueIsAgent0 ? blueAgent : redAgent);
            builder.WithAgent(1, blueIsAgent0 ? redAgent : blueAgent);

            var game = builder.Build();

            // Parse Unity snapshots
            var snapshots = ParseSnapshots(stateFile);
            _output.WriteLine($"Snapshots: {snapshots.Count}");
            if (snapshots.Count == 0)
            {
                _output.WriteLine("No snapshots to verify.");
                return;
            }

            // Run SimGame
            game.InitializeMatch();
            game.InitializeRound();

            int maxTick = snapshots.Max(s => s.Tick);
            // Skip tick-0 (initial state before any tick processing)
            var checkSnapshots = snapshots.Where(s => s.Tick > 0).ToList();

            int snapshotIdx = 0;
            int passed = 0, failed = 0;

            for (int t = 0; t < maxTick; t++)
            {
                game.Tick();
                int tick = t + 1;

                if (snapshotIdx < checkSnapshots.Count && checkSnapshots[snapshotIdx].Tick == tick)
                {
                    var snap = checkSnapshots[snapshotIdx];
                    var diffs = CompareState(game, snap);

                    if (diffs.Count > 0)
                    {
                        if (failed == 0)
                        {
                            _output.WriteLine($"FIRST DIVERGENCE at tick {tick}:");
                            foreach (var diff in diffs.Take(20))
                                _output.WriteLine($"  {diff}");
                            if (diffs.Count > 20)
                                _output.WriteLine($"  ... and {diffs.Count - 20} more");
                        }
                        failed++;
                    }
                    else
                    {
                        passed++;
                    }
                    snapshotIdx++;
                }
            }

            _output.WriteLine($"Results: {passed} snapshots matched, {failed} diverged (of {checkSnapshots.Count} total)");
            Assert.Equal(0, failed);
        }

        #region State Comparison

        private List<string> CompareState(SimGame game, StateSnapshot snap)
        {
            var diffs = new List<string>();

            if (game.GetGold(0) != snap.Gold0)
                diffs.Add($"Gold[0]: sim={game.GetGold(0)} engine={snap.Gold0}");
            if (game.GetGold(1) != snap.Gold1)
                diffs.Add($"Gold[1]: sim={game.GetGold(1)} engine={snap.Gold1}");

            // Collect all sim units
            var simUnits = new List<SimUnit>();
            int maxUnitNbr = snap.Units.Count > 0 ? snap.Units.Max(u => u.UnitNbr) + 20 : 20;
            for (int i = 0; i < maxUnitNbr; i++)
            {
                var su = game.GetUnit(i);
                if (su != null) simUnits.Add(su);
            }

            if (simUnits.Count != snap.UnitCount)
                diffs.Add($"UnitCount: sim={simUnits.Count} engine={snap.UnitCount}");

            foreach (var eu in snap.Units)
            {
                var su = game.GetUnit(eu.UnitNbr);
                if (su == null)
                {
                    diffs.Add($"Unit {eu.UnitNbr} ({eu.UnitType}): missing in sim");
                    continue;
                }

                // Each field is compared only if the recording carried it (eu.Present),
                // so pre-instrumentation CSVs still validate on the original 8 fields while
                // full recordings validate every ITickUnit field. sim side (su) is read via
                // ITickUnit so both engines expose identical semantics.
                var p = eu.Present ?? EightFieldKeys;
                AgentSDK.ITickUnit s = su;

                void D(string label, object simVal, object engVal)
                    => diffs.Add($"Unit {eu.UnitNbr}: {label} sim={simVal} engine={engVal}");

                if (p.Contains("t") && s.UnitType != eu.UnitType) D("type", s.UnitType, eu.UnitType);
                if (p.Contains("o") && s.OwnerAgentNbr != eu.Owner) D("owner", s.OwnerAgentNbr, eu.Owner);
                if ((p.Contains("x") || p.Contains("y")) && (s.GridPosition.X != eu.X || s.GridPosition.Y != eu.Y))
                    D("pos", $"({s.GridPosition.X},{s.GridPosition.Y})", $"({eu.X},{eu.Y})");
                if (p.Contains("hp") && Math.Abs(s.Health - eu.Health) > 0.1f) D("health", s.Health.ToString("F1"), eu.Health.ToString("F1"));
                if (p.Contains("b") && s.IsBuilt != eu.IsBuilt) D("isBuilt", s.IsBuilt, eu.IsBuilt);
                if (p.Contains("a") && s.CurrentAction != eu.Action) D("action", s.CurrentAction, eu.Action);

                // Fine-grained fields (only present in full-instrumentation CSVs).
                if (p.Contains("pp") && Math.Abs(s.PathProgress - eu.PathProgress) > 1e-3f) D("pathProgress", s.PathProgress.ToString("F4"), eu.PathProgress.ToString("F4"));
                if (p.Contains("pi") && s.PathIndex != eu.PathIndex) D("pathIndex", s.PathIndex, eu.PathIndex);
                if (p.Contains("mana") && Math.Abs(s.Mana - eu.Mana) > 0.05f) D("mana", s.Mana.ToString("F2"), eu.Mana.ToString("F2"));
                if (p.Contains("bp") && Math.Abs(s.BuildProgress - eu.BuildProgress) > 1e-3f) D("buildProgress", s.BuildProgress.ToString("F4"), eu.BuildProgress.ToString("F4"));
                if (p.Contains("tt") && Math.Abs(s.TrainTimer - eu.TrainTimer) > 1e-3f) D("taskTimer", s.TrainTimer.ToString("F4"), eu.TrainTimer.ToString("F4"));
                if (p.Contains("gph") && s.GatherPhase != eu.GatherPhase) D("gatherPhase", s.GatherPhase, eu.GatherPhase);
                if (p.Contains("mt") && Math.Abs(s.MiningTimer - eu.MiningTimer) > 1e-3f) D("miningTimer", s.MiningTimer.ToString("F4"), eu.MiningTimer.ToString("F4"));
                if (p.Contains("gc") && s.GoldCarried != eu.GoldCarried) D("goldCarried", s.GoldCarried, eu.GoldCarried);
                if (p.Contains("atk") && s.AttackTargetNbr != eu.AttackTargetNbr) D("attackTarget", s.AttackTargetNbr, eu.AttackTargetNbr);
                if (p.Contains("bld") && s.BuildTargetNbr != eu.BuildTargetNbr) D("buildTarget", s.BuildTargetNbr, eu.BuildTargetNbr);
                if (p.Contains("rep") && s.RepairBuildingNbr != eu.RepairBuildingNbr) D("repairTarget", s.RepairBuildingNbr, eu.RepairBuildingNbr);
                if (p.Contains("heal") && s.HealTargetNbr != eu.HealTargetNbr) D("healTarget", s.HealTargetNbr, eu.HealTargetNbr);
                if (p.Contains("rpp") && s.RepathPending != eu.RepathPending) D("repathPending", s.RepathPending, eu.RepathPending);
            }

            foreach (var su in simUnits)
            {
                if (!snap.Units.Any(u => u.UnitNbr == su.UnitNbr))
                    diffs.Add($"Unit {su.UnitNbr} ({su.UnitType}): extra in sim");
            }

            return diffs;
        }

        #endregion

        #region Agent Loading

        private IPlanningAgent LoadAgentDll(string dllName)
        {
            if (string.IsNullOrEmpty(dllName)) return null;

            string enemyDir = FindEnemyAgentsDir();
            if (enemyDir == null) return null;

            string dllPath = Path.Combine(enemyDir, $"PlanningAgent_{dllName}.dll");
            if (!File.Exists(dllPath))
            {
                _output.WriteLine($"DLL not found: {dllPath}");
                return null;
            }

            try
            {
                var assembly = Assembly.LoadFrom(dllPath);
                foreach (var type in assembly.GetTypes())
                {
                    if (typeof(IPlanningAgent).IsAssignableFrom(type) && !type.IsAbstract && !type.IsInterface)
                        return (IPlanningAgent)Activator.CreateInstance(type);
                }
                _output.WriteLine($"No IPlanningAgent found in {dllPath}");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Failed to load {dllPath}: {ex.Message}");
            }
            return null;
        }

        private static string FindEnemyAgentsDir()
        {
            string dir = Path.GetDirectoryName(typeof(EngineParityTests).Assembly.Location);
            for (int i = 0; i < 10 && dir != null; i++)
            {
                string enemyDir = Path.Combine(dir, "EnemyAgents");
                if (Directory.Exists(enemyDir))
                    return enemyDir;
                dir = Path.GetDirectoryName(dir);
            }
            return null;
        }

        #endregion

        #region Parsing

        private class ExportMetadata
        {
            public int MapW = 30, MapH = 30;
            public int GameSpeed = 20;
            public string Mode = "HandMade";
            public bool IsProcedural;
            public string Template = "OpenField";
            public float Density = 0.15f;
            public int Seed = 42;
            public string Symmetry = "Mirror";
            public int GenW = 30, GenH = 30;
            public List<Position> BlockedCells = new List<Position>();
            public string BlueDll = "";
            public string RedDll = "";
            public int StartingGold = 1000;
        }

        private class StateSnapshot
        {
            public int Tick, Gold0, Gold1, UnitCount;
            public List<UnitSnap> Units = new List<UnitSnap>();
        }

        /// <summary>Fallback field set (the original 8) when a UnitSnap has no Present set.</summary>
        private static readonly HashSet<string> EightFieldKeys =
            new HashSet<string> { "n", "t", "o", "x", "y", "hp", "b", "a" };

        private struct UnitSnap
        {
            public int UnitNbr, Owner, X, Y;
            public UnitType UnitType;
            public float Health;
            public bool IsBuilt;
            public UnitAction Action;
            // Fine-grained fields (full per-field parity). Defaults are benign for older
            // CSVs that don't carry these keys (key=value parsing degrades gracefully).
            public float PathProgress, Mana, BuildProgress, TrainTimer, MiningTimer;
            public int PathIndex, GoldCarried, AttackTargetNbr, BuildTargetNbr, RepairBuildingNbr, HealTargetNbr;
            public GatherPhase GatherPhase;
            public bool RepathPending;
            // Which keys were actually present in the CSV — so we only compare fields the
            // recording carried (a pre-instrumentation CSV won't fail on missing columns).
            public HashSet<string> Present;
        }

        private static ExportMetadata ParseMetadata(string path)
        {
            var meta = new ExportMetadata();
            foreach (string line in File.ReadLines(path))
            {
                if (!line.StartsWith("#")) break;

                if (line.StartsWith("# blocked="))
                {
                    string data = line.Substring("# blocked=".Length);
                    if (!string.IsNullOrEmpty(data))
                    {
                        foreach (var pair in data.Split(';'))
                        {
                            var xy = pair.Split(',');
                            if (xy.Length == 2 && int.TryParse(xy[0], out int bx) && int.TryParse(xy[1], out int by))
                                meta.BlockedCells.Add(new Position(bx, by));
                        }
                    }
                    continue;
                }

                var parts = line.Substring(2).Split(' ');
                foreach (var part in parts)
                {
                    var kv = part.Split('=');
                    if (kv.Length != 2) continue;
                    switch (kv[0])
                    {
                        case "map":
                            var dims = kv[1].Split('x');
                            if (dims.Length == 2) { int.TryParse(dims[0], out meta.MapW); int.TryParse(dims[1], out meta.MapH); }
                            break;
                        case "speed": int.TryParse(kv[1], out meta.GameSpeed); break;
                        case "mode": meta.Mode = kv[1]; meta.IsProcedural = kv[1] == "Procedural"; break;
                        case "template": meta.Template = kv[1]; break;
                        case "density": float.TryParse(kv[1], System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out meta.Density); break;
                        case "seed": int.TryParse(kv[1], out meta.Seed); break;
                        case "symmetry": meta.Symmetry = kv[1]; break;
                        case "genW": int.TryParse(kv[1], out meta.GenW); break;
                        case "genH": int.TryParse(kv[1], out meta.GenH); break;
                        case "blue": meta.BlueDll = kv[1]; break;
                        case "red": meta.RedDll = kv[1]; break;
                        case "gold": int.TryParse(kv[1], out meta.StartingGold); break;
                    }
                }
            }
            return meta;
        }

        private static List<StateSnapshot> ParseSnapshots(string path)
        {
            var snapshots = new List<StateSnapshot>();
            foreach (string line in File.ReadLines(path))
            {
                if (line.StartsWith("#") || line.StartsWith("tick,")) continue;
                var parts = line.Split(',');
                if (parts.Length < 5) continue;

                var snap = new StateSnapshot
                {
                    Tick = int.Parse(parts[0]),
                    Gold0 = int.Parse(parts[1]),
                    Gold1 = int.Parse(parts[2]),
                    UnitCount = int.Parse(parts[3]),
                };

                if (parts.Length >= 5 && !string.IsNullOrWhiteSpace(parts[4]))
                {
                    foreach (string unitStr in parts[4].Split('|'))
                    {
                        if (string.IsNullOrEmpty(unitStr)) continue;
                        // New format is key=value;key=value; old format is positional a:b:c.
                        snap.Units.Add(unitStr.Contains("=")
                            ? ParseUnitKeyed(unitStr)
                            : ParseUnitPositional(unitStr));
                    }
                }

                snapshots.Add(snap);
            }
            return snapshots;
        }

        /// <summary>Legacy positional encoding: unitNbr:type:owner:x:y:health:isBuilt:action[:pp].</summary>
        private static UnitSnap ParseUnitPositional(string unitStr)
        {
            var u = unitStr.Split(':');
            var snap = new UnitSnap { Present = new HashSet<string>() };
            if (u.Length < 8) return snap;
            snap.UnitNbr = int.Parse(u[0]);
            snap.UnitType = Enum.TryParse(u[1], out UnitType ut) ? ut : UnitType.PAWN;
            snap.Owner = int.Parse(u[2]);
            snap.X = int.Parse(u[3]);
            snap.Y = int.Parse(u[4]);
            snap.Health = ParseF(u[5]);
            snap.IsBuilt = u[6] == "1";
            snap.Action = Enum.TryParse(u[7], out UnitAction ua) ? ua : UnitAction.IDLE;
            foreach (var k in new[] { "n", "t", "o", "x", "y", "hp", "b", "a" }) snap.Present.Add(k);
            return snap;
        }

        /// <summary>Full per-field encoding: key=value;key=value;... (see ParityExporter.EncodeUnit).</summary>
        private static UnitSnap ParseUnitKeyed(string unitStr)
        {
            var snap = new UnitSnap { Present = new HashSet<string>() };
            foreach (var pair in unitStr.Split(';'))
            {
                int eq = pair.IndexOf('=');
                if (eq <= 0) continue;
                string key = pair.Substring(0, eq);
                string val = pair.Substring(eq + 1);
                snap.Present.Add(key);
                switch (key)
                {
                    case "n": snap.UnitNbr = int.Parse(val); break;
                    case "t": snap.UnitType = Enum.TryParse(val, out UnitType ut) ? ut : UnitType.PAWN; break;
                    case "o": snap.Owner = int.Parse(val); break;
                    case "x": snap.X = int.Parse(val); break;
                    case "y": snap.Y = int.Parse(val); break;
                    case "hp": snap.Health = ParseF(val); break;
                    case "b": snap.IsBuilt = val == "1"; break;
                    case "a": snap.Action = Enum.TryParse(val, out UnitAction ua) ? ua : UnitAction.IDLE; break;
                    case "pp": snap.PathProgress = ParseF(val); break;
                    case "pi": snap.PathIndex = int.Parse(val); break;
                    case "mana": snap.Mana = ParseF(val); break;
                    case "bp": snap.BuildProgress = ParseF(val); break;
                    case "tt": snap.TrainTimer = ParseF(val); break;
                    case "gph": snap.GatherPhase = Enum.TryParse(val, out GatherPhase gp) ? gp : GatherPhase.TO_MINE; break;
                    case "mt": snap.MiningTimer = ParseF(val); break;
                    case "gc": snap.GoldCarried = int.Parse(val); break;
                    case "atk": snap.AttackTargetNbr = int.Parse(val); break;
                    case "bld": snap.BuildTargetNbr = int.Parse(val); break;
                    case "rep": snap.RepairBuildingNbr = int.Parse(val); break;
                    case "heal": snap.HealTargetNbr = int.Parse(val); break;
                    case "rpp": snap.RepathPending = val == "1"; break;
                }
            }
            return snap;
        }

        private static float ParseF(string s)
            => float.Parse(s, System.Globalization.NumberStyles.Float,
                           System.Globalization.CultureInfo.InvariantCulture);

        #endregion

        #region File Discovery

        private static string FindLatestStateExport()
        {
            string root = FindProjectRoot();
            if (root == null) return null;

            return Directory.GetFiles(root, "ParityState_*.csv")
                .OrderByDescending(f => f)
                .FirstOrDefault();
        }

        private static string FindProjectRoot()
        {
            string dir = Path.GetDirectoryName(typeof(EngineParityTests).Assembly.Location);
            for (int i = 0; i < 10 && dir != null; i++)
            {
                if (Directory.GetFiles(dir, "ParityState_*.csv").Length > 0)
                    return dir;

                string rtsDir = Path.Combine(dir, "RTS");
                if (Directory.Exists(rtsDir) &&
                    Directory.GetFiles(rtsDir, "ParityState_*.csv").Length > 0)
                    return rtsDir;

                dir = Path.GetDirectoryName(dir);
            }
            return null;
        }

        #endregion
    }
}
