using System;

namespace Shooter.Core
{
    // 試合の進行は文を作らず、何をどう出すかは UI が決めるので、再生の値だけを持つ。
    public readonly struct KillCamStatus : IEquatable<KillCamStatus>
    {
        // default では null になるので、読むときに空文字へ寄せる。
        private readonly string _killerName;
        private readonly string _weaponId;

        public KillCamStatus(string killerName, string weaponId, float durationSeconds, float elapsedSeconds, float speed)
        {
            if (!(durationSeconds >= 0f) || float.IsInfinity(durationSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(durationSeconds), durationSeconds, "durationSeconds must be finite and not negative");
            }

            if (float.IsNaN(elapsedSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds), elapsedSeconds, "elapsedSeconds must be a number");
            }

            if (!KillCamPlayback.IsValidSpeed(speed))
            {
                throw new ArgumentOutOfRangeException(nameof(speed), speed, "speed must be positive and finite");
            }

            _killerName = killerName ?? string.Empty;
            _weaponId = weaponId ?? string.Empty;
            DurationSeconds = durationSeconds;
            ElapsedSeconds = Math.Clamp(elapsedSeconds, 0f, durationSeconds);
            Speed = speed;
        }

        public string KillerName => _killerName ?? string.Empty;

        public string WeaponId => _weaponId ?? string.Empty;

        public float DurationSeconds { get; }

        public float ElapsedSeconds { get; }

        public float Speed { get; }

        public float Progress => DurationSeconds > 0f ? ElapsedSeconds / DurationSeconds : 1f;

        public float RemainingSeconds => Speed > 0f ? (DurationSeconds - ElapsedSeconds) / Speed : 0f;

        public KillCamStatus At(float elapsedSeconds)
        {
            return new KillCamStatus(KillerName, WeaponId, DurationSeconds, elapsedSeconds, Speed);
        }

        public bool Equals(KillCamStatus other)
        {
            return KillerName == other.KillerName
                && WeaponId == other.WeaponId
                && DurationSeconds.Equals(other.DurationSeconds)
                && ElapsedSeconds.Equals(other.ElapsedSeconds)
                && Speed.Equals(other.Speed);
        }

        public static bool operator ==(KillCamStatus left, KillCamStatus right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(KillCamStatus left, KillCamStatus right)
        {
            return !left.Equals(right);
        }

        public override bool Equals(object obj)
        {
            return obj is KillCamStatus other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(KillerName, WeaponId, DurationSeconds, ElapsedSeconds, Speed);
        }

        public override string ToString()
        {
            return "killer=" + KillerName + " weapon=" + WeaponId
                + " " + ElapsedSeconds.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
                + "/" + DurationSeconds.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
                + "s at " + Speed.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "x";
        }
    }
}
