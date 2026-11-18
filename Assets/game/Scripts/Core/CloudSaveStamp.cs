using System;
using System.Globalization;

namespace Shooter.Core
{
    // SavedAt は UTC の Unix 時刻 (ミリ秒)。端末をまたいで比べるので、端末ごとの時刻帯に左右されない値にする。
    public readonly struct CloudSaveStamp : IEquatable<CloudSaveStamp>
    {
        public CloudSaveStamp(long savedAt, bool deleted)
        {
            if (savedAt < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(savedAt), savedAt, "savedAt must not be negative");
            }

            SavedAt = savedAt;
            Deleted = deleted;
        }

        public long SavedAt { get; }

        public bool Deleted { get; }

        public bool Equals(CloudSaveStamp other)
        {
            return SavedAt == other.SavedAt && Deleted == other.Deleted;
        }

        public override bool Equals(object obj)
        {
            return obj is CloudSaveStamp other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (SavedAt.GetHashCode() * 397) ^ (Deleted ? 1 : 0);
        }

        public static bool operator ==(CloudSaveStamp left, CloudSaveStamp right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CloudSaveStamp left, CloudSaveStamp right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return "savedAt=" + SavedAt.ToString(CultureInfo.InvariantCulture) + (Deleted ? " deleted" : string.Empty);
        }
    }
}
