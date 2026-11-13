using System;
using System.Numerics;

namespace Fps.Core
{
    /// <summary>
    /// カバーの状態 (外 / 入りかけ / 中 / 覗き) の遷移。1 フレームごとに入力と <see cref="CoverProbe.Evaluate"/> の結果で <see cref="Step"/> を呼ぶ。
    /// 外で Cover を押し、入れる壁 (<see cref="CoverProbeResult.CanEnter"/>) があれば入りかけになり、<see cref="EnterSeconds"/> 後に中になる。
    /// 中で Aim を押している間、低い壁なら立って、高い壁なら端にいるときだけ横に出て覗く。
    /// もう一度 Cover を押すか、後ろの入力を <see cref="BackHoldSeconds"/> 続けるか、壁が無くなると出る。
    /// 左右は壁に向いたときの左右 (移動入力の x の正が右)。
    /// </summary>
    public sealed class CoverState
    {
        public const string LogTag = "[Cover]";

        /// <summary>壁へ寄せるのにかける時間 (秒)。</summary>
        public const float EnterSeconds = 0.15f;

        /// <summary>後ろの入力をこれだけ続けると出る (秒)。</summary>
        public const float BackHoldSeconds = 0.2f;

        /// <summary>移動入力の y がこれ以下なら後ろの入力と見なす (スティックの遊びで出ないように)。</summary>
        public const float BackInputThreshold = -0.5f;

        /// <summary>もう一度 Cover を押した。</summary>
        public const string ReasonButton = "button";

        /// <summary>後ろの入力を続けた。</summary>
        public const string ReasonBack = "back";

        /// <summary>壁が無くなった (壊れた・端から出た)。</summary>
        public const string ReasonLost = "lost";

        // 横の入力をこれより小さければ、覗く側の好みを変えない。
        private const float _sideInputDeadZone = 0.1f;

        private float _enterElapsed;
        private float _backElapsed;
        private float _preferredSide = 1f;

        /// <summary>今の状態。最初は <see cref="CoverPhase.Out"/>。</summary>
        public CoverPhase Phase { get; private set; }

        /// <summary>壁に張り付いている (入りかけ・中・覗き) か。</summary>
        public bool IsInCover => Phase != CoverPhase.Out;

        /// <summary>覗いているか。</summary>
        public bool IsPeeking => Phase == CoverPhase.Peeking;

        /// <summary>張り付いている壁の高さの区分。<see cref="IsInCover"/> が false なら最後の値。</summary>
        public CoverHeight Height { get; private set; }

        /// <summary>張り付いている壁の、水平で長さ 1 のこちら向きの法線。<see cref="IsInCover"/> が false なら最後の値。</summary>
        public Vector3 Normal { get; private set; }

        /// <summary>
        /// 覗いている間に壁沿いに横へ出る量 (m、右が正)。高い壁の端から覗いているときだけ 0 でない。
        /// 覗いている間は覗き始めの値を保つ。
        /// </summary>
        public float PeekOffset { get; private set; }

        /// <summary>両側とも端の壁で覗く・ブラインドファイアで出る側。負なら左、それ以外は右。最後に横へ入力した側で、最初は右。</summary>
        public float PreferredSide => _preferredSide;

        /// <summary>入りかけになってからの割合 (0〜1)。中・覗きでは 1、外では 0。</summary>
        public float EnterProgress
        {
            get
            {
                switch (Phase)
                {
                    case CoverPhase.Out:
                        return 0f;
                    case CoverPhase.Entering:
                        return Math.Min(1f, _enterElapsed / EnterSeconds);
                    default:
                        return 1f;
                }
            }
        }

        /// <summary>最後の <see cref="CoverTransition.Exit"/> の理由 (<see cref="ReasonButton"/> など)。出たことが無ければ null。</summary>
        public string LastExitReason { get; private set; }

        /// <summary>
        /// 1 フレーム分進め、ログに出す変化を返す。
        /// </summary>
        /// <param name="coverPressed">このフレームで Cover を押したか。</param>
        /// <param name="aimHeld">Aim を押しているか (覗くか)。</param>
        /// <param name="move">移動入力 (x が右、y が前)。</param>
        /// <param name="probe">今の壁の判定。カバー中は壁へ向かう方向で調べたもの。</param>
        /// <param name="deltaSeconds">経過秒数。0 以上。</param>
        /// <exception cref="ArgumentOutOfRangeException">deltaSeconds か move が有限でないか、deltaSeconds が負のとき。</exception>
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
            if (_backElapsed >= BackHoldSeconds)
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
                    if (_enterElapsed >= EnterSeconds)
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

        /// <summary>
        /// 外の理由 (死亡・無効化など) でカバーから出る。カバー中だったら true。
        /// </summary>
        /// <param name="reason">ログに出す理由。</param>
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

        /// <summary>今の中身を写す。<see cref="LastExitReason"/> はログにしか使わないので含めない。</summary>
        public CoverSnapshot Capture()
        {
            return new CoverSnapshot(Phase, Height, Normal, PeekOffset, _enterElapsed, _backElapsed, _preferredSide);
        }

        /// <summary>
        /// <see cref="Capture"/> で写した中身に戻す。同じ中身から同じ入力で <see cref="Step"/> すれば同じ結果になる。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">snapshot が <see cref="CoverSnapshot.IsValid"/> でないとき。</exception>
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

        /// <summary>
        /// 変化のログ。例: <c>[Cover] enter low</c>、<c>[Cover] peek left</c>、<c>[Cover] unpeek</c>、<c>[Cover] exit back</c>。
        /// <see cref="CoverTransition.None"/> なら null。
        /// </summary>
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
            var offset = CoverProbe.PeekOffset(probe, _preferredSide);
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
