using System.Linq;
using AgentSDK;

namespace AgentTestHarness
{
    /// <summary>
    /// Task advancement and movement. Task logic delegates to TickEngine.AdvanceAllUnits.
    /// Movement delegates to MovementSystem.Advance per unit per tick.
    /// </summary>
    public partial class SimGame
    {
        /// <summary>Lazy-initialized world adapter for TickEngine.</summary>
        private SimTickWorld tickWorld;

        private void AdvanceAllUnits()
        {
            if (tickWorld == null)
                tickWorld = new SimTickWorld(this);

            // Phase 1: Task logic (action state machines)
            TickEngine.AdvanceAllUnits(tickWorld, NullTickCallbacks.Instance);

            // Phase 2: Movement — advance all units by one tick's worth of distance
            var units = tickWorld.AllUnits.ToList();
            units.Sort((a, b) => a.UnitNbr.CompareTo(b.UnitNbr));
            foreach (var unit in units)
                MovementSystem.Advance(unit, Config.TickDuration, tickWorld, NullTickCallbacks.Instance);
        }
    }
}
