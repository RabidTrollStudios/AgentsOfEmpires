namespace AgentSDK
{
    /// <summary>
    /// Deterministic per-tick cap on pursuit re-pathing, shared by both engines.
    ///
    /// The problem it solves: when a large army's shared target vanishes in a single
    /// tick — classically, an enemy BASE (6x4) being destroyed while dozens of
    /// soldiers/lancers/archers surround it — every attacker is told to re-path on the
    /// SAME tick. Each pursuit pathfind costs ~0.7ms on the real map, so 80+ units
    /// re-pathing at once is a ~56ms frame hitch. The same thundering-herd shape shows
    /// up when a moving target outruns many pursuers' paths simultaneously, and (rarely)
    /// when a mine a whole worker crew is gathering runs out.
    ///
    /// The fix is a rate limit, NOT a behavior change: the unit keeps its assigned
    /// action and target (the PlanningAgent's target choice is never second-guessed —
    /// see U3). It is only rate-limited on WHEN it may compute its pursuit path. A unit
    /// that is gated out this tick keeps CurrentAction, clears TickPath, and sets
    /// RepathPending — the per-tick Advance* handlers already re-attempt a path whenever
    /// one is missing, so the deferred unit simply tries again next tick until the gate
    /// admits it. No RNG, no per-instance counters: the gate is a pure function of
    /// (tick, unitNbr), so Unity and SimGame admit exactly the same units on every tick.
    ///
    /// Data (PathfindingStressTest, real divergent-parity map): normal play peaks at
    /// ~15-29 pursuit re-paths/tick and never exceeds a 60fps frame; the herd is the only
    /// case that does. With SLOTS_PER_TICK=15 the worst-case pathfinding cost is held to
    /// roughly half a 60fps frame, and an 80-unit herd fully re-engages over ~6 ticks
    /// (~0.3s at 20Hz) — imperceptible in play, and the game never stutters.
    /// </summary>
    public static class PathBudget
    {
        /// <summary>
        /// How many gated pursuit pathfinds may run per tick. AI is conventionally
        /// budgeted to ~half a frame; 15 lands slightly over that in the rare herd
        /// case but keeps normal play well clear. Tune here only.
        /// </summary>
        public const int SLOTS_PER_TICK = 15;

        /// <summary>
        /// Rotating-window size. (unitNbr + tick) % SPREAD selects a slot in [0,SPREAD);
        /// slots below SLOTS_PER_TICK pass. Adding <c>tick</c> rotates the window every
        /// tick, so a deferred unit's slot shifts each tick and is guaranteed to enter
        /// the pass-window within SPREAD ticks — no unit is ever starved. SPREAD must be
        /// &gt;= SLOTS_PER_TICK; larger SPREAD spreads a big herd over more ticks.
        /// </summary>
        public const int SPREAD = 80;

        /// <summary>
        /// True if this unit is allowed to run a pursuit pathfind on this tick.
        /// Pure function of (tick, unitNbr) — identical across both engines.
        /// </summary>
        public static bool CanPathThisTick(int tick, int unitNbr)
        {
            // Non-negative modulo (unitNbr is always >= 0, tick >= 0, but guard anyway).
            int slot = ((unitNbr + tick) % SPREAD + SPREAD) % SPREAD;
            return slot < SLOTS_PER_TICK;
        }

        /// <summary>
        /// Run a gated pursuit pathfind for <paramref name="unit"/>. If the unit is
        /// admitted this tick, computes and returns the path (via <paramref name="pathfind"/>)
        /// and clears RepathPending. If gated out, returns null and sets RepathPending so the
        /// caller's per-tick handler retries next tick. Keeps the deterministic gate and the
        /// pending-flag bookkeeping in one place so every call site behaves identically.
        /// </summary>
        public static System.Collections.Generic.List<Position> GatedRepath(
            ITickWorld world, ITickUnit unit,
            System.Func<System.Collections.Generic.List<Position>> pathfind)
        {
            if (!CanPathThisTick(world.CurrentTick, unit.UnitNbr))
            {
                unit.RepathPending = true;
                return null;
            }
            unit.RepathPending = false;
            return pathfind();
        }

        /// <summary>
        /// Parity digest of the gate's grant/defer decision this tick over a set of unit
        /// numbers. Because <see cref="CanPathThisTick"/> is a pure function of (tick, unitNbr),
        /// both engines fed the SAME live-unit set produce the SAME digest — so a mismatch means
        /// the two engines disagree on which units EXIST this tick (a roster skew) or on the
        /// tick number itself, either of which is a real divergence that can precede any visible
        /// position change. The caller must pass unit numbers in ascending order for a stable
        /// value; sort before calling. FNV-1a for cross-runtime determinism.
        /// </summary>
        public static ulong ComputeSlotDigest(int tick,
            System.Collections.Generic.IEnumerable<int> ascendingUnitNbrs)
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            ulong h = offset;
            unchecked
            {
                foreach (int unitNbr in ascendingUnitNbrs)
                {
                    // Fold the unit number and its grant/defer bit together.
                    int v = (unitNbr << 1) | (CanPathThisTick(tick, unitNbr) ? 1 : 0);
                    h = (h ^ (byte)v) * prime;
                    h = (h ^ (byte)(v >> 8)) * prime;
                    h = (h ^ (byte)(v >> 16)) * prime;
                    h = (h ^ (byte)(v >> 24)) * prime;
                }
            }
            return h;
        }
    }
}
