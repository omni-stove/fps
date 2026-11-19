using System;
using System.Numerics;

namespace Shooter.Core
{
    public sealed class CoverState
    {
        public const string LogTag = "[Cover]";

        // スティックの遊びで後ろの入力と見なさないよう、移動入力の y がこれ以下のときだけ後ろの入力とする。
        public const float BackInputThreshold = -0.5f;

        public const string ReasonButton = "button";

        public const string ReasonBack = "back";

        public const string ReasonLost = "lost";

        public const string ReasonBlocked = "blocked";

        // 寄せ終わりの判定で、残りの隙間をこれ以下なら着いたとみなす (m)。
        private const float _arriveDistance = 1e-3f;

        // 経過時間の積み上げの丸めで、最後の tick を 1 つ先へ送らないための余裕 (秒)。
        private const float _timeEpsilon = 1e-5f;

        private const float _sideInputDeadZone = 0.1f;

        private float _enterElapsed;
        private float _backElapsed;
        private float _preferredSide = 1f;
        private float _bodyRadius;
        private MotorSpec _spec;

        public CoverState(MotorSpec spec)
        {
            _spec = spec ?? throw new ArgumentNullException(nameof(spec));
        }

        public MotorSpec Spec
        {
            get => _spec;
            set => _spec = value ?? throw new ArgumentNullException(nameof(value));
        }

        // 体の大きさは設定なので、スナップショットには含めない。
        public float BodyRadius
        {
            get => _bodyRadius;
            set
            {
                if (!(value >= 0f) || float.IsInfinity(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "BodyRadius must be finite and not negative");
                }

                _bodyRadius = value;
            }
        }

        public CoverPhase Phase { get; private set; }

        public bool IsInCover => Phase != CoverPhase.Out;

        public bool IsPeeking => Phase == CoverPhase.Peeking;

        public CoverHeight Height { get; private set; }

        public Vector3 Normal { get; private set; }

        public float PeekOffset { get; private set; }

        public float PreferredSide => _preferredSide;

        public float EnterStep { get; private set; }

        public float EnterTurn { get; private set; }

        public float EnterTimeoutSeconds => _spec.CoverEnterSeconds + (_spec.CoverMaxEnterDistance / _spec.CoverEnterSpeed);

        public string LastExitReason { get; private set; }

        public CoverTransition Step(bool coverPressed, bool aimHeld, Vector2 move, CoverProbeResult probe, float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be finite and not negative");
            }

            if (float.IsNaN(move.X) || float.IsInfinity(move.X) || float.IsNaN(move.Y) || float.IsInfinity(move.Y))
            {
                throw new ArgumentOutOfRangeException(nameof(move), move, "move must be finite");
            }

            EnterStep = 0f;
            EnterTurn = 0f;
            if (Phase == CoverPhase.Out)
            {
                return coverPressed && probe.CanEnter ? Enter(probe) : CoverTransition.None;
            }

            if (coverPressed)
            {
                return Exit(ReasonButton);
            }

            if (!probe.IsWall)
            {
                return Exit(ReasonLost);
            }

            _backElapsed = move.Y <= BackInputThreshold ? _backElapsed + deltaSeconds : 0f;
            if (_backElapsed >= _spec.CoverBackHoldSeconds)
            {
                return Exit(ReasonBack);
            }

            if (Math.Abs(move.X) > _sideInputDeadZone)
            {
                _preferredSide = Math.Sign(move.X);
            }

            Height = probe.Height;
            Normal = probe.Normal;

            switch (Phase)
            {
                case CoverPhase.Entering:
                    return StepEntering(probe, deltaSeconds);

                case CoverPhase.In:
                    return aimHeld ? TryPeek(probe) : CoverTransition.None;

                default:
                    if (aimHeld)
                    {
                        return CoverTransition.None;
                    }

                    Phase = CoverPhase.In;
                    PeekOffset = 0f;
                    return CoverTransition.Unpeek;
            }
        }

