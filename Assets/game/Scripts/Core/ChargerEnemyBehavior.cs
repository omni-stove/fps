using System;
using System.Numerics;

namespace Fps.Core
{
    // 構え → 突進 → (当たれば) 立て直し / (外れれば) よろめき、を回す。
    // 体を動かすことと、相手や壁に当たったかは物理を持つ Runtime (EnemyCharger) が見て EndCharge で知らせる。
    // 突進の時間はここで進め、届く距離を走り切ったら当たらなかったことにする。Runtime が動かせない間も突進が終わらないままにならないように。
    public sealed class ChargerEnemyBehavior : IEnemyBehavior
    {
        // 突進中に向く先を、進む向きのこれだけ先に置く。近すぎると向き直りが揺れる。
        private const float _lookAheadDistance = 10f;

        private ChargerSpec _spec = ChargerSpec.Default;
        private float _phaseElapsed;

        public event Action<ChargerPhase, ChargerPhase> PhaseChanged;

        public bool Enabled { get; set; } = true;

        public ChargerPhase Phase { get; private set; } = ChargerPhase.Ready;

        public float PhaseElapsed => _phaseElapsed;

        public Vector3? WindupTarget { get; private set; }

        public Vector3? ChargeOrigin { get; private set; }

        public Vector3? ChargeDirection { get; private set; }

        public ChargeEnd LastEnd { get; private set; } = ChargeEnd.None;

        public ChargerSpec Spec => _spec;

        public bool IsCharging => Phase == ChargerPhase.Charging;

        // Hit は立て直し、それ以外の終わり方 (外れ) はよろめきにする。Cancelled は Cancel を使う。
        public bool EndCharge(ChargeEnd end)
        {
            if (end == ChargeEnd.None || end == ChargeEnd.Cancelled || !Enum.IsDefined(typeof(ChargeEnd), end))
            {
                throw new ArgumentOutOfRangeException(nameof(end), end, "end must be how a charge finishes, not None or Cancelled");
            }

            if (Phase != ChargerPhase.Charging)
            {
                return false;
            }

            LastEnd = end;
            _phaseElapsed = 0f;
            SetPhase(end == ChargeEnd.Hit ? ChargerPhase.Recovering : ChargerPhase.Stunned);
            return true;
        }

        // 怯みや吹き飛ばしで体を持っていかれたときに、構えや突進を初めからにする。
        public bool Cancel()
        {
            if (Phase != ChargerPhase.WindingUp && Phase != ChargerPhase.Charging)
            {
                return false;
            }

            LastEnd = ChargeEnd.Cancelled;
            BecomeReady();
            return true;
        }

        public void Reset()
        {
            LastEnd = ChargeEnd.None;
            BecomeReady();
        }

        public bool TryOverride(EnemyBrain brain, float deltaSeconds, EnemyPerception perception, EnemyDecision planned, out EnemyDecision decision)
        {
            if (brain == null)
            {
                throw new ArgumentNullException(nameof(brain));
            }

            decision = planned;
            if (!Enabled || brain.IsDead || brain.Spec.Archetype != EnemyArchetype.Charger)
            {
                if (Phase != ChargerPhase.Ready)
                {
                    Reset();
                }

                return false;
            }

            _spec = brain.Spec.Charger;
            var target = perception.Target;
            var seen = target.HasValue
                && perception.HasLineOfSight
                && perception.Awareness >= EnemyPerception.FullAwareness
                && Vector3.Distance(perception.Self, target.Value) <= brain.Spec.SightRange;

            switch (Phase)
            {
                case ChargerPhase.WindingUp:
                    // 構えの間に隠れられたら突っ込まない。見えない相手へまっすぐ走ると、物陰の壁へ当たりに行くだけになる。
                    if (!seen)
                    {
                        LastEnd = ChargeEnd.Cancelled;
                        BecomeReady();
                        decision = new EnemyDecision(null, planned.LookAt, false, false);
                        return true;
                    }

                    WindupTarget = target;
                    _phaseElapsed += deltaSeconds;
                    if (_phaseElapsed >= _spec.WindupSeconds && TryAim(perception.Self, target.Value, out var heading))
                    {
                        ChargeOrigin = perception.Self;
                        ChargeDirection = heading;
                        _phaseElapsed = 0f;
                        LastEnd = ChargeEnd.None;
                        SetPhase(ChargerPhase.Charging);
                        decision = new EnemyDecision(null, Ahead(perception.Self), false, false);
                        return true;
                    }

                    decision = new EnemyDecision(null, target, false, false);
                    return true;

                case ChargerPhase.Charging:
                    _phaseElapsed += deltaSeconds;
                    if (_phaseElapsed >= _spec.ChargeSeconds)
                    {
                        EndCharge(ChargeEnd.Spent);
                    }

                    decision = new EnemyDecision(null, Ahead(perception.Self), false, false);
                    return true;

                case ChargerPhase.Stunned:
                case ChargerPhase.Recovering:
                    _phaseElapsed += deltaSeconds;
                    var wait = Phase == ChargerPhase.Stunned ? _spec.StunSeconds : _spec.RecoverSeconds;
                    var look = Phase == ChargerPhase.Stunned ? Ahead(perception.Self) : (seen ? target : planned.LookAt);
                    if (_phaseElapsed >= wait)
                    {
                        BecomeReady();
                    }

                    decision = new EnemyDecision(null, look, false, false);
                    return true;
            }

            // 銃は撃たず、隠れたりしゃがんだりもしない。体ごと当たるのが唯一の攻撃なので。
            if (!seen)
            {
                decision = new EnemyDecision(planned.MoveTo, planned.LookAt, false, false);
                return true;
            }

            if (HorizontalDistance(perception.Self, target.Value) <= _spec.TriggerDistance)
            {
                _phaseElapsed = 0f;
                WindupTarget = target;
                SetPhase(ChargerPhase.WindingUp);
                decision = new EnemyDecision(null, target, false, false);
                return true;
            }

            decision = new EnemyDecision(target, target, false, false);
            return true;
        }

        private Vector3? Ahead(Vector3 self)
        {
            return ChargeDirection.HasValue ? self + (ChargeDirection.Value * _lookAheadDistance) : (Vector3?)null;
        }

        private static bool TryAim(Vector3 self, Vector3 target, out Vector3 heading)
        {
            var flat = new Vector3(target.X - self.X, 0f, target.Z - self.Z);
            if (flat.LengthSquared() < 1e-6f)
            {
                heading = Vector3.Zero;
                return false;
            }

            heading = Vector3.Normalize(flat);
            return true;
        }

        private void BecomeReady()
        {
            _phaseElapsed = 0f;
            WindupTarget = null;
            ChargeOrigin = null;
            ChargeDirection = null;
            SetPhase(ChargerPhase.Ready);
        }

        private void SetPhase(ChargerPhase next)
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
