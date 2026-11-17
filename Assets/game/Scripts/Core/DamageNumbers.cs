using System;
using System.Collections.Generic;
using System.Numerics;

namespace Shooter.Core
{
    public sealed class DamageNumberSettings
    {
        public const float DefaultLifetimeSeconds = 0.8f;
        public const float DefaultRiseDistance = 48f;
        public const float DefaultFadeFraction = 0.5f;
        public const int DefaultMaxCount = 24;

        public static readonly DamageNumberSettings Default = new DamageNumberSettings(
            DefaultLifetimeSeconds, DefaultRiseDistance, DefaultFadeFraction, DefaultMaxCount);

        public DamageNumberSettings(float lifetimeSeconds, float riseDistance, float fadeFraction, int maxCount)
        {
            if (!IsFinite(lifetimeSeconds) || lifetimeSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(lifetimeSeconds), lifetimeSeconds, "must be a finite number > 0");
            }

            if (!IsFinite(riseDistance) || riseDistance < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(riseDistance), riseDistance, "must be a finite number >= 0");
            }

            if (!IsFinite(fadeFraction) || fadeFraction < 0f || fadeFraction > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(fadeFraction), fadeFraction, "must be in [0, 1]");
            }

            if (maxCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxCount), maxCount, "must be at least 1");
            }

            LifetimeSeconds = lifetimeSeconds;
            RiseDistance = riseDistance;
            FadeFraction = fadeFraction;
            MaxCount = maxCount;
        }

        public float LifetimeSeconds { get; }

        public float RiseDistance { get; }

        public float FadeFraction { get; }

        public int MaxCount { get; }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    public readonly struct DamageNumber
    {
        public DamageNumber(int id, HitConfirm confirm, float ageSeconds)
        {
            Id = id;
            Confirm = confirm;
            AgeSeconds = ageSeconds;
        }

        public int Id { get; }

        public HitConfirm Confirm { get; }

        public int Damage => Confirm.Damage;

        public HitZone Zone => Confirm.Zone;

        public bool Killed => Confirm.Killed;

        public Vector3? Point => Confirm.Point;

        public float AgeSeconds { get; }
    }

    public sealed class DamageNumberFeed
    {
        private readonly List<DamageNumber> _numbers = new List<DamageNumber>();
        private int _nextId = 1;

        public DamageNumberFeed(DamageNumberSettings settings)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public DamageNumberSettings Settings { get; }

        public IReadOnlyList<DamageNumber> Numbers => _numbers;

        public int Add(HitConfirm confirm)
        {
            if (confirm.Damage <= 0)
            {
                return 0;
            }

            var id = _nextId++;
            _numbers.Add(new DamageNumber(id, confirm, 0f));
            while (_numbers.Count > Settings.MaxCount)
            {
                _numbers.RemoveAt(0);
            }

            return id;
        }

        public void Tick(float deltaSeconds)
        {
            if (!(deltaSeconds > 0f) || float.IsInfinity(deltaSeconds))
            {
                return;
            }

            for (var i = _numbers.Count - 1; i >= 0; i--)
            {
                var number = _numbers[i];
                var age = number.AgeSeconds + deltaSeconds;
                if (age >= Settings.LifetimeSeconds)
                {
                    _numbers.RemoveAt(i);
                }
                else
                {
                    _numbers[i] = new DamageNumber(number.Id, number.Confirm, age);
                }
            }
        }

        public void Clear()
        {
            _numbers.Clear();
        }

        public float Rise(DamageNumber number)
        {
            return Settings.RiseDistance * Progress(number);
        }

        public float Opacity(DamageNumber number)
        {
            var fade = Settings.FadeFraction;
            if (fade <= 0f)
            {
                return 1f;
            }

            var remaining = 1f - Progress(number);
            return remaining >= fade ? 1f : Math.Max(0f, remaining / fade);
        }

        private float Progress(DamageNumber number)
        {
            return Math.Min(1f, Math.Max(0f, number.AgeSeconds / Settings.LifetimeSeconds));
        }
    }
}
