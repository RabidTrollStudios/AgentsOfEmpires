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

            // The command trace (what Unity commanded, per tick) pairs with the state export by
            // timestamp: ParityState_<ts>.csv <-> ParityCommands_<ts>.csv. Loaded lazily only if
            // a divergence fires, so a green run pays nothing.
            string commandFile = DeriveCommandFile(stateFile);

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

            // The grid digests are hashed over the PLAYABLE region only — the generated map size,
            // shared by both engines at the (0,0) origin. Unity's grid additionally spans a
            // water/border margin outside this region that the sim never builds; hashing the full
            // grid would compare non-existent cells. For hand-made maps (no gen size) fall back to
            // the sim's grid dimensions.
            int digestRegionW = meta.IsProcedural ? meta.GenW : game.Map.Grid.Width;
            int digestRegionH = meta.IsProcedural ? meta.GenH : game.Map.Grid.Height;

            int snapshotIdx = 0;
            int passed = 0, failed = 0;

            // Repro window: a small ring buffer of the most recent CHECKED ticks' state for the
            // eventual culprit unit(s). When the first divergence fires, replaying these shows
            // the state that LED INTO the wrong decision — the tick before is usually where the
            // two engines still agreed, making the exact decision point obvious.
            const int ReproWindow = 3;
            var window = new Queue<TickSnapshotLite>(ReproWindow + 1);

            // Divergence timeline: one root-cause signature per diverging tick, so a whole run
            // reveals whether a failure is ONE cause cascading or SEVERAL independent breaks
            // surfacing at different ticks. Consecutive identical signatures are collapsed into
            // a tick RANGE at report time so a 200-tick cascade is one line, not 200.
            var timeline = new List<(int Tick, Diff Primary, int DiffCount)>();
            bool reportedFirst = false;

            for (int t = 0; t < maxTick; t++)
            {
                game.Tick();
                int tick = t + 1;

                if (snapshotIdx < checkSnapshots.Count && checkSnapshots[snapshotIdx].Tick == tick)
                {
                    var snap = checkSnapshots[snapshotIdx];
                    var diffs = CompareState(game, snap, digestRegionW, digestRegionH);

                    // Record this checked tick into the repro ring buffer BEFORE handling diffs,
                    // capturing the full unit set so we can later slice out whichever unit turns
                    // out to be the culprit.
                    window.Enqueue(CaptureLite(game, snap, tick));
                    while (window.Count > ReproWindow) window.Dequeue();

                    if (diffs.Count > 0)
                    {
                        var primary = PrimaryDiff(diffs);
                        timeline.Add((tick, primary, diffs.Count));

                        if (!reportedFirst)
                        {
                            reportedFirst = true;

                            // Root-cause verdict FIRST — a single greppable line naming the most
                            // upstream diverging field, so the actual cause is unmissable even if
                            // the cascade below is truncated. Format is stable for tooling/grep:
                            //   DIVERGE tick=N unit=U field=F sim=X engine=Y
                            _output.WriteLine(
                                $"DIVERGE tick={tick} unit={primary.UnitNbr} field={primary.Field} " +
                                $"sim={primary.SimVal} engine={primary.EngineVal}");

                            // Repro window: the culprit unit's state across the last few checked
                            // ticks, so the lead-in to the wrong decision is visible.
                            if (primary.UnitNbr >= 0)
                                PrintReproWindow(window, primary.UnitNbr);

                            // Command trace: what Unity commanded the culprit unit around the
                            // divergence tick, so the CAUSING command (not just the resulting
                            // state) is visible. Shows a few ticks of lead-in.
                            if (primary.UnitNbr >= 0 && commandFile != null)
                                PrintCommandTrace(commandFile, primary.UnitNbr, tick, ReproWindow);

                            _output.WriteLine($"FIRST DIVERGENCE at tick {tick} ({diffs.Count} field(s)):");
                            // Print in root-cause order (most upstream first), not collection order,
                            // so the cause sits at the top of the detail list too.
                            var ordered = diffs
                                .OrderBy(d => d.FieldRank)
                                .ThenBy(d => d.UnitNbr < 0 ? int.MaxValue : d.UnitNbr)
                                .ToList();
                            foreach (var diff in ordered.Take(20))
                                _output.WriteLine($"  {diff}");
                            if (ordered.Count > 20)
                                _output.WriteLine($"  ... and {ordered.Count - 20} more");
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

            if (timeline.Count > 0)
                PrintTimeline(timeline);

            _output.WriteLine($"Results: {passed} snapshots matched, {failed} diverged (of {checkSnapshots.Count} total)");
            Assert.Equal(0, failed);
        }

        #region Timeline + repro window

        /// <summary>
        /// A compact per-unit state line captured at one checked tick, keyed by unitNbr, holding
        /// BOTH the sim value and the Unity-recorded value so the repro window can show them side
        /// by side without re-running the game. Only the fields the recording carried are shown.
        /// </summary>
        private struct TickSnapshotLite
        {
            public int Tick;
            public Dictionary<int, string> UnitLines; // unitNbr -> "sim{...} engine{...}"
        }

        /// <summary>Snapshot every recorded unit's sim-vs-engine state at one checked tick.</summary>
        private static TickSnapshotLite CaptureLite(SimGame game, StateSnapshot snap, int tick)
        {
            var lines = new Dictionary<int, string>();
            foreach (var eu in snap.Units)
            {
                var su = game.GetUnit(eu.UnitNbr);
                string sim = su == null ? "<absent>" : FormatLite((AgentSDK.ITickUnit)su);
                string eng = FormatLite(eu);
                lines[eu.UnitNbr] = sim == eng ? $"= {sim}" : $"sim[{sim}] eng[{eng}]";
            }
            return new TickSnapshotLite { Tick = tick, UnitLines = lines };
        }

        /// <summary>Compact one-line state used in the repro window (decision-first fields).</summary>
        private static string FormatLite(AgentSDK.ITickUnit u)
            => $"a={u.CurrentAction},pos=({u.GridPosition.X},{u.GridPosition.Y}),hp={u.Health:F1}," +
               $"atk={u.AttackTargetNbr},bld={u.BuildTargetNbr},gph={u.GatherPhase},pi={u.PathIndex}";

        private static string FormatLite(UnitSnap u)
            => $"a={u.Action},pos=({u.X},{u.Y}),hp={u.Health:F1}," +
               $"atk={u.AttackTargetNbr},bld={u.BuildTargetNbr},gph={u.GatherPhase},pi={u.PathIndex}";

        private void PrintReproWindow(Queue<TickSnapshotLite> window, int unitNbr)
        {
            _output.WriteLine($"  --- repro window (unit {unitNbr}, last {window.Count} checked ticks) ---");
            foreach (var w in window)
            {
                string line = w.UnitLines.TryGetValue(unitNbr, out var l) ? l : "<not present>";
                _output.WriteLine($"    t{w.Tick}: {line}");
            }
            _output.WriteLine($"  --- end repro window ---");
        }

        /// <summary>
        /// Print the divergence timeline: the sequence of DISTINCT root-cause signatures across
        /// the whole run, each as a tick range. One signature spanning many ticks = a single
        /// cascade. Multiple signatures = independent breaks worth investigating separately.
        /// </summary>
        private void PrintTimeline(List<(int Tick, Diff Primary, int DiffCount)> timeline)
        {
            _output.WriteLine($"--- divergence timeline ({timeline.Count} diverging ticks) ---");
            foreach (var row in BuildTimelineRows(timeline, maxRows: 30))
                _output.WriteLine($"  {row}");
            _output.WriteLine($"--- end timeline ---");
        }

        /// <summary>
        /// Collapse a per-diverging-tick list into distinct root-cause rows, each a tick RANGE.
        /// Pure (returns strings, no I/O) so it can be unit-tested — the collapse logic is the
        /// part that can silently regress, not the WriteLine around it. Consecutive ticks with
        /// the same unit:field signature merge into one "t{start}-{end}" row carrying the peak
        /// field count seen in that span. Rows beyond <paramref name="maxRows"/> are summarized.
        /// </summary>
        private static List<string> BuildTimelineRows(
            List<(int Tick, Diff Primary, int DiffCount)> timeline, int maxRows)
        {
            var rows = new List<string>();
            int i = 0;
            while (i < timeline.Count && rows.Count < maxRows)
            {
                var sig = Sig(timeline[i].Primary);
                int startTick = timeline[i].Tick;
                int endTick = startTick;
                int maxDiffs = timeline[i].DiffCount;
                int j = i + 1;
                while (j < timeline.Count && Sig(timeline[j].Primary) == sig)
                {
                    endTick = timeline[j].Tick;
                    maxDiffs = Math.Max(maxDiffs, timeline[j].DiffCount);
                    j++;
                }
                string range = startTick == endTick ? $"t{startTick}" : $"t{startTick}-{endTick}";
                var p = timeline[i].Primary;
                rows.Add($"{range}: unit={p.UnitNbr} field={p.Field} sim={p.SimVal} " +
                         $"engine={p.EngineVal} (peak {maxDiffs} field(s))");
                i = j;
            }
            if (i < timeline.Count)
            {
                // Count remaining DISTINCT signatures so the summary reflects unresolved causes,
                // not just leftover ticks of an already-shown cascade.
                rows.Add($"... {timeline.Count - i} more diverging tick(s) collapsed");
            }
            return rows;
        }

        private static string Sig(Diff d) => $"{d.UnitNbr}:{d.Field}";

        #endregion

        #region Command-trace correlation

        /// <summary>
        /// A single row from the Unity command export (ParityCommands_*.csv). Only the fields
        /// useful for correlating a divergence are kept; the rest are echoed as a raw tail.
        /// </summary>
        private struct CommandRow
        {
            public int Tick, Agent, Unit, TargetX, TargetY, TargetUnit, Mine, Base;
            public string Type, BuildingType;

            public override string ToString()
            {
                // Compact, only showing the target fields relevant to the command type.
                string tgt = Type switch
                {
                    "BUILD" or "REPAIR" => $"->({TargetX},{TargetY}) {BuildingType}",
                    "GATHER" => $"mine={Mine} base={Base}",
                    "TRAIN" => $"{BuildingType}",
                    "ATTACK" or "HEAL" => $"->unit={TargetUnit}",
                    "MOVE" => $"->({TargetX},{TargetY})",
                    _ => "",
                };
                return $"agent={Agent} {Type} {tgt}".TrimEnd();
            }
        }

        /// <summary>Map ParityState_&lt;ts&gt;.csv -> ParityCommands_&lt;ts&gt;.csv (same directory).</summary>
        private static string DeriveCommandFile(string stateFile)
        {
            string dir = Path.GetDirectoryName(stateFile);
            string name = Path.GetFileName(stateFile);
            if (dir == null || !name.StartsWith("ParityState_")) return null;
            string cmd = Path.Combine(dir, "ParityCommands_" + name.Substring("ParityState_".Length));
            return File.Exists(cmd) ? cmd : null;
        }

        /// <summary>
        /// Print the commands Unity issued to <paramref name="unitNbr"/> in the tick window
        /// [divergeTick - lead, divergeTick]. If the culprit unit received no direct command in
        /// that window (common — the divergence may be a downstream physics/timer effect), fall
        /// back to ALL commands at the divergence tick so the tick's activity is still visible.
        /// </summary>
        private void PrintCommandTrace(string commandFile, int unitNbr, int divergeTick, int lead)
        {
            List<CommandRow> rows;
            try { rows = ParseCommands(commandFile); }
            catch (Exception ex)
            {
                _output.WriteLine($"  --- command trace unavailable: {ex.Message} ---");
                return;
            }

            int lo = divergeTick - lead;
            var forUnit = rows
                .Where(r => r.Unit == unitNbr && r.Tick >= lo && r.Tick <= divergeTick)
                .OrderBy(r => r.Tick).ToList();

            _output.WriteLine($"  --- command trace (Unity), unit {unitNbr}, ticks {lo}-{divergeTick} ---");
            if (forUnit.Count > 0)
            {
                foreach (var r in forUnit)
                    _output.WriteLine($"    t{r.Tick}: {r}");
            }
            else
            {
                _output.WriteLine($"    (no direct command to unit {unitNbr} in window — " +
                                  $"showing all commands at tick {divergeTick})");
                foreach (var r in rows.Where(r => r.Tick == divergeTick).OrderBy(r => r.Agent).ThenBy(r => r.Unit))
                    _output.WriteLine($"    t{r.Tick}: unit={r.Unit} {r}");
            }
            _output.WriteLine($"  --- end command trace ---");
        }

        private static List<CommandRow> ParseCommands(string path)
        {
            var rows = new List<CommandRow>();
            foreach (string line in File.ReadLines(path))
            {
                if (line.StartsWith("tick,") || string.IsNullOrWhiteSpace(line)) continue;
                var c = line.Split(',');
                if (c.Length < 10) continue;
                rows.Add(new CommandRow
                {
                    Tick = ParseInt(c[0]), Agent = ParseInt(c[1]), Type = c[2], Unit = ParseInt(c[3]),
                    TargetX = ParseInt(c[4]), TargetY = ParseInt(c[5]), TargetUnit = ParseInt(c[6]),
                    BuildingType = c[7], Mine = ParseInt(c[8]), Base = ParseInt(c[9]),
                });
            }
            return rows;
        }

        private static int ParseInt(string s) => int.TryParse(s, out int v) ? v : 0;

        #endregion

        #region Root-cause verdict self-tests

        // These prove the DIAGNOSTIC itself is correct — that when a cascade of diffs is
        // present, PrimaryDiff/FieldRankOrder surface the most-upstream (root-cause) field,
        // not the loudest downstream symptom. Without these, a mis-ordered FieldRankOrder
        // would silently point the verdict line at the wrong field and no one would notice
        // until chasing a real divergence.

        [Fact]
        public void PrimaryDiff_PicksDecisionOverDownstreamSymptoms()
        {
            // A realistic cascade: a unit chose a different ACTION (the cause), which produced
            // a different position, health, and a game-level gold mismatch (the symptoms).
            var cascade = new List<Diff>
            {
                new Diff { UnitNbr = -1, Field = "Gold[0]", FieldRank = RankOf("Gold[0]"), SimVal = "600", EngineVal = "700" },
                new Diff { UnitNbr = 12, Field = "health",  FieldRank = RankOf("health"),  SimVal = "40.0", EngineVal = "60.0" },
                new Diff { UnitNbr = 12, Field = "pos",     FieldRank = RankOf("pos"),     SimVal = "(5,6)", EngineVal = "(7,6)" },
                new Diff { UnitNbr = 7,  Field = "action",  FieldRank = RankOf("action"),  SimVal = "GATHER", EngineVal = "BUILD" },
            };

            var primary = PrimaryDiff(cascade);
            Assert.Equal("action", primary.Field);
            Assert.Equal(7, primary.UnitNbr);
        }

        [Fact]
        public void PrimaryDiff_TiesBrokenByLowestUnitNbr()
        {
            // Two units diverge on the SAME (most-upstream) field — the verdict must be
            // deterministic, always naming the lowest unitNbr.
            var diffs = new List<Diff>
            {
                new Diff { UnitNbr = 9, Field = "attackTarget", FieldRank = RankOf("attackTarget"), SimVal = "3", EngineVal = "4" },
                new Diff { UnitNbr = 2, Field = "attackTarget", FieldRank = RankOf("attackTarget"), SimVal = "3", EngineVal = "5" },
            };
            Assert.Equal(2, PrimaryDiff(diffs).UnitNbr);
        }

        [Fact]
        public void PrimaryDiff_UnitDecisionBeatsGameLevelAggregate()
        {
            // A specific unit's wrong decision should outrank an aggregate gold mismatch even
            // though both are "the first thing we noticed" — the aggregate is downstream.
            var diffs = new List<Diff>
            {
                new Diff { UnitNbr = -1, Field = "Gold[1]", FieldRank = RankOf("Gold[1]"), SimVal = "0", EngineVal = "40" },
                new Diff { UnitNbr = 4,  Field = "buildTarget", FieldRank = RankOf("buildTarget"), SimVal = "-1", EngineVal = "8" },
            };
            var primary = PrimaryDiff(diffs);
            Assert.Equal("buildTarget", primary.Field);
            Assert.Equal(4, primary.UnitNbr);
        }

        [Fact]
        public void FieldRankOrder_RanksDecisionsAboveDerivedState()
        {
            // Guard the ORDERING invariant directly: every decision/intent field must rank
            // strictly above the physical/aggregate symptoms it can cause. If someone reorders
            // FieldRankOrder and breaks this, the verdict line silently regresses.
            Assert.True(RankOf("action") < RankOf("pos"));
            Assert.True(RankOf("action") < RankOf("health"));
            Assert.True(RankOf("attackTarget") < RankOf("pos"));
            Assert.True(RankOf("buildTarget") < RankOf("isBuilt"));
            Assert.True(RankOf("pos") < RankOf("Gold[0]"));      // unit-level before game-level
            Assert.True(RankOf("health") < RankOf("UnitCount")); // field diff before presence
        }

        [Fact]
        public void BuildTimelineRows_CollapsesCascadeIntoRange()
        {
            // A single root cause (unit 7 action) persists across ticks 40-43, then a genuinely
            // NEW cause (unit 9 attackTarget) appears at 50. The timeline must show TWO rows —
            // one collapsed range for the cascade, one for the new break — not five rows.
            Diff a = new Diff { UnitNbr = 7, Field = "action", SimVal = "GATHER", EngineVal = "BUILD" };
            Diff b = new Diff { UnitNbr = 9, Field = "attackTarget", SimVal = "3", EngineVal = "4" };
            var timeline = new List<(int, Diff, int)>
            {
                (40, a, 1), (41, a, 3), (42, a, 8), (43, a, 8), (50, b, 12),
            };

            var rows = BuildTimelineRows(timeline, maxRows: 30);
            Assert.Equal(2, rows.Count);
            Assert.Contains("t40-43", rows[0]);
            Assert.Contains("field=action", rows[0]);
            Assert.Contains("peak 8", rows[0]);     // peak diff count across the span
            Assert.Contains("t50", rows[1]);
            Assert.Contains("field=attackTarget", rows[1]);
        }

        [Fact]
        public void BuildTimelineRows_SummarizesBeyondMaxRows()
        {
            // Each tick a DISTINCT signature (different unit) so nothing collapses; with maxRows=2
            // we expect 2 rows + 1 summary line.
            var timeline = new List<(int, Diff, int)>();
            for (int u = 0; u < 5; u++)
                timeline.Add((10 + u, new Diff { UnitNbr = u, Field = "pos", SimVal = "(0,0)", EngineVal = "(1,1)" }, 1));

            var rows = BuildTimelineRows(timeline, maxRows: 2);
            Assert.Equal(3, rows.Count);            // 2 shown + summary
            Assert.Contains("more diverging tick(s) collapsed", rows[2]);
        }

        [Fact]
        public void ParseCommands_ReadsRowsAndFormatsByType()
        {
            string tmp = Path.Combine(Path.GetTempPath(), "parity_selftest_cmds.csv");
            File.WriteAllLines(tmp, new[]
            {
                "tick,agent,type,unit,target_x,target_y,target_unit,building_type,mine,base",
                "5,0,BUILD,7,12,13,-1,BARRACKS,-1,-1",
                "5,1,GATHER,3,0,0,-1,,2,4",
                "6,0,ATTACK,7,0,0,9,,-1,-1",
            });
            try
            {
                var rows = ParseCommands(tmp);
                Assert.Equal(3, rows.Count);

                var build = rows[0];
                Assert.Equal(5, build.Tick);
                Assert.Equal(7, build.Unit);
                Assert.Equal("BUILD", build.Type);
                Assert.Contains("->(12,13) BARRACKS", build.ToString());

                Assert.Contains("mine=2 base=4", rows[1].ToString());   // GATHER shows mine/base
                Assert.Contains("->unit=9", rows[2].ToString());        // ATTACK shows target unit
            }
            finally { File.Delete(tmp); }
        }

        [Fact]
        public void DeriveCommandFile_PairsByTimestamp()
        {
            // Non-existent files -> null (File.Exists gate). We can still prove the NAME mapping
            // by pointing at a temp pair we create.
            string dir = Path.GetTempPath();
            string ts = "20990101_000000";
            string state = Path.Combine(dir, $"ParityState_{ts}.csv");
            string cmd = Path.Combine(dir, $"ParityCommands_{ts}.csv");
            File.WriteAllText(state, "# stub\n");
            File.WriteAllText(cmd, "tick,agent,type,unit,target_x,target_y,target_unit,building_type,mine,base\n");
            try
            {
                Assert.Equal(cmd, DeriveCommandFile(state));
            }
            finally { File.Delete(state); File.Delete(cmd); }
        }

        [Fact]
        public void FormatLite_EqualStatesRenderIdentically()
        {
            // The repro window marks a unit "= ..." only when sim and engine agree. Prove the two
            // FormatLite overloads produce byte-identical strings for matching state, so an
            // agreeing lead-in tick is unambiguously distinguishable from a diverging one.
            var eng = new UnitSnap
            {
                Action = UnitAction.GATHER, X = 5, Y = 6, Health = 40f,
                AttackTargetNbr = -1, BuildTargetNbr = -1, GatherPhase = GatherPhase.TO_MINE, PathIndex = 2,
            };
            string engStr = FormatLite(eng);
            Assert.Contains("a=GATHER", engStr);
            Assert.Contains("pos=(5,6)", engStr);
            Assert.Contains("gph=TO_MINE", engStr);
            Assert.Contains("pi=2", engStr);
        }

        #endregion

        #region State Comparison

        /// <summary>
        /// One field-level divergence between the sim and the Unity recording. Carrying the
        /// unit number and a canonical field RANK (not just a formatted string) lets the
        /// reporter pick the single most-upstream diff — the likely ROOT CAUSE — out of a
        /// cascade, rather than dumping every downstream consequence in arbitrary order.
        /// </summary>
        private struct Diff
        {
            public int UnitNbr;     // -1 for game-level diffs (gold, unit count)
            public string Field;    // canonical field key, e.g. "action", "buildTarget", "pos"
            public int FieldRank;   // lower = more upstream / more likely the cause
            public string SimVal;
            public string EngineVal;

            public override string ToString()
            {
                string who = UnitNbr >= 0 ? $"Unit {UnitNbr}: " : "";
                return $"{who}{Field} sim={SimVal} engine={EngineVal}";
            }
        }

        /// <summary>
        /// Canonical field ordering for root-cause ranking, most-upstream first. A divergence
        /// almost always starts as a single wrong DECISION (which target, which action, which
        /// build cell) and everything else — position, health, gold, unit count — is downstream
        /// fallout that only appears a tick or two later. Ranking decision/intent fields above
        /// derived/physical fields means the verdict line names the field that actually broke,
        /// not the loudest symptom. Ties (same rank) fall back to lowest unitNbr.
        /// </summary>
        private static readonly string[] FieldRankOrder =
        {
            // Intent / decisions — the usual root cause.
            "action", "attackTarget", "buildTarget", "repairTarget", "healTarget",
            "gatherPhase", "gatherMine", "gatherBase", "trainTarget", "buildTarget2",
            // Task progress driven by those decisions.
            "taskTimer", "buildProgress", "miningTimer", "repathPending",
            // Derived engine internals — a slot/occupancy skew can PRECEDE a visible position
            // change, so rank these above movement/physical fields as candidate causes. pbSlots
            // (roster/tick agreement) is the most upstream of the three.
            "pbSlotsDigest", "occDigest", "walkDigest",
            // Movement, derived from a chosen path.
            "pathIndex", "pathProgress", "pos",
            // Physical/aggregate state — almost always a downstream symptom.
            "isBuilt", "health", "mana", "goldCarried", "type", "owner",
            // Whole-unit presence and game-level aggregates — the loudest, latest symptoms.
            "missing", "extra", "UnitCount", "Gold[0]", "Gold[1]",
        };

        private static int RankOf(string field)
        {
            int i = Array.IndexOf(FieldRankOrder, field);
            return i >= 0 ? i : FieldRankOrder.Length; // unknown fields rank last
        }

        private List<Diff> CompareState(SimGame game, StateSnapshot snap, int digestRegionW, int digestRegionH)
        {
            var diffs = new List<Diff>();

            void G(string field, object simVal, object engVal) => diffs.Add(new Diff
            {
                UnitNbr = -1, Field = field, FieldRank = RankOf(field),
                SimVal = simVal.ToString(), EngineVal = engVal.ToString(),
            });

            if (game.GetGold(0) != snap.Gold0) G("Gold[0]", game.GetGold(0), snap.Gold0);
            if (game.GetGold(1) != snap.Gold1) G("Gold[1]", game.GetGold(1), snap.Gold1);

            // Collect all sim units
            var simUnits = new List<SimUnit>();
            int maxUnitNbr = snap.Units.Count > 0 ? snap.Units.Max(u => u.UnitNbr) + 20 : 20;
            for (int i = 0; i < maxUnitNbr; i++)
            {
                var su = game.GetUnit(i);
                if (su != null) simUnits.Add(su);
            }

            if (simUnits.Count != snap.UnitCount) G("UnitCount", simUnits.Count, snap.UnitCount);

            foreach (var eu in snap.Units)
            {
                var su = game.GetUnit(eu.UnitNbr);
                if (su == null)
                {
                    diffs.Add(new Diff
                    {
                        UnitNbr = eu.UnitNbr, Field = "missing", FieldRank = RankOf("missing"),
                        SimVal = "<absent>", EngineVal = eu.UnitType.ToString(),
                    });
                    continue;
                }

                // Each field is compared only if the recording carried it (eu.Present),
                // so pre-instrumentation CSVs still validate on the original 8 fields while
                // full recordings validate every ITickUnit field. sim side (su) is read via
                // ITickUnit so both engines expose identical semantics.
                var p = eu.Present ?? EightFieldKeys;
                AgentSDK.ITickUnit s = su;

                void D(string field, object simVal, object engVal) => diffs.Add(new Diff
                {
                    UnitNbr = eu.UnitNbr, Field = field, FieldRank = RankOf(field),
                    SimVal = simVal.ToString(), EngineVal = engVal.ToString(),
                });

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
                    diffs.Add(new Diff
                    {
                        UnitNbr = su.UnitNbr, Field = "extra", FieldRank = RankOf("extra"),
                        SimVal = su.UnitType.ToString(), EngineVal = "<absent>",
                    });
            }

            // Derived engine-internal digests (#17): compare occupancy / walkability / PathBudget
            // slot grants, computed on the SIM's grid via the SAME shared AgentSDK helpers the
            // exporter used, so a mismatch is a genuine internal divergence — not a hashing
            // artifact. Only checked when the recording carried them (HasDigests).
            if (snap.HasDigests)
            {
                // Hash the SAME playable region the exporter used (see digestRegionW/H) so Unity's
                // larger water-bordered grid and the sim's playable-only grid produce comparable
                // digests.
                ulong simOcc = game.Map.Grid.ComputeOccupancyDigest(digestRegionW, digestRegionH);
                ulong simWalk = game.Map.Grid.ComputeWalkabilityDigest(digestRegionW, digestRegionH);
                // Live unit numbers in ascending order — must match the exporter's ordering.
                var unitNbrsAsc = simUnits.Select(u => u.UnitNbr).OrderBy(n => n).ToList();
                ulong simPb = AgentSDK.PathBudget.ComputeSlotDigest(game.CurrentTick, unitNbrsAsc);

                if (simOcc != snap.OccDigest) G("occDigest", simOcc, snap.OccDigest);
                if (simWalk != snap.WalkDigest) G("walkDigest", simWalk, snap.WalkDigest);
                if (simPb != snap.PbSlotsDigest) G("pbSlotsDigest", simPb, snap.PbSlotsDigest);
            }

            return diffs;
        }

        /// <summary>
        /// Pick the single most-upstream diff — the likely root cause — from a cascade:
        /// lowest field rank first (decisions before symptoms), then lowest unitNbr as a
        /// deterministic tiebreak. Game-level diffs (unitNbr -1) sort after unit diffs at the
        /// same rank so a specific unit's wrong decision beats an aggregate gold mismatch.
        /// </summary>
        private static Diff PrimaryDiff(List<Diff> diffs)
            => diffs.OrderBy(d => d.FieldRank)
                    .ThenBy(d => d.UnitNbr < 0 ? int.MaxValue : d.UnitNbr)
                    .First();

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
            // Derived engine-internal digests (grid occupancy/walkability, PathBudget slots).
            // Present only in CSVs recorded after the #17 instrumentation; HasDigests gates the
            // comparison so older recordings don't spuriously "fail" on absent columns.
            public bool HasDigests;
            public ulong OccDigest, WalkDigest, PbSlotsDigest;
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

                // Trailing digests column (#17): occ=<ulong>;walk=<ulong>;pbslots=<ulong>.
                // Optional — only present in post-instrumentation CSVs. The units column never
                // contains a comma (units are '|'-separated), so any 6th field is the digests.
                if (parts.Length >= 6 && parts[5].Contains("occ="))
                    ParseDigests(parts[5], snap);

                snapshots.Add(snap);
            }
            return snapshots;
        }

        /// <summary>Parse the trailing digests column: occ=&lt;ulong&gt;;walk=&lt;ulong&gt;;pbslots=&lt;ulong&gt;.</summary>
        private static void ParseDigests(string field, StateSnapshot snap)
        {
            foreach (var pair in field.Split(';'))
            {
                int eq = pair.IndexOf('=');
                if (eq <= 0) continue;
                string key = pair.Substring(0, eq);
                if (!ulong.TryParse(pair.Substring(eq + 1), out ulong v)) continue;
                switch (key)
                {
                    case "occ": snap.OccDigest = v; snap.HasDigests = true; break;
                    case "walk": snap.WalkDigest = v; break;
                    case "pbslots": snap.PbSlotsDigest = v; break;
                }
            }
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
