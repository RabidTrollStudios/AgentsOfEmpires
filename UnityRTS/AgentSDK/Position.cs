using System;

namespace AgentSDK
{
    /// <summary>
    /// A 2D grid position. Replaces Unity's Vector3Int for agent code.
    /// </summary>
    public readonly struct Position : IEquatable<Position>
    {
        /// <summary>X coordinate on the grid</summary>
        public int X { get; }

        /// <summary>Y coordinate on the grid</summary>
        public int Y { get; }

        /// <summary>Create a new position at (<paramref name="x"/>, <paramref name="y"/>)</summary>
        public Position(int x, int y)
        {
            X = x;
            Y = y;
        }

        /// <summary>Position (0, 0)</summary>
        public static Position Zero => new Position(0, 0);

        /// <summary>
        /// Euclidean distance between two positions
        /// </summary>
        public static float Distance(Position a, Position b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// Center cell of a unit's footprint given its top-left corner and size.
        /// For 1x1 units returns the same position; for 3x3 returns topLeft+(1,-1).
        /// </summary>
        public static Position Center(Position topLeft, Position size) =>
            new Position(topLeft.X + (size.X - 1) / 2,
                         topLeft.Y - (size.Y - 1) / 2);

        /// <summary>Add two positions component-wise</summary>
        public static Position operator +(Position a, Position b) => new Position(a.X + b.X, a.Y + b.Y);
        /// <summary>Subtract two positions component-wise</summary>
        public static Position operator -(Position a, Position b) => new Position(a.X - b.X, a.Y - b.Y);
        /// <summary>Check if two positions are equal</summary>
        public static bool operator ==(Position a, Position b) => a.X == b.X && a.Y == b.Y;
        /// <summary>Check if two positions are not equal</summary>
        public static bool operator !=(Position a, Position b) => !(a == b);

        /// <inheritdoc/>
        public bool Equals(Position other) => X == other.X && Y == other.Y;
        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is Position p && Equals(p);
        /// <inheritdoc/>
        public override int GetHashCode() => X * 397 ^ Y;
        /// <inheritdoc/>
        public override string ToString() => $"({X}, {Y})";
    }
}
