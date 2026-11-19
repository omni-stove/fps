using System;
using System.Globalization;

namespace Shooter.Core
{
    public sealed class ProgressionUnlock
    {
        public ProgressionUnlock(string id, int level)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("id must not be empty", nameof(id));
            }

            if (level < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(level), level, "level must be 1 or more");
            }

            Id = id;
            Level = level;
        }

        public string Id { get; }

        public int Level { get; }

        public override string ToString()
        {
            return Id + "@" + Level.ToString(CultureInfo.InvariantCulture);
        }
    }
}
