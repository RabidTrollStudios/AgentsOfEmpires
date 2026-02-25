namespace AgentSDK
{
    /// <summary>
    /// Interface for issuing commands to your units.
    /// All commands are validated by the game engine - invalid commands are ignored with a log message.
    /// </summary>
    public interface IAgentActions
    {
        /// <summary>
        /// Move a unit to a target grid position.
        /// The unit must be able to move and the target must be walkable.
        /// </summary>
        /// <param name="unitNbr">The unit to move</param>
        /// <param name="target">The grid position to move to</param>
        void Move(int unitNbr, Position target);

        /// <summary>
        /// Send a worker to build a structure at a target position.
        /// Requires sufficient gold and all dependencies met.
        /// </summary>
        /// <param name="unitNbr">The worker unit that will build</param>
        /// <param name="target">Where to place the structure</param>
        /// <param name="unitType">Type of structure to build (BASE, BARRACKS, or REFINERY)</param>
        void Build(int unitNbr, Position target, UnitType unitType);

        /// <summary>
        /// Send a worker to gather gold from a mine and return it to a base.
        /// </summary>
        /// <param name="workerNbr">The worker unit</param>
        /// <param name="mineNbr">The gold mine to gather from</param>
        /// <param name="baseNbr">The base to return gold to</param>
        void Gather(int workerNbr, int mineNbr, int baseNbr);

        /// <summary>
        /// Train a new unit at a structure.
        /// Requires sufficient gold and the structure must be fully built.
        /// </summary>
        /// <param name="buildingNbr">The structure that will train (BASE or BARRACKS)</param>
        /// <param name="unitType">Type of unit to train (WORKER, SOLDIER, or ARCHER)</param>
        void Train(int buildingNbr, UnitType unitType);

        /// <summary>
        /// Command a combat unit to attack an enemy unit.
        /// Cannot attack your own units or mines.
        /// </summary>
        /// <param name="unitNbr">Your attacking unit</param>
        /// <param name="targetNbr">The enemy unit to attack</param>
        void Attack(int unitNbr, int targetNbr);

        /// <summary>
        /// Log a message to your agent's CSV output file.
        /// Useful for debugging and learning data.
        /// </summary>
        /// <param name="message">The message to log</param>
        void Log(string message);
    }
}
