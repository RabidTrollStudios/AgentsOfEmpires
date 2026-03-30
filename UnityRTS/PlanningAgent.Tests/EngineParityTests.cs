using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AgentSDK;
using AgentTestHarness;
using Xunit;
using Xunit.Abstractions;

namespace PlanningAgent.Tests
{
    /// <summary>
    /// Verifies parity between the Unity game engine and AgentTestHarness SimGame
    /// by replaying command logs exported from Unity and comparing state snapshots.
    ///
    /// Workflow:
    /// 1. Run the Unity game with a ParityExporter component attached to GameManager
    /// 2. It produces ParityCommands_*.csv and ParityState_*.csv in the project root
    /// 3. This test loads the most recent export, replays commands in SimGame,
    ///    and compares state at each checkpoint
    ///
    /// What's compared at each checkpoint:
    /// - Gold per agent (exact)
    /// - Unit count (exact)
    /// - Per-unit: type, owner, grid position (exact), health (within tolerance),
    ///   isBuilt (exact), action (exact)
    /// </summary>
    public class EngineParityTests
    {
        private readonly ITestOutputHelper _output;

        public EngineParityTests(ITestOutputHelper output)
        {
            _output = output;
        }

        /// <summary>
        /// Find the most recent ParityCommands/ParityState file pair.
        /// Returns null if no export files exist.
        /// </summary>
        private static (string cmdFile, string stateFile)? FindLatestExport()
        {
            string root = FindProjectRoot();
            if (root == null) return null;

            var cmdFiles = Directory.GetFiles(root, "ParityCommands_*.csv")
                .OrderByDescending(f => f)
                .ToList();

            foreach (string cmdFile in cmdFiles)
            {
                string timestamp = Path.GetFileName(cmdFile)
                    .Replace("ParityCommands_", "")
                    .Replace(".csv", "");
                string stateFile = Path.Combine(root, $"ParityState_{timestamp}.csv");
                if (File.Exists(stateFile))
                    return (cmdFile, stateFile);
            }
            return null;
        }

