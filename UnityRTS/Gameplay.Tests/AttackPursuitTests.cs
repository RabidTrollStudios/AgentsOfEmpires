using System.Linq;
using AgentSDK;
using AgentTestHarness;
using Xunit;

namespace Gameplay.Tests
{
    /// <summary>
    /// Tests for attack pursuit behavior (U3):
    /// - The engine never RETARGETS: a unit pursues only the target its agent assigned.
    /// - When that target is unreachable, the unit goes IDLE (pursuit failed) so the
    ///   agent can decide what to do next — it does not stay stuck or auto-switch enemies.
    /// - Following a moving target still works (repath toward its current position).
    /// </summary>
    public class AttackPursuitTests
    {
        /// <summary>Attacks a specific enemy unit number once, then stops re-issuing.</summary>
        private sealed class AttackSpecificOnceAgent : IPlanningAgent
        {
            private readonly System.Func<IGameState, int?> pickTarget;
            private bool issued;

            public AttackSpecificOnceAgent(System.Func<IGameState, int?> pickTarget)
            {
                this.pickTarget = pickTarget;
            }

            public void InitializeMatch() { issued = false; }
            public void InitializeRound(IGameState state) { }
            public void Learn(IGameState state) { }

            public void Update(IGameState state, IAgentActions actions)
            {
                if (issued) return;
                var warriors = state.GetMyUnits(UnitType.WARRIOR);
                var target = pickTarget(state);
                if (warriors.Count > 0 && target.HasValue)
                {
                    actions.Attack(warriors[0], target.Value);
                    issued = true;
                }
            }
        }

        [Fact]
        public void UnreachableTarget_UnitGoesIdle_DoesNotStayStuck()
        {
            // Enemy pawn at (20,20) ringed by terrain (its own cell left open) so the
            // warrior can never path to a neighbor of it.
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithUnit(0, UnitType.WARRIOR, new Position(5, 5))
                .WithUnit(1, UnitType.PAWN, new Position(20, 20))
                // Four wall segments forming a ring around (20,20), center left open:
                .WithWall(new Position(19, 19), new Position(21, 19)) // bottom row
                .WithWall(new Position(19, 21), new Position(21, 21)) // top row
                .WithWall(new Position(19, 20), new Position(19, 20)) // left
                .WithWall(new Position(21, 20), new Position(21, 20)) // right
                .WithAgent(0, new AttackSpecificOnceAgent(s =>
                {
                    var e = s.GetEnemyUnits(UnitType.PAWN);
                    return e.Count > 0 ? e[0] : (int?)null;
                }))
                .WithAgent(1, new DoNothingAgent())
                .Build();

            game.InitializeMatch();
            game.InitializeRound();

            var warrior = game.GetUnitsByType(0, UnitType.WARRIOR).Single();

            // Run enough ticks for the command to be issued and the pursuit to resolve.
            for (int i = 0; i < 60 && warrior.CurrentAction != UnitAction.IDLE; i++)
                game.Tick();

            Assert.Equal(UnitAction.IDLE, warrior.CurrentAction);
        }

        [Fact]
        public void Engine_DoesNotRetarget_ToCloserEnemy()
        {
            // Warrior is told to attack the FAR enemy; a CLOSER enemy sits next to it.
            // The engine must keep pursuing the assigned (far) target — no auto-switch.
            var farEnemyPos = new Position(25, 5);
            var game = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithUnit(0, UnitType.WARRIOR, new Position(5, 5))
                .WithUnit(1, UnitType.WARRIOR, farEnemyPos)          // far — assigned target
                .WithUnit(1, UnitType.PAWN, new Position(7, 5))      // close — must be ignored
                .WithAgent(0, new AttackSpecificOnceAgent(s =>
                {
                    // Pick the far WARRIOR specifically.
                    foreach (int n in s.GetEnemyUnits(UnitType.WARRIOR))
                    {
                        var info = s.GetUnit(n);
                        if (info.HasValue && info.Value.GridPosition == farEnemyPos)
                            return n;
                    }
                    return null;
                }))
                .WithAgent(1, new DoNothingAgent())
                .Build();

            game.InitializeMatch();
            game.InitializeRound();

            // Record the assigned target once the command lands.
            int assignedTarget = -1;
            var warrior = game.GetUnitsByType(0, UnitType.WARRIOR).Single();
            for (int i = 0; i < 5; i++)
            {
                game.Tick();
                if (warrior.CurrentAction == UnitAction.ATTACK)
                {
                    assignedTarget = warrior.AttackTargetNbr;
                    break;
                }
            }
            Assert.True(assignedTarget >= 0, "Warrior should have started attacking the far target");

            var farWarrior = game.GetUnitsByType(1, UnitType.WARRIOR).Single();
            Assert.Equal(farWarrior.UnitNbr, assignedTarget);

            // Advance while it walks past the close pawn — it must NOT switch to it.
            for (int i = 0; i < 40 && warrior.CurrentAction == UnitAction.ATTACK; i++)
            {
                game.Tick();
                if (warrior.CurrentAction == UnitAction.ATTACK)
                    Assert.Equal(assignedTarget, warrior.AttackTargetNbr);
            }
        }
    }
}
