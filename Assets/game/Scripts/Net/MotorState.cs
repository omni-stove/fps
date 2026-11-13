using UnityEngine;

namespace Fps.Net
{
    /// <summary><see cref="NetworkPlayerMotor"/> の 1 tick 分の状態。tick の入力を適用した後の値。</summary>
    public readonly struct MotorState
    {
        /// <summary>
        /// 姿勢かカバーの状態が食い違ったときの <see cref="PositionError"/>。どんな許容値も超えるので必ず巻き戻す。
        /// </summary>
        public const float StanceMismatchError = float.PositiveInfinity;

        /// <summary>プレイヤー本体のワールド座標 (m)。しゃがんでも足元を保つので、この値は変わらない。</summary>
        public Vector3 Position { get; }

        /// <summary>速度 (m/s)。</summary>
        public Vector3 Velocity { get; }

        /// <summary>この tick の移動の結果、接地しているか。</summary>
        public bool IsGrounded { get; }

        /// <summary>この tick の移動の後、しゃがんでいるか。</summary>
        public bool IsCrouched { get; }

        /// <summary>この tick の後のカバーの状態。</summary>
        public NetCoverState Cover { get; }

        public MotorState(Vector3 position, Vector3 velocity, bool isGrounded, bool isCrouched)
            : this(position, velocity, isGrounded, isCrouched, NetCoverState.Out)
        {
        }

        public MotorState(Vector3 position, Vector3 velocity, bool isGrounded, bool isCrouched, NetCoverState cover)
        {
            Position = position;
            Velocity = velocity;
            IsGrounded = isGrounded;
            IsCrouched = isCrouched;
            Cover = cover;
        }

        /// <summary>位置と速度がすべて有限値で、カバーの状態が正しいか。ネットワークから受け取った値の検査に使う。</summary>
        public bool IsFinite => NetMath.IsFinite(Position) && NetMath.IsFinite(Velocity) && Cover.IsValid;

        /// <summary>
        /// 予測と確定状態のずれ。姿勢とカバーの状態が同じなら位置の距離 (m)、しゃがみ・カバーの段階 (<see cref="NetCoverState.Phase"/>)・
        /// カバー中の壁の高さのどれかが食い違っていれば <see cref="StanceMismatchError"/>。
        /// </summary>
        /// <remarks>
        /// 位置が一致していても姿勢が違えばカプセルの高さが違い、以降の移動 (頭上の検査・しゃがみの速さ) も目の高さも変わるので、
        /// 位置の許容値に埋もれさせず必ず巻き戻す。カバーも同じで、段階が違えば以降の移動の向きと速さ、ブラインドファイアの始点が変わる。
        /// </remarks>
        public static float PositionError(MotorState predicted, MotorState authoritative)
        {
            if (predicted.IsCrouched != authoritative.IsCrouched)
            {
                return StanceMismatchError;
            }

            var mine = predicted.Cover;
            var theirs = authoritative.Cover;
            if (mine.Phase != theirs.Phase || (mine.IsInCover && mine.Height != theirs.Height))
            {
                return StanceMismatchError;
            }

            return Vector3.Distance(predicted.Position, authoritative.Position);
        }
    }
}
