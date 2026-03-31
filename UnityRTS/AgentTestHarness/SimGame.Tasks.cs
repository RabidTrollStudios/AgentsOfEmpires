using AgentSDK;

namespace AgentTestHarness
{
    /// <summary>
    /// Task advancement: delegates entirely to <see cref="TickEngine.AdvanceAllUnits"/>.
    /// All game logic (movement, training, building, gathering, attack, repair, heal)
    /// lives in the shared AgentSDK TickEngine — no duplicate implementations here.
    /// </summary>
    public partial class SimGame
    {
        /// <summary>Lazy-initialized world adapter for TickEngine.</summary>
        private SimTickWorld tickWorld;

        private void AdvanceAllUnits()
        {
            if (tickWorld == null)
                tickWorld = new SimTickWorld(this);
            TickEngine.AdvanceAllUnits(tickWorld, NullTickCallbacks.Instance);
        }
    }
}
