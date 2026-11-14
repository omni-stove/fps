using System;
using System.Collections.Generic;
using System.Numerics;

namespace Fps.Core
{
    // 狙う → 撃つ → 位置を変える、を回す。撃てたかは武器の連射間隔や弾切れで決まるので、Firing のまま Runtime の ConfirmShot を待つ。
    // 追ったり捜したりして足場を降りないよう、種類が Sniper の間は移動を常に上書きする。
    public sealed class SniperEnemyBehavior : IEnemyBehavior
    {
        private Vector3[] _perches = Array.Empty<Vector3>();
        private int _perchIndex = -1;
        private float _phaseElapsed;
        private float _reaimRemaining;
        private SniperSpec _spec = SniperSpec.Default;

        public event Action<SniperPhase, SniperPhase> PhaseChanged;

        public bool Enabled { get; set; } = true;

        public SniperPhase Phase { get; private set; } = SniperPhase.Holding;

        public IReadOnlyList<Vector3> Perches => _perches;

        public int PerchIndex => _perchIndex;

        public float AimElapsed => Phase == SniperPhase.Aiming || Phase == SniperPhase.Firing ? _phaseElapsed : 0f;

        public Vector3? AimTarget { get; private set; }

        public float ReaimRemaining => _reaimRemaining;

        public bool LastRelocationGaveUp { get; private set; }

        public void SetPerches(IReadOnlyList<Vector3> perches)
        {
            if (perches == null)
            {
                throw new ArgumentNullException(nameof(perches));
            }

            var copy = new Vector3[perches.Count];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = perches[i];
            }

            _perches = copy;
            _perchIndex = -1;
            if (Phase == SniperPhase.Relocating)
            {
                Hold(0f);
            }
        }

        // self は撃った瞬間の足元。次の足場は、今いる足場 (いちばん近いもの) の次にする。同じ場所から撃ち続けさせないため。
        public bool ConfirmShot(Vector3 self)
        {
            if (Phase != SniperPhase.Firing)
            {
                return false;
            }

            LastRelocationGaveUp = false;
            var next = NextPerch(self);
            if (next < 0)
            {
                Hold(_spec.ReaimDelaySeconds);
                return true;
            }

            _perchIndex = next;
            _phaseElapsed = 0f;
            AimTarget = null;
            SetPhase(SniperPhase.Relocating);
            return true;
        }

        public bool CancelAim()
        {
            if (Phase != SniperPhase.Aiming && Phase != SniperPhase.Firing)
            {
                return false;
            }

            Hold(0f);
            return true;
        }

        public void Reset()
        {
            _perchIndex = -1;
            LastRelocationGaveUp = false;
            Hold(0f);
        }

        public bool TryOverride(EnemyBrain brain, float deltaSeconds, EnemyPerception perception, EnemyDecision planned, out EnemyDecision decision)
        {
            if (brain == null)
            {
                throw new ArgumentNullException(nameof(brain));
            }

            decision = planned;
            if (!Enabled || brain.IsDead || brain.Spec.Archetype != EnemyArchetype.Sniper)
            {
                if (Phase != SniperPhase.Holding)
                {
                    Hold(0f);
                }

                return false;
            }

            _spec = brain.Spec.Sniper;
            var target = perception.Target;
            var seen = target.HasValue
                && perception.HasLineOfSight
                && perception.Awareness >= EnemyPerception.FullAwareness
                && Vector3.Distance(perception.Self, target.Value) <= brain.Spec.AttackRange;
            var look = seen ? target : planned.LookAt;

            switch (Phase)
            {
                case SniperPhase.Relocating:
                    _phaseElapsed += deltaSeconds;
                    var perch = _perches[_perchIndex];
                    if (HorizontalDistance(perception.Self, perch) <= EnemyBrain.ArrivalDistance)
                    {
                        Hold(_spec.ReaimDelaySeconds);
                        decision = new EnemyDecision(null, look, false, false);
                        return true;
                    }

                    if (_phaseElapsed >= _spec.RelocateGiveUpSeconds)
                    {
                        LastRelocationGaveUp = true;
                        Hold(_spec.ReaimDelaySeconds);
                        decision = new EnemyDecision(null, look, false, false);
                        return true;
                    }

                    decision = new EnemyDecision(perch, seen ? target : perch, false, false);
                    return true;

                case SniperPhase.Aiming:
                case SniperPhase.Firing:
                    // 光線を見て物陰に入れば撃たれない、という逃げ道を残すため、見失ったら撃たずにやめる。
                    if (!seen)
                    {
                        Hold(0f);
                        decision = new EnemyDecision(null, planned.LookAt, false, false);
                        return true;
                    }

                    AimTarget = target;
                    if (Phase == SniperPhase.Aiming)
                    {
                        _phaseElapsed += deltaSeconds;
                        if (_phaseElapsed >= _spec.AimSeconds)
                        {
                            SetPhase(SniperPhase.Firing);
                        }
                    }

                    decision = new EnemyDecision(null, target, Phase == SniperPhase.Firing, false);
                    return true;
            }

            _reaimRemaining = Math.Max(0f, _reaimRemaining - deltaSeconds);
            if (seen && _reaimRemaining <= 0f)
            {
                _phaseElapsed = 0f;
                AimTarget = target;
                SetPhase(SniperPhase.Aiming);
            }

            decision = new EnemyDecision(null, look, false, false);
            return true;
        }

        private int NextPerch(Vector3 self)
        {
            if (_perches.Length == 0)
            {
                return -1;
            }

            var nearest = 0;
            var nearestDistance = float.MaxValue;
            for (var i = 0; i < _perches.Length; i++)
            {
                var distance = Vector3.DistanceSquared(self, _perches[i]);
                if (distance < nearestDistance)
                {
                    nearest = i;
                    nearestDistance = distance;
                }
            }

            if (_perches.Length > 1)
            {
                return (nearest + 1) % _perches.Length;
            }

            return HorizontalDistance(self, _perches[0]) > EnemyBrain.ArrivalDistance ? 0 : -1;
        }

        private void Hold(float reaimSeconds)
        {
            _reaimRemaining = reaimSeconds;
            _phaseElapsed = 0f;
            AimTarget = null;
            SetPhase(SniperPhase.Holding);
        }

        private void SetPhase(SniperPhase next)
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
