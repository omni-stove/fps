using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Fps.Runtime
{
    // 固めるまでの時間を DeathBodyPresenter ではなく抜け殻の側で数えるのは、倒れた体を非アクティブにする購読者
    // (KillTargetObjective) がいると体の Update が来なくなり、固めないまま寿命まで転がり続けるため。
    [DisallowMultipleComponent]
    public sealed class DeathRagdoll : MonoBehaviour
    {
        public const string LogTag = DeathBodyPresenter.LogTag;

        // 足元も上半身と同じ速さで押すと、倒れずに滑っていく。
        public const float FootSpeedRatio = 0.3f;

        private readonly List<Rigidbody> _bodies = new List<Rigidbody>();
        private string _id = string.Empty;
        private float _settleSeconds;
        private float _elapsed;
        private bool _frozen;

        public IReadOnlyList<Rigidbody> Bodies => _bodies;

        public float SettleSeconds => _settleSeconds;

        public float ElapsedSeconds => _elapsed;

        public bool IsFrozen => _frozen;

        public void Initialize(string id, IEnumerable<Rigidbody> bodies, float settleSeconds)
        {
            if (bodies == null)
            {
                throw new ArgumentNullException(nameof(bodies));
            }

            if (!(settleSeconds > 0f) || float.IsInfinity(settleSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(settleSeconds), settleSeconds, "settleSeconds must be positive and finite");
            }

            _bodies.Clear();
            foreach (var body in bodies)
            {
                if (body != null)
                {
                    _bodies.Add(body);
                }
            }

            _id = id ?? string.Empty;
            _settleSeconds = settleSeconds;
            _elapsed = 0f;
            _frozen = false;
        }

        public bool Push(Vector3 direction, float speed)
        {
            if (!(speed >= 0f) || float.IsInfinity(speed))
            {
                throw new ArgumentOutOfRangeException(nameof(speed), speed, "speed must be a non-negative finite number");
            }

            if (_frozen)
            {
                return false;
            }

            var lowest = float.PositiveInfinity;
            var highest = float.NegativeInfinity;
            foreach (var body in _bodies)
            {
                if (body == null)
                {
                    continue;
                }

                var y = body.worldCenterOfMass.y;
                lowest = Mathf.Min(lowest, y);
                highest = Mathf.Max(highest, y);
            }

            var range = highest - lowest;
            foreach (var body in _bodies)
            {
                if (body == null || body.isKinematic)
                {
                    continue;
                }

                var height = range > 1e-4f ? (body.worldCenterOfMass.y - lowest) / range : 1f;
                body.linearVelocity = direction * (speed * Mathf.Lerp(FootSpeedRatio, 1f, height));
                body.angularVelocity = Vector3.zero;
            }

            return true;
        }

        public bool Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            if (_frozen)
            {
                return false;
            }

            _elapsed += deltaSeconds;
            if (_elapsed < _settleSeconds)
            {
                return false;
            }

            Freeze();
            return true;
        }

        public void Freeze()
        {
            if (_frozen)
            {
                return;
            }

            _frozen = true;
            var frozen = 0;
            foreach (var body in _bodies)
            {
                if (body == null)
                {
                    continue;
                }

                // kinematic にしてから速度を書くと Unity が警告を出すので、先に止める。
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                    body.isKinematic = true;
                }

                frozen++;
            }

            Debug.Log(
                $"{LogTag} ragdoll frozen id={_id} bones={frozen.ToString(CultureInfo.InvariantCulture)} after={_elapsed.ToString("0.##", CultureInfo.InvariantCulture)}s",
                this);
        }

        private void Update()
        {
            if (_settleSeconds > 0f)
            {
                Tick(Time.deltaTime);
            }
        }
    }
}
