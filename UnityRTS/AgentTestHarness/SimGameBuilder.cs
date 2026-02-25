using System.Collections.Generic;
using AgentSDK;

namespace AgentTestHarness
{
    /// <summary>
    /// An agent that does nothing. Useful as an opponent in single-agent tests.
    /// </summary>
    public class DoNothingAgent : IPlanningAgent
    {
        public void InitializeMatch() { }
        public void InitializeRound(IGameState state) { }
        public void Update(IGameState state, IAgentActions actions) { }
        public void Learn(IGameState state) { }
    }

    /// <summary>
    /// Fluent builder for constructing SimGame test scenarios.
    ///
    /// Usage:
    ///   var game = new SimGameBuilder()
    ///       .WithMapSize(30, 30)
    ///       .WithGold(0, 5000)
    ///       .WithUnit(0, UnitType.BASE, new Position(5, 5), isBuilt: true)
    ///       .WithMine(new Position(15, 15))
    ///       .WithAgent(0, myAgent)
    ///       .WithAgent(1, new DoNothingAgent())
    ///       .Build();
    /// </summary>
    public class SimGameBuilder
    {
        private SimConfig config = new SimConfig();
        private readonly Dictionary<int, IPlanningAgent> agents = new Dictionary<int, IPlanningAgent>();
        private readonly List<UnitSpec> unitSpecs = new List<UnitSpec>();
        private readonly List<(Position from, Position to)> walls = new List<(Position, Position)>();
        private int? gold0;
        private int? gold1;

        private struct UnitSpec
        {
            public int OwnerAgentNbr;
            public UnitType UnitType;
            public Position Position;
            public float Health;
            public bool IsBuilt;
        }

        /// <summary>Set the map dimensions.</summary>
        public SimGameBuilder WithMapSize(int width, int height)
        {
            config.MapWidth = width;
            config.MapHeight = height;
            return this;
        }

        /// <summary>Override the full SimConfig.</summary>
        public SimGameBuilder WithConfig(SimConfig cfg)
        {
            config = cfg;
            return this;
        }

        /// <summary>Set starting gold for an agent.</summary>
        public SimGameBuilder WithGold(int agentNbr, int amount)
        {
            if (agentNbr == 0) gold0 = amount;
            else gold1 = amount;
            return this;
        }

        /// <summary>Place a unit owned by an agent.</summary>
        public SimGameBuilder WithUnit(int agentNbr, UnitType unitType, Position position, bool isBuilt = true)
        {
            float health = GameConstants.HEALTH[unitType];
            unitSpecs.Add(new UnitSpec
            {
                OwnerAgentNbr = agentNbr,
                UnitType = unitType,
                Position = position,
                Health = health,
                IsBuilt = isBuilt
            });
            return this;
        }

        /// <summary>Place a neutral gold mine. Owner is set to -1 (neutral).</summary>
        public SimGameBuilder WithMine(Position position, int health = -1)
        {
            unitSpecs.Add(new UnitSpec
            {
                OwnerAgentNbr = -1,
                UnitType = UnitType.MINE,
                Position = position,
                Health = health < 0 ? config.StartingMineGold : health,
                IsBuilt = true
            });
            return this;
        }

        /// <summary>
        /// Mark a rectangular region of cells as unwalkable/unbuildable.
        /// Useful for creating terrain obstacles.
        /// </summary>
        public SimGameBuilder WithWall(Position from, Position to)
        {
            walls.Add((from, to));
            return this;
        }

        /// <summary>Set the agent for a player slot (0 or 1).</summary>
        public SimGameBuilder WithAgent(int agentNbr, IPlanningAgent agent)
        {
            agents[agentNbr] = agent;
            return this;
        }

        /// <summary>Build the SimGame with all configured state.</summary>
        public SimGame Build()
        {
            var map = new SimMap(config.MapWidth, config.MapHeight);
            var game = new SimGame(config, map);

            // Override gold if specified
            if (gold0.HasValue) game.Gold[0] = gold0.Value;
            if (gold1.HasValue) game.Gold[1] = gold1.Value;

            // Place walls
            foreach (var (from, to) in walls)
            {
                int minX = from.X < to.X ? from.X : to.X;
                int maxX = from.X > to.X ? from.X : to.X;
                int minY = from.Y < to.Y ? from.Y : to.Y;
                int maxY = from.Y > to.Y ? from.Y : to.Y;

                for (int x = minX; x <= maxX; x++)
                    for (int y = minY; y <= maxY; y++)
                        map.SetCellBlocked(new Position(x, y));
            }

            // Place units
            foreach (var spec in unitSpecs)
            {
                game.PlaceUnit(spec.OwnerAgentNbr, spec.UnitType, spec.Position, spec.Health, spec.IsBuilt);
            }

            // Set agents (default to DoNothingAgent if not specified)
            game.SetAgent(0, agents.ContainsKey(0) ? agents[0] : new DoNothingAgent());
            game.SetAgent(1, agents.ContainsKey(1) ? agents[1] : new DoNothingAgent());

            return game;
        }
    }
}
