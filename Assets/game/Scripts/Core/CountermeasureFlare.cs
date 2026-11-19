using System.Numerics;

namespace Shooter.Core
{
    public readonly struct CountermeasureFlare
    {
        public CountermeasureFlare(int id, Vector3 position, Vector3 velocity, float age)
        {
            Id = id;
            Position = position;
            Velocity = velocity;
            Age = age;
        }

        public int Id { get; }

        public Vector3 Position { get; }

        public Vector3 Velocity { get; }

        public float Age { get; }
    }
}
