using Fps.Core;
using UnityEngine;

namespace Fps.Net
{
    /// <summary><see cref="NetworkPlayerMotor"/> の 1 tick 分の状態。tick の入力を適用した後の値。</summary>
    public readonly struct MotorState
    {
        /// <summary>
        /// 姿勢・カバー・回避・乗り越えの状態が食い違ったときの <see cref="PositionError"/>。どんな許容値も超えるので必ず巻き戻す。
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

        /// <summary>この tick の後の回避の状態 (転がりの進みと待ち時間)。</summary>
        public NetDodgeState Dodge { get; }

        /// <summary>この tick の後の乗り越えの状態 (たどっている軌跡と進み)。</summary>
        public NetVaultState Vault { get; }

        public MotorState(Vector3 position, Vector3 velocity, bool isGrounded, bool isCrouched)
            : this(position, velocity, isGrounded, isCrouched, NetCoverState.Out)
        {
        }

        public MotorState(Vector3 position, Vector3 velocity, bool isGrounded, bool isCrouched, NetCoverState cover)
            : this(position, velocity, isGrounded, isCrouched, cover, NetDodgeState.Ready, NetVaultState.None)
        {
        }

        public MotorState(
            Vector3 position,
            Vector3 velocity,
            bool isGrounded,
            bool isCrouched,
            NetCoverState cover,
            NetDodgeState dodge,
            NetVaultState vault)
        {
            Position = position;
            Velocity = velocity;
            IsGrounded = isGrounded;
            IsCrouched = isCrouched;
            Cover = cover;
            Dodge = dodge;
            Vault = vault;
        }

        /// <summary>回避の最中か。</summary>

        public static MotorState FromCore(CharacterMotorState state)
        {
            return new MotorState(
                NetMath.ToUnity(state.Position),
                NetMath.ToUnity(state.Velocity),
                state.IsGrounded,
                state.IsCrouched,
                NetCoverState.FromCore(state.Cover),
                new NetDodgeState(state.Dodge),
                NetVaultState.FromCore(state.Vault));
        }

        public CharacterMotorState ToCore()
        {
            return new CharacterMotorState(
                NetMath.ToNumerics(Position),
                NetMath.ToNumerics(Velocity),
                IsGrounded,
                IsCrouched,
                Cover.ToCore(),
                Dodge.Snapshot,
                Vault.ToCore());
        }

        public bool IsDodging => Dodge.IsRolling;

        /// <summary>乗り越えの最中か。</summary>
        public bool IsVaulting => Vault.IsVaulting;

        /// <summary>
        /// 位置と速度がすべて有限値で、カバー・回避・乗り越えの状態が正しいか。ネットワークから受け取った値の検査に使う。
        /// </summary>
        public bool IsFinite => NetMath.IsFinite(Position) && NetMath.IsFinite(Velocity) && Cover.IsValid && Dodge.IsValid && Vault.IsValid;

        /// <summary>
        /// 予測と確定状態のずれ。姿勢とカバー・回避・乗り越えの状態が同じなら位置の距離 (m)、しゃがみ・カバーの段階 (<see cref="NetCoverState.Phase"/>)・
        /// カバー中の壁の高さ・転がっているか・乗り越えているかのどれかが食い違っていれば <see cref="StanceMismatchError"/>。
        /// </summary>
        /// <remarks>
        /// 位置が一致していても姿勢が違えばカプセルの高さが違い、以降の移動 (頭上の検査・しゃがみの速さ) も目の高さも変わるので、
        /// 位置の許容値に埋もれさせず必ず巻き戻す。カバーも同じで、段階が違えば以降の移動の向きと速さ、ブラインドファイアの始点が変わる。
        /// 回避と乗り越えも、始めた直後は位置がまだ近くても以降の動きがまるで違い、射撃の可否も変わるので同じ扱いにする。
        /// 待ち時間の食い違いだけでは巻き戻さない (次に始められるかが変われば、そのとき転がっているかが食い違う)。
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

            if (predicted.IsDodging != authoritative.IsDodging || predicted.IsVaulting != authoritative.IsVaulting)
            {
                return StanceMismatchError;
            }

            return Vector3.Distance(predicted.Position, authoritative.Position);
        }
    }
}
