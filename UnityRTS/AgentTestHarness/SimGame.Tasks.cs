using AgentSDK;

namespace AgentTestHarness
{
    /// <summary>
    /// Task advancement and movement. Delegates to the shared SimulationRunner
    /// which orchestrates StepEngine + MovementSystem in deterministic order.
    /// </summary>
    public partial class SimGame
    {
        /// <summary>Lazy-initialized world adapter for StepEngine.</summary>
        private SimWorld stepWorld;

        /// <summary>
        /// Get the shared ISimWorld adapter for direct CommandProcessor calls.
        /// Used by cross-engine parity tests to issue commands identically to both engines.
        /// </summary>
        public ISimWorld GetSimWorld()
        {
            if (stepWorld == null)
                stepWorld = new SimWorld(this);
            return stepWorld;
        }

        private void AdvanceAllUnits()
        {
            if (stepWorld == null)
                stepWorld = new SimWorld(this);

            SimulationRunner.AdvanceStep(stepWorld, NullSimCallbacks.Instance);
        }
    }
}
