using System;
using System.Numerics;

namespace Shooter.Core
{
    // 乗りに行くまでは判断の上書き (TryOverride) で、乗った後は Man で決める。乗っている間の敵は判断を進めない (EnemyAgent が座席に留める) ため。
    // どの銃座が空いているかはシーンにしか無いので、近くの空いた銃座の乗る位置は Runtime が SetCandidate で毎フレーム渡す。
    public sealed class MountedGunEnemyBehavior : IEnemyBehavior
    {
        private Vector3? _candidate;
        private float _unusableSeconds;

        public MountedGunEnemyBehavior()
            : this(MountedGunUseSpec.Default)
        {
        }

        public MountedGunEnemyBehavior(MountedGunUseSpec spec)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
        }

        public MountedGunUseSpec Spec { get; private set; }

        public bool Enabled { get; set; } = true;

        public bool IsSeeking { get; private set; }

        public Vector3? Candidate => _candidate;

        public float RemountRemainingSeconds { get; private set; }

        public float UnusableSeconds => _unusableSeconds;

        public void ApplySpec(MountedGunUseSpec spec)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
        }

        public void SetCandidate(Vector3? boardPoint)
        {
            _candidate = boardPoint;
        }

        public bool TryOverride(EnemyBrain brain, float deltaSeconds, EnemyPerception perception, EnemyDecision planned, out EnemyDecision decision)
        {
            if (brain == null)
            {
                throw new ArgumentNullException(nameof(brain));
            }

            SpecGuard.NonNegativeFinite(deltaSeconds, nameof(deltaSeconds));
            decision = planned;
            RemountRemainingSeconds = Math.Max(0f, RemountRemainingSeconds - deltaSeconds);
            IsSeeking = Enabled && !brain.IsDead && RemountRemainingSeconds <= 0f && IsEngaged(brain.State) && IsNear(perception.Self);
            if (!IsSeeking)
            {
                return false;
            }

            decision = new EnemyDecision(_candidate, planned.LookAt, planned.Fire, false);
            return true;
        }

        // 熱で撃てない間も引き金は引かせる。撃てるかは銃 (WeaponHeat) が決め、冷めたらすぐ撃ち始めるように。
        public MountedGunOrder Man(float deltaSeconds, bool targetVisibleFromGun, bool gunCanReachTarget, bool gunPointsAtTarget)
        {
            SpecGuard.NonNegativeFinite(deltaSeconds, nameof(deltaSeconds));
            IsSeeking = false;
            if (targetVisibleFromGun && gunCanReachTarget)
            {
                _unusableSeconds = 0f;
                return new MountedGunOrder(gunPointsAtTarget, null);
            }

            var cause = gunCanReachTarget ? MountedGunLeaveCause.TargetLost : MountedGunLeaveCause.OutOfArc;
            _unusableSeconds += deltaSeconds;
            return new MountedGunOrder(false, _unusableSeconds >= Spec.GiveUpSeconds ? cause : (MountedGunLeaveCause?)null);
        }

        public void NoteLeft()
        {
            _unusableSeconds = 0f;
            IsSeeking = false;
            RemountRemainingSeconds = Spec.RemountDelaySeconds;
        }

        public void Reset()
        {
            _candidate = null;
            _unusableSeconds = 0f;
            IsSeeking = false;
            RemountRemainingSeconds = 0f;
        }

        private static bool IsEngaged(EnemyState state)
        {
            return state == EnemyState.Chase || state == EnemyState.Attack || state == EnemyState.TakeCover;
        }

        private bool IsNear(Vector3 self)
        {
            if (!_candidate.HasValue || Spec.SeekRadius <= 0f)
            {
                return false;
            }

            var dx = _candidate.Value.X - self.X;
            var dz = _candidate.Value.Z - self.Z;
            return (dx * dx) + (dz * dz) <= Spec.SeekRadius * Spec.SeekRadius;
        }
    }
}
