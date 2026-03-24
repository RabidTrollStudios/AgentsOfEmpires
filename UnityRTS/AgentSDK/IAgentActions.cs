namespace AgentSDK
{
    /// <summary>
    /// Interface for issuing commands to your units.
    /// All commands are validated by the game engine. Each method returns a
    /// <see cref="CommandResult"/> indicating success or the specific reason for failure.
    /// </summary>
    public interface IAgentActions
    {
        /// <summary>
        /// Move a unit to a target grid position.
        /// The unit must be able to move and the target must be walkable.
        /// </summary>
        /// <param name="unitNbr">The unit to move</param>
        /// <param name="target">The grid position to move to</param>
        /// <returns>Success if the command was dispatched, or a failure code.</returns>
        CommandResult Move(int unitNbr, Position target);

        /// <summary>
        /// Send a pawn to build a structure at a target position.
        /// Requires sufficient gold and all dependencies met.
        /// </summary>
        /// <param name="unitNbr">The pawn unit that will build</param>
        /// <param name="target">Where to place the structure</param>
        /// <param name="unitType">Type of structure to build (BASE, BARRACKS, ARCHERY, or TOWER)</param>
        /// <returns>Success if the command was dispatched, or a failure code.</returns>
        CommandResult Build(int unitNbr, Position target, UnitType unitType);

        /// <summary>
        /// Send a pawn to gather gold from a mine and return it to a base.
        /// </summary>
        /// <param name="pawnNbr">The pawn unit</param>
        /// <param name="mineNbr">The gold mine to gather from</param>
        /// <param name="baseNbr">The base to return gold to</param>
        /// <returns>Success if the command was dispatched, or a failure code.</returns>
        CommandResult Gather(int pawnNbr, int mineNbr, int baseNbr);

        /// <summary>
        /// Train a new unit at a structure.
        /// Requires sufficient gold and the structure must be fully built.
        /// </summary>
        /// <param name="buildingNbr">The structure that will train (BASE, BARRACKS, ARCHERY, or TOWER)</param>
        /// <param name="unitType">Type of unit to train (PAWN, WARRIOR, ARCHER, or LANCER)</param>
        /// <returns>Success if the command was dispatched, or a failure code.</returns>
        CommandResult Train(int buildingNbr, UnitType unitType);

        /// <summary>
        /// Command a combat unit to attack an enemy unit.
        /// Cannot attack your own units or mines.
        /// </summary>
        /// <param name="unitNbr">Your attacking unit</param>
        /// <param name="targetNbr">The enemy unit to attack</param>
        /// <returns>Success if the command was dispatched, or a failure code.</returns>
        CommandResult Attack(int unitNbr, int targetNbr);

        /// <summary>
        /// Send a pawn to repair a damaged friendly building.
        /// The building must belong to the same agent and have less than full health.
        /// </summary>
        /// <param name="pawnNbr">The pawn unit that will repair</param>
        /// <param name="buildingNbr">The building to repair</param>
        /// <returns>Success if the command was dispatched, or a failure code.</returns>
        CommandResult Repair(int pawnNbr, int buildingNbr);

        /// <summary>
        /// Command a monk to heal a friendly unit.
        /// The target must be a friendly mobile unit at or below 80% health.
        /// The monk must have sufficient mana.
        /// </summary>
        /// <param name="monkNbr">Your monk unit</param>
        /// <param name="targetNbr">The friendly unit to heal</param>
        /// <returns>Success if the command was dispatched, or a failure code.</returns>
        CommandResult Heal(int monkNbr, int targetNbr);

        /// <summary>
        /// Log a message to your agent's CSV output file.
        /// Useful for debugging and learning data.
        /// </summary>
        /// <param name="message">The message to log</param>
        void Log(string message);
    }
}
