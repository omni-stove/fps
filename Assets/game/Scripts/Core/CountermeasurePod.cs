using System;
using System.Collections.Generic;
using System.Numerics;

namespace Shooter.Core
{
    public sealed class CountermeasurePod : ILockOnDecoy
    {
        private const float _minLength = 1e-6f;

        private readonly List<CountermeasureFlare> _flares = new List<CountermeasureFlare>();
        private int _nextId;

        public CountermeasurePod(CountermeasureSpec spec)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
            Remaining = spec.Count;
        }

        public CountermeasureSpec Spec { get; }

        public int Remaining { get; private set; }

        public float CooldownLeft { get; private set; }

        public IReadOnlyList<CountermeasureFlare> Flares => _flares;

        public CountermeasureVerdict Readiness => Remaining <= 0
            ? CountermeasureVerdict.Empty
            : CooldownLeft > 0f ? CountermeasureVerdict.CoolingDown : CountermeasureVerdict.Deployed;

        public CountermeasureVerdict TryDeploy(Vector3 origin, Vector3 velocity, out int flareId)
        {
            RequireFinite(origin, nameof(origin));
            RequireFinite(velocity, nameof(velocity));
            var verdict = Readiness;
            if (verdict != CountermeasureVerdict.Deployed)
            {
                flareId = -1;
                return verdict;
            }

            flareId = _nextId++;
            Remaining--;
            CooldownLeft = Spec.IntervalSeconds;
            _flares.Add(new CountermeasureFlare(flareId, origin, velocity, 0f));
            return verdict;
        }

        public void Tick(float deltaSeconds, Vector3 gravity)
        {
            SpecGuard.NonNegativeFinite(deltaSeconds, nameof(deltaSeconds));
            RequireFinite(gravity, nameof(gravity));
            CooldownLeft = Math.Max(0f, CooldownLeft - deltaSeconds);
            var acceleration = gravity * Spec.GravityMultiplier;
            for (var i = _flares.Count - 1; i >= 0; i--)
            {
                var flare = _flares[i];
                var age = flare.Age + deltaSeconds;
                if (age >= Spec.LifetimeSeconds)
                {
                    _flares.RemoveAt(i);
                    continue;
                }

                var position = flare.Position + (flare.Velocity * deltaSeconds) + (acceleration * (0.5f * deltaSeconds * deltaSeconds));
                _flares[i] = new CountermeasureFlare(flare.Id, position, flare.Velocity + (acceleration * deltaSeconds), age);
            }
        }

        // 撒いた後のフレアは消さないのは、補給しても空にいるフレアの効き目が切れないようにするため。
        public void Refill()
        {
            Remaining = Spec.Count;
        }

        public bool TryLure(Vector3 seeker, Vector3 target, out int decoyId, out Vector3 decoyPoint)
        {
            decoyId = -1;
            decoyPoint = default;
            var toTarget = target - seeker;
            if (toTarget.LengthSquared() < _minLength * _minLength)
            {
                return false;
            }

            var unit = Vector3.Normalize(toTarget);
            var best = float.MaxValue;
            for (var i = 0; i < _flares.Count; i++)
            {
                var flare = _flares[i];
                var angle = LockOnTracker.AngleTo(seeker, unit, flare.Position);

                // 同じ角度なら後に撒いた方を選ぶのは、古いフレアほど先に燃え尽きるため。
                if (angle > Spec.DecoyConeDegrees || angle > best)
                {
                    continue;
                }

                best = angle;
                decoyId = flare.Id;
                decoyPoint = flare.Position;
            }

            return decoyId >= 0;
        }

        public bool TryLocate(int decoyId, out Vector3 decoyPoint)
        {
            for (var i = 0; i < _flares.Count; i++)
            {
                if (_flares[i].Id == decoyId)
                {
                    decoyPoint = _flares[i].Position;
                    return true;
                }
            }

            decoyPoint = default;
            return false;
        }

        private static void RequireFinite(Vector3 value, string paramName)
        {
            if (float.IsNaN(value.X) || float.IsInfinity(value.X)
                || float.IsNaN(value.Y) || float.IsInfinity(value.Y)
                || float.IsNaN(value.Z) || float.IsInfinity(value.Z))
            {
                throw new ArgumentOutOfRangeException(paramName, value, paramName + " must be finite");
            }
        }
    }
}
