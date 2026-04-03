using AgentSDK;

namespace Opponent.Tests
{
    /// <summary>
    /// [EASY] Does absolutely nothing. Free win — just build any army and attack.
    /// </summary>
    public class IdleOpponent : PlanningAgentBase
    {
        public override void InitializeMatch() { }
        public override void Update(IGameState state, IAgentActions actions) { }
    }
}
