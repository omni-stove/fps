using System;
using System.Globalization;

namespace Shooter.Core
{
    /// <summary>武器またはアタッチメントの Id と、それが使えるようになるレベル。</summary>
    public sealed class ProgressionUnlock
    {
        /// <param name="id">武器の Id かアタッチメントの持ち物の Id。空白だけにはできない。</param>
        /// <param name="level">使えるようになるレベル。1 以上。</param>
        /// <exception cref="ArgumentException">id が null・空・空白だけ。</exception>
        /// <exception cref="ArgumentOutOfRangeException">level が 1 未満。</exception>
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
