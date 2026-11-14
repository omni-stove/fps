using System;
using System.Numerics;

namespace Fps.Core
{
    // 走って近づき、近くで合図 (信管) を始め、燃え尽きたら爆発させる。爆発そのものは物理を持つ Runtime (EnemyBomber) が行い、Detonate で知らせる。
    // 倒されたときに大小どちらの爆発にするかは、その時の Phase で決まる (Approaching なら小さく、Fusing なら本来の爆発)。
    public sealed class BomberEnemyBehavior : IEnemyBehavior
    {
        private BomberSpec _spec = BomberSpec.Default;
        private float _fuseElapsed;

        public event Action<BomberPhase, BomberPhase> PhaseChanged;

        public bool Enabled { get; set; } = true;

        public BomberPhase Phase { get; private set; } = BomberPhase.Approaching;

        public float FuseElapsed => Phase == BomberPhase.Approaching ? 0f : _fuseElapsed;

        public bool IsFuseBurnt => Phase == BomberPhase.Fusing && _fuseElapsed >= _spec.FuseSeconds;

        public Vector3? LastKnownTarget { get; private set; }

        public BomberSpec Spec => _spec;

        // 怯みや吹き飛ばしの間は判断 (TryOverride) が呼ばれないので、Runtime がここで信管だけを進める。火の付いた爆弾が止まらないように。
        public bool AdvanceFuse(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be non-negative and finite");
            }

            if (Phase != BomberPhase.Fusing)
            {
                return false;
            }

            _fuseElapsed += deltaSeconds;
            return IsFuseBurnt;
        }

        // 爆発させた結果が倒された時か信管かに関わらず、2 度目の爆発を起こさないために Exploded に移す。前の Phase を返す。
        public BomberPhase Detonate()
        {
            var previous = Phase;
            SetPhase(BomberPhase.Exploded);
            return previous;
        }

        public void Reset()
        {
            _fuseElapsed = 0f;
            LastKnownTarget = null;
            SetPhase(BomberPhase.Approaching);
        }

        public bool TryOverride(EnemyBrain brain, float deltaSeconds, EnemyPerception perception, EnemyDecision planned, out EnemyDecision decision)
        {
            if (brain == null)
            {
                throw new ArgumentNullException(nameof(brain));
            }

            decision = planned;
            if (!Enabled || brain.IsDead || brain.Spec.Archetype != EnemyArchetype.Bomber)
            {
                return false;
            }

            _spec = brain.Spec.Bomber;
            var target = perception.Target;
            var seen = target.HasValue
                && perception.HasLineOfSight
                && perception.Awareness >= EnemyPerception.FullAwareness
                && Vector3.Distance(perception.Self, target.Value) <= brain.Spec.SightRange;
            if (seen)
            {
                LastKnownTarget = target;
            }

            switch (Phase)
            {
                case BomberPhase.Exploded:
                    decision = new EnemyDecision(null, planned.LookAt, false, false);
                    return true;

                // 合図を始めたら、見失っても止めずに最後に見た所へ走り続ける。物陰へ隠れるだけで火が消えると、合図の意味が無くなる。
                case BomberPhase.Fusing:
                    _fuseElapsed += deltaSeconds;
                    var chase = seen ? target : LastKnownTarget;
                    decision = new EnemyDecision(chase, chase ?? planned.LookAt, false, false);
                    return true;
            }

            if (!seen)
            {
                decision = new EnemyDecision(planned.MoveTo, planned.LookAt, false, false);
                return true;
            }

            if (HorizontalDistance(perception.Self, target.Value) <= _spec.TriggerDistance)
            {
                _fuseElapsed = 0f;
                SetPhase(BomberPhase.Fusing);
            }

            decision = new EnemyDecision(target, target, false, false);
            return true;
        }

        private void SetPhase(BomberPhase next)
        {
            if (Phase == next)
            {
                return;
            }

            var previous = Phase;
            Phase = next;
            PhaseChanged?.Invoke(previous, next);
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            var dx = a.X - b.X;
            var dz = a.Z - b.Z;
            return (float)Math.Sqrt((dx * dx) + (dz * dz));
        }
    }
}