        public bool Release(string reason)
        {
            if (string.IsNullOrEmpty(reason))
            {
                throw new ArgumentException("reason must not be empty", nameof(reason));
            }

            if (Phase == CoverPhase.Out)
            {
                return false;
            }

            Exit(reason);
            return true;
        }

        // LastExitReason はログにしか使わないので含めない。
        public CoverSnapshot Capture()
        {
            return new CoverSnapshot(Phase, Height, Normal, PeekOffset, _enterElapsed, _backElapsed, _preferredSide);
        }

        public void Restore(CoverSnapshot snapshot)
        {
            if (!snapshot.IsValid)
            {
                throw new ArgumentOutOfRangeException(nameof(snapshot), "snapshot must be valid");
            }

            Phase = snapshot.Phase;
            Height = snapshot.Height;
            Normal = snapshot.Normal;
            PeekOffset = snapshot.PeekOffset;
            _enterElapsed = snapshot.EnterElapsed;
            _backElapsed = snapshot.BackElapsed;
            _preferredSide = snapshot.PreferredSide < 0f ? -1f : 1f;
        }

        public string FormatLog(CoverTransition transition)
        {
            switch (transition)
            {
                case CoverTransition.None:
                    return null;
                case CoverTransition.Enter:
                    return $"{LogTag} enter {(Height == CoverHeight.Low ? "low" : "high")}";
                case CoverTransition.Peek:
                    return $"{LogTag} peek {(PeekOffset < 0f ? "left" : PeekOffset > 0f ? "right" : "up")}";
                case CoverTransition.Unpeek:
                    return $"{LogTag} unpeek";
                case CoverTransition.Exit:
                    return $"{LogTag} exit {LastExitReason}";
                default:
                    throw new ArgumentOutOfRangeException(nameof(transition), transition, "unknown cover transition");
            }
        }

        private CoverTransition Enter(CoverProbeResult probe)
        {
            Phase = CoverPhase.Entering;
            Height = probe.Height;
            Normal = probe.Normal;
            PeekOffset = 0f;
            _enterElapsed = 0f;
            _backElapsed = 0f;
            return CoverTransition.Enter;
        }

        // 近い壁は CoverEnterSeconds をかけて、遠い壁は CoverEnterSpeed で走って寄せる (遅い方に合わせる)。
        // 着いたかは寄せる予定の距離で決めるので、呼び手が動かせなかった分は次の Step の測り直しで残りに出る。
        private CoverTransition StepEntering(CoverProbeResult probe, float deltaSeconds)
        {
            var gap = Math.Max(0f, probe.Distance - CoverProbe.HoldDistance(_bodyRadius));
            var remaining = _spec.CoverEnterSeconds - _enterElapsed;
            var isLastTimed = remaining <= deltaSeconds + _timeEpsilon;
            _enterElapsed += deltaSeconds;
            var timed = isLastTimed ? gap : gap * deltaSeconds / remaining;
            var step = Math.Min(gap, Math.Min(timed, _spec.CoverEnterSpeed * deltaSeconds));
            EnterStep = step;
            EnterTurn = gap > _arriveDistance ? step / gap : (isLastTimed ? 1f : deltaSeconds / remaining);
            if (isLastTimed && gap - step <= _arriveDistance)
            {
                EnterTurn = 1f;
                Phase = CoverPhase.In;
                return CoverTransition.None;
            }

            return _enterElapsed >= EnterTimeoutSeconds ? Exit(ReasonBlocked) : CoverTransition.None;
        }

        private CoverTransition TryPeek(CoverProbeResult probe)
        {
            var offset = CoverProbe.PeekOffset(probe, _preferredSide, _spec);
            if (probe.Height == CoverHeight.High && offset == 0f)
            {
                return CoverTransition.None;
            }

            Phase = CoverPhase.Peeking;
            PeekOffset = offset;
            return CoverTransition.Peek;
        }

        private CoverTransition Exit(string reason)
        {
            Phase = CoverPhase.Out;
            PeekOffset = 0f;
            _enterElapsed = 0f;
            _backElapsed = 0f;
            LastExitReason = reason;
            return CoverTransition.Exit;
        }
    }
}
