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

        private const float _sideInputDeadZone = 0.1f;

        private float _enterElapsed;
        private float _backElapsed;
        private float _preferredSide = 1f;
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

        public CoverPhase Phase { get; private set; }

        public bool IsInCover => Phase != CoverPhase.Out;

        public bool IsPeeking => Phase == CoverPhase.Peeking;

        public CoverHeight Height { get; private set; }

        public Vector3 Normal { get; private set; }

        public float PeekOffset { get; private set; }

        public float PreferredSide => _preferredSide;

        public float EnterProgress
        {
            get
            {
                switch (Phase)
                {
                    case CoverPhase.Out:
                        return 0f;
                    case CoverPhase.Entering:
                        return Math.Min(1f, _enterElapsed / _spec.CoverEnterSeconds);
                    default:
                        return 1f;
                }
            }
        }

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
                    _enterElapsed += deltaSeconds;
                    if (_enterElapsed >= _spec.CoverEnterSeconds)
                    {
                        Phase = CoverPhase.In;
                    }

                    return CoverTransition.None;

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
