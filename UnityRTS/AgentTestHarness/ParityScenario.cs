using System;
using AgentSDK;

namespace AgentTestHarness
{
    /// <summary>
    /// Named scenario descriptor for parity testing.
    /// Encapsulates everything needed to run a record-replay determinism test.
    /// </summary>
    public class ParityScenario
    {
        public string Name { get; }
        public Func<SimGameBuilder> BuilderFactory { get; }
        public Func<IPlanningAgent> Agent0Factory { get; }
        public Func<IPlanningAgent> Agent1Factory { get; }
        public int Ticks { get; }

        /// <summary>
        /// Create a scenario with an explicit tick count.
        /// </summary>
        public ParityScenario(string name,
            Func<SimGameBuilder> builderFactory,
            Func<IPlanningAgent> agent0Factory,
            Func<IPlanningAgent> agent1Factory,
            int ticks)
        {
            Name = name;
            BuilderFactory = builderFactory;
            Agent0Factory = agent0Factory;
            Agent1Factory = agent1Factory;
            Ticks = ticks;
        }

        /// <summary>
        /// Create a scenario with a duration in seconds.
        /// Tick count is computed from the default SimConfig step rate (60 Hz).
        /// </summary>
        public static ParityScenario FromDuration(string name,
            Func<SimGameBuilder> builderFactory,
            Func<IPlanningAgent> agent0Factory,
            Func<IPlanningAgent> agent1Factory,
            float durationSeconds)
        {
            float tickDuration = new SimConfig().TickDuration;
            int ticks = (int)Math.Ceiling(durationSeconds / tickDuration);
            return new ParityScenario(name, builderFactory, agent0Factory, agent1Factory, ticks);
        }

        public override string ToString() => Name;
    }
}