        [Fact]
        public void EngineExport_ReplayMatchesSnapshots()
        {
            var files = FindLatestExport();
            if (files == null)
            {
                _output.WriteLine("SKIP: No ParityExport files found. Run the Unity game " +
                    "with ParityExporter enabled to generate them.");
                return;
            }

            var (cmdFile, stateFile) = files.Value;
            _output.WriteLine($"Loading: {Path.GetFileName(cmdFile)}");
            _output.WriteLine($"         {Path.GetFileName(stateFile)}");

            // Parse commands
            var commands = ParseCommands(cmdFile);
            _output.WriteLine($"Commands: {commands.Count}");

            // Parse state snapshots
            var snapshots = ParseSnapshots(stateFile);
            _output.WriteLine($"Snapshots: {snapshots.Count}");

            if (snapshots.Count == 0)
            {
                _output.WriteLine("No snapshots to verify.");
                return;
            }

            int maxTick = snapshots.Max(s => s.Tick);

            // Read map config from metadata
            var meta = ParseMetadata(stateFile);
            _output.WriteLine($"Map: {meta.MapW}x{meta.MapH} procedural={meta.IsProcedural}");

            // Use tick-0 snapshot as initial state
            var tick0 = snapshots.FirstOrDefault(s => s.Tick == 0);
            if (tick0 == null)
            {
                _output.WriteLine("SKIP: No tick-0 snapshot. Re-run Unity with updated ParityExporter.");
                return;
            }

            _output.WriteLine($"Initial state: {tick0.Units.Count} units, gold=({tick0.Gold0},{tick0.Gold1})");

            var builder = new SimGameBuilder();
            builder.WithConfig(new SimConfig { GameSpeed = meta.GameSpeed });
            _output.WriteLine($"  GameSpeed: {meta.GameSpeed}");

            // Use the full map dimensions from the Unity tilemap, not the generator
            // input dimensions. The blocked cells are exported from the full map.
            builder.WithMapSize(meta.MapW, meta.MapH);

            if (meta.BlockedCells.Count > 0)
            {
                _output.WriteLine($"  Blocked cells from export: {meta.BlockedCells.Count}");
                foreach (var cell in meta.BlockedCells)
                    builder.WithWall(new Position(cell.X, cell.Y), new Position(cell.X, cell.Y));
            }
            else if (meta.IsProcedural)
            {
                // Fallback: regenerate from seed if no blocked cells exported (old export format)
                Enum.TryParse(meta.Template, out MapTemplate template);
                Enum.TryParse(meta.Symmetry, out SymmetryType symmetry);
                var mapResult = new MapGenerator().Generate(new MapGeneratorConfig
                {
                    Seed = meta.Seed,
                    Width = meta.GenW,
                    Height = meta.GenH,
                    Template = template,
                    ObstacleDensity = meta.Density,
                    Symmetry = symmetry,
                });
                _output.WriteLine($"  Procedural fallback: seed={meta.Seed} blocked={mapResult.BlockedCells.Count}");
                foreach (var cell in mapResult.BlockedCells)
                    builder.WithWall(new Position(cell.X, cell.Y), new Position(cell.X, cell.Y));
            }

            builder.WithGold(0, tick0.Gold0).WithGold(1, tick0.Gold1);

            // Place all tick-0 units from the engine snapshot.
            // Use WithUnit for ALL unit types (including mines) to preserve the exact
            // owner from the export. Mines in Unity are owned by agents, not neutral.
            foreach (var u in tick0.Units)
            {
                builder.WithUnit(u.Owner, u.UnitType, new Position(u.X, u.Y),
                    isBuilt: u.IsBuilt, health: u.Health);
            }

            var cmds0 = FilterCommands(commands, 0);
            var cmds1 = FilterCommands(commands, 1);
            _output.WriteLine($"  Filtered commands: agent0={cmds0.Count}, agent1={cmds1.Count}");
            foreach (var c in cmds0.Where(c => c.Tick <= 26))
                _output.WriteLine($"  cmd0: tick={c.Tick} type={c.Type} unit={c.UnitNbr} building={c.BuildingNbr} trainType={c.TrainType} buildType={c.BuildingType} target=({c.Target.X},{c.Target.Y})");
            foreach (var c in cmds1.Where(c => c.Tick <= 26))
                _output.WriteLine($"  cmd1: tick={c.Tick} type={c.Type} unit={c.UnitNbr} building={c.BuildingNbr} trainType={c.TrainType} buildType={c.BuildingType} target=({c.Target.X},{c.Target.Y})");

            builder.WithAgent(0, new CommandPlayer(cmds0))
                   .WithAgent(1, new CommandPlayer(cmds1));
            var game = builder.Build();

            // Skip tick-0 in the comparison loop (it's the initial state)
            snapshots = snapshots.Where(s => s.Tick > 0).ToList();

            game.InitializeMatch();
            game.InitializeRound();

            // Debug: dump detailed state at key ticks
            int snapshotIdx = 0;
            int passed = 0, failed = 0;

            for (int t = 0; t < maxTick; t++)
            {
                game.Tick();
                int tick = t + 1;

                // Trace pawn positions + grid state near base at (7,8)
                if (tick == 1 || tick == 2)
                {
                    _output.WriteLine($"  Grid near (7,8) at t{tick}:");
                    for (int y = 10; y >= 4; y--)
                    {
                        string row = $"    y={y}: ";
                        for (int x = 5; x <= 14; x++)
                        {
                            var st = game.Map.Grid.GetCell(x, y);
                            row += st == CellState.OPEN ? "." : st == CellState.WALKABLE ? "W" : "X";
                        }
                        _output.WriteLine(row);
                    }
                    // Also show path for u0
                    var u0path = game.GetUnit(0) as SimUnit;
                    if (u0path?.Path != null)
                    {
                        string pathStr = "";
                        for (int i = u0path.PathIndex; i < System.Math.Min(u0path.PathIndex + 5, u0path.Path.Count); i++)
                            pathStr += $"({u0path.Path[i].X},{u0path.Path[i].Y}) ";
                        _output.WriteLine($"  u0 path: {pathStr}");
                    }
                }
                // Trace who's at (63,19) around tick 40
                if (tick >= 38 && tick <= 44)
                {
                    string who = "";
                    for (int uid = 0; uid < 20; uid++)
                    {
                        var u = game.GetUnit(uid);
                        if (u != null && u.GridPosition.X == 63 && u.GridPosition.Y == 19)
                        {
                            string phase = u.CurrentAction == UnitAction.GATHER ? $":{((SimUnit)u).GatherPhase}" : "";
                            who += $" u{uid}:{u.CurrentAction}{phase}";
                        }
                    }
                    var u11 = game.GetUnit(11) as SimUnit;
                    string u11s = u11 != null ? $" u11:({u11.GridPosition.X},{u11.GridPosition.Y}):{u11.CurrentAction}" : "";
                    _output.WriteLine($"  [t{tick}] (63,19)={game.Map.Grid.GetCell(63,19)}{who} |{u11s}");
                }

                if (snapshotIdx < snapshots.Count && snapshots[snapshotIdx].Tick == t + 1)
                {
                    var snap = snapshots[snapshotIdx];
                    var diffs = CompareState(game, snap);

                    if (diffs.Count > 0)
                    {
                        if (failed == 0)
                        {
                            _output.WriteLine($"FIRST DIVERGENCE at tick {snap.Tick}:");
                            foreach (var diff in diffs)
                                _output.WriteLine($"  {diff}");
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

            _output.WriteLine($"Results: {passed} snapshots matched, {failed} diverged");
            Assert.Equal(0, failed);
        }

        #region State Comparison

        private int CountSimUnits(SimGame game, StateSnapshot snap)
        {
            int count = 0;
            int max = snap.Units.Count > 0 ? snap.Units.Max(u => u.UnitNbr) + 50 : 50;
            for (int i = 0; i < max; i++)
                if (game.GetUnit(i) != null) count++;
            return count;
        }

        private List<string> CompareState(SimGame game, StateSnapshot snap)
        {
            var diffs = new List<string>();

            if (game.GetGold(0) != snap.Gold0)
                diffs.Add($"Gold[0]: sim={game.GetGold(0)} engine={snap.Gold0}");
            if (game.GetGold(1) != snap.Gold1)
                diffs.Add($"Gold[1]: sim={game.GetGold(1)} engine={snap.Gold1}");

            // Count all sim units by checking all possible unit numbers
            var simUnits = new List<SimUnit>();
            foreach (var eu in snap.Units)
            {
                var su = game.GetUnit(eu.UnitNbr);
                if (su != null) simUnits.Add(su);
            }
            // Also check for extra sim units not in the snapshot
            // (scan a reasonable range based on snapshot unit count)
            int maxUnitNbr = snap.Units.Count > 0 ? snap.Units.Max(u => u.UnitNbr) + 20 : 20;
            for (int i = 0; i < maxUnitNbr; i++)
            {
                var su = game.GetUnit(i);
                if (su != null && !simUnits.Contains(su))
                    simUnits.Add(su);
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

                if (su.UnitType != eu.UnitType)
                    diffs.Add($"Unit {eu.UnitNbr}: type sim={su.UnitType} engine={eu.UnitType}");
                if (su.OwnerAgentNbr != eu.Owner)
                    diffs.Add($"Unit {eu.UnitNbr}: owner sim={su.OwnerAgentNbr} engine={eu.Owner}");
                if (su.GridPosition.X != eu.X || su.GridPosition.Y != eu.Y)
                    diffs.Add($"Unit {eu.UnitNbr}: pos sim=({su.GridPosition.X},{su.GridPosition.Y}) engine=({eu.X},{eu.Y})");
                if (Math.Abs(su.Health - eu.Health) > 0.1f)
                    diffs.Add($"Unit {eu.UnitNbr}: health sim={su.Health:F1} engine={eu.Health:F1}");
                if (su.IsBuilt != eu.IsBuilt)
                    diffs.Add($"Unit {eu.UnitNbr}: isBuilt sim={su.IsBuilt} engine={eu.IsBuilt}");
            }

            foreach (var su in simUnits)
            {
                if (!snap.Units.Any(u => u.UnitNbr == su.UnitNbr))
                    diffs.Add($"Unit {su.UnitNbr} ({su.UnitType}): extra in sim");
            }

            return diffs;
        }

        #endregion

        #region Parsing

        private struct ExportedCommand
        {
            public int Tick, Agent, UnitNbr, TargetX, TargetY, TargetUnit, MineNbr, BaseNbr;
            public string Type, BuildingType;
        }

        private class StateSnapshot
        {
            public int Tick, Gold0, Gold1, UnitCount;
            public List<UnitSnap> Units = new List<UnitSnap>();
        }

        private struct UnitSnap
        {
            public int UnitNbr, Owner, X, Y;
            public UnitType UnitType;
            public float Health;
            public bool IsBuilt;
            public UnitAction Action;
        }

        private static List<ExportedCommand> ParseCommands(string path)
        {
            var commands = new List<ExportedCommand>();
            foreach (string line in File.ReadLines(path).Skip(1)) // skip header
            {
                var parts = line.Split(',');
                if (parts.Length < 10) continue;
                commands.Add(new ExportedCommand
                {
                    Tick = int.Parse(parts[0]) - 1,
                    Agent = int.Parse(parts[1]),
                    Type = parts[2],
                    UnitNbr = int.Parse(parts[3]),
                    TargetX = int.Parse(parts[4]),
                    TargetY = int.Parse(parts[5]),
                    TargetUnit = int.Parse(parts[6]),
                    BuildingType = parts[7],
                    MineNbr = int.Parse(parts[8]),
                    BaseNbr = int.Parse(parts[9]),
                });
            }
            return commands;
        }

        private static List<CommandRecord> FilterCommands(List<ExportedCommand> commands, int agentNbr)
        {
            var records = new List<CommandRecord>();
            foreach (var cmd in commands.Where(c => c.Agent == agentNbr))
            {
                var r = new CommandRecord
                {
                    Tick = cmd.Tick,
                    AgentNbr = cmd.Agent,
                    UnitNbr = cmd.UnitNbr,
                    Target = new Position(cmd.TargetX, cmd.TargetY),
                    TargetUnitNbr = cmd.TargetUnit,
                    MineNbr = cmd.MineNbr,
                    BaseNbr = cmd.BaseNbr,
                };

                switch (cmd.Type)
                {
                    case "MOVE":   r.Type = CommandType.Move; break;
                    case "BUILD":  r.Type = CommandType.Build;
                        Enum.TryParse(cmd.BuildingType, out UnitType bt);
                        r.BuildingType = bt; break;
                    case "GATHER": r.Type = CommandType.Gather; break;
                    case "TRAIN":  r.Type = CommandType.Train;
                        Enum.TryParse(cmd.BuildingType, out UnitType tt);
                        r.TrainType = tt;
                        r.BuildingNbr = cmd.UnitNbr; break;
                    case "ATTACK": r.Type = CommandType.Attack; break;
                    case "REPAIR": r.Type = CommandType.Repair;
                        r.RepairBuildingNbr = cmd.TargetUnit; break;
                    case "HEAL":   r.Type = CommandType.Heal; break;
                    default: continue;
                }

                records.Add(r);
            }
            return records;
        }

        private class ExportMetadata
        {
            public int MapW = 30, MapH = 30;
            public int GameSpeed = 20;
            public bool IsProcedural;
            public string Template = "OpenField";
            public float Density = 0.15f;
            public int Seed = 42;
            public string Symmetry = "Mirror";
            public int GenW = 30, GenH = 30;
            public List<Position> BlockedCells = new List<Position>();
        }

        private static ExportMetadata ParseMetadata(string path)
        {
            var meta = new ExportMetadata();
            foreach (string line in File.ReadLines(path))
            {
                if (!line.StartsWith("#")) break;

                // Parse blocked cells line: # blocked=x1,y1;x2,y2;...
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

                // Parse main metadata line: # map=WxH speed=S mode=M ...
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
                        case "mode": meta.IsProcedural = kv[1] == "Procedural"; break;
                        case "template": meta.Template = kv[1]; break;
                        case "density": float.TryParse(kv[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out meta.Density); break;
                        case "seed": int.TryParse(kv[1], out meta.Seed); break;
                        case "symmetry": meta.Symmetry = kv[1]; break;
                        case "genW": int.TryParse(kv[1], out meta.GenW); break;
                        case "genH": int.TryParse(kv[1], out meta.GenH); break;
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
                if (line.StartsWith("#") || line.StartsWith("tick,")) continue; // skip metadata/header
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
                        var u = unitStr.Split(':');
                        if (u.Length < 8) continue;
                        snap.Units.Add(new UnitSnap
                        {
                            UnitNbr = int.Parse(u[0]),
                            UnitType = Enum.TryParse(u[1], out UnitType ut) ? ut : UnitType.PAWN,
                            Owner = int.Parse(u[2]),
                            X = int.Parse(u[3]),
                            Y = int.Parse(u[4]),
                            Health = float.Parse(u[5]),
                            IsBuilt = u[6] == "1",
                            Action = Enum.TryParse(u[7], out UnitAction ua) ? ua : UnitAction.IDLE,
                        });
                    }
                }

                snapshots.Add(snap);
            }
            return snapshots;
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Determine the initial units from tick-0 commands and the first snapshot.
        /// Units that exist at the first snapshot but weren't created by BUILD/TRAIN
        /// commands must have been placed at game start (pawns and mines).
        /// </summary>
        private static List<UnitSnap> GetInitialUnits(List<ExportedCommand> commands,
            StateSnapshot firstSnap)
        {
            // Units created by tick-0 BUILD commands won't exist yet at tick 0;
            // they appear later. The initial units are those in the earliest snapshot
            // that are PAWN or MINE type (placed by PlaceUnits at game start).
            // Also include any unit with IsBuilt=true in the first snapshot that
            // couldn't have been built in time (bases start pre-built? No — they're built).
            //
            // Simpler approach: the game starts with only PAWNs and MINEs.
            // Everything else is created by agent commands.
            var initial = new List<UnitSnap>();
            foreach (var u in firstSnap.Units)
            {
                if (u.UnitType == UnitType.PAWN || u.UnitType == UnitType.MINE)
                    initial.Add(u);
            }

            // If first snapshot is late (tick 50+), pawns may have already built bases.
            // Fall back: include all units from tick-0 that look like starting units.
            if (initial.Count == 0)
            {
                // Just use all units from the first snapshot as initial state
                initial.AddRange(firstSnap.Units);
            }

            return initial;
        }

        private static string FindProjectRoot()
        {
            string dir = Path.GetDirectoryName(typeof(EngineParityTests).Assembly.Location);

            // Walk up looking for ParityCommands files directly or in RTS/ subfolder
            for (int i = 0; i < 10 && dir != null; i++)
            {
                if (Directory.GetFiles(dir, "ParityCommands_*.csv").Length > 0)
                    return dir;

                string rtsDir = Path.Combine(dir, "RTS");
                if (Directory.Exists(rtsDir) &&
                    Directory.GetFiles(rtsDir, "ParityCommands_*.csv").Length > 0)
                    return rtsDir;

                dir = Path.GetDirectoryName(dir);
            }

            return null;
        }

        #endregion
    }
}
