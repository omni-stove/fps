using UnityEngine;

namespace Fps.Net
{
    /// <summary><see cref="NetworkPlayerMotor"/> が 1 tick ごとに <see cref="Fps.Core.PlayerMotor"/> へ渡す入力。</summary>
    public readonly struct MotorInput
    {
        /// <summary>移動入力。X が右、Y が前。長さは 1 以下。</summary>
        public Vector2 Move { get; }

        /// <summary>向き (Y 軸まわり、ラジアン)。</summary>
        public float Yaw { get; }

        public bool Sprint { get; }

        /// <summary>この tick にジャンプが押されたか。</summary>
        public bool Jump { get; }

        /// <summary>しゃがみを押しているか。離しても頭上が塞がっていればしゃがんだまま。</summary>
        public bool Crouch { get; }

        /// <summary>
        /// ダウン中か。クライアントからは送らず、サーバーは自分の知るダウンの状態を、予測は同期されたダウンの状態を入れる。
        /// true なら <see cref="NetMotorSimulation"/> は <see cref="Restrained"/> で動かす。
        /// </summary>
        public bool Downed { get; }

        public MotorInput(Vector2 move, float yaw, bool sprint, bool jump, bool crouch)
            : this(move, yaw, sprint, jump, crouch, false)
        {
        }

        public MotorInput(Vector2 move, float yaw, bool sprint, bool jump, bool crouch, bool downed)
        {
            Move = Vector2.ClampMagnitude(move, 1f);
            Yaw = yaw;
            Sprint = sprint;
            Jump = jump;
            Crouch = crouch;
            Downed = downed;
        }

        /// <summary>
        /// ダウン中に実際に使う入力。移動・ダッシュ・ジャンプを捨て、向きは残し、カプセルを低くするためしゃがませる
        /// (当たり判定と目の高さが寝かせた見た目に近くなる)。<see cref="Downed"/> でなければそのまま返す。
        /// </summary>
        public MotorInput Restrained()
        {
            return Downed ? new MotorInput(Vector2.zero, Yaw, false, false, true, true) : this;
        }

        /// <summary>移動入力と向きがすべて有限値か。ネットワークから受け取った値の検査に使う。</summary>
        public static bool IsValid(Vector2 move, float yaw)
        {
            return NetMath.IsFinite(move.x) && NetMath.IsFinite(move.y) && NetMath.IsFinite(yaw);
        }
    }
}
