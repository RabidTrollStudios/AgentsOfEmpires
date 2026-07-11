using System.Collections.Generic;

namespace AgentSDK
{
    /// <summary>
    /// Deterministic sorting helpers for agent code.
    ///
    /// <para>
    /// <see cref="List{T}.Sort(System.Comparison{T})"/> uses an UNSTABLE introsort, and its
    /// tie-breaking order differs between runtimes — notably Unity's Mono/IL2CPP versus the
    /// headless .NET runtime used by the simulation harness. When an agent sorts candidate
    /// positions purely by distance, grid cells that are equidistant from the target tie, and
    /// the two runtimes can order those ties differently. The agent then picks a different
    /// build/placement position in Unity than in the headless sim — a parity divergence
    /// (see issue #4: barracks placed at a different position at ~tick 28).
    /// </para>
    ///
    /// <para>
    /// These helpers sort by the primary key and break ties with a fixed, coordinate-based
    /// order, so the result is identical on every runtime. Prefer them over a bare
    /// <c>list.Sort((a, b) =&gt; Distance(a, t).CompareTo(Distance(b, t)))</c> anywhere the
    /// result feeds a decision that must match between Unity and the simulation.
    /// </para>
    /// </summary>
    public static class DeterministicSort
    {
        /// <summary>
        /// Deterministic ordering of two positions: lower X first, then lower Y.
        /// Total order over the grid, so it fully resolves any tie.
        /// </summary>
        public static int Compare(Position a, Position b)
        {
            int c = a.X.CompareTo(b.X);
            if (c != 0) return c;
            return a.Y.CompareTo(b.Y);
        }

        /// <summary>
        /// Sort <paramref name="positions"/> in place by distance to <paramref name="target"/>,
        /// ascending, breaking ties by <see cref="Compare"/> so the order is identical on
        /// every runtime.
        /// </summary>
        public static void SortByDistance(List<Position> positions, Position target)
        {
            positions.Sort((a, b) =>
            {
                int c = Position.Distance(a, target).CompareTo(Position.Distance(b, target));
                if (c != 0) return c;
                return Compare(a, b);
            });
        }
    }
}
