using System.Collections.Generic;
using System.Linq;

namespace AgentSDK
{
    /// <summary>
    /// Shared simulation step runner. Orchestrates the full sequence of
    /// task advancement and movement in a single deterministic call.
    /// Both Unity (via FixedUpdate) and SimGame call this to guarantee
    /// identical game logic execution order.
    /// </summary>
    public static class SimulationRunner
    {
        /// <summary>
        /// Advance the simulation by one fixed step:
        /// 1. Task logic (StepEngine.AdvanceAllUnits — actions, mana, death)
        /// 2. Movement (MovementSystem.Advance per unit, sorted by UnitNbr)
        /// </summary>
        public static void AdvanceStep(ISimWorld world, ISimCallbacks callbacks)
        {
            // Phase 1: Task logic — action state machines, mana regen, dead removal
            StepEngine.AdvanceAllUnits(world, callbacks);

            // Phase 2: Movement — advance all units by one step's worth of distance
            var units = world.AllUnits.ToList();
            units.Sort((a, b) => a.UnitNbr.CompareTo(b.UnitNbr));
            foreach (var unit in units)
                MovementSystem.Advance(unit, world.StepDuration, world, callbacks);
        }
    }
}
