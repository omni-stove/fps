using Fps.Core;
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

        /// <summary>
        /// この tick に Cover が押されたか (カバーに入る・出る)。ルールの <see cref="Fps.Core.ActionRules.Cover"/> が Off なら、
        /// オーナーは送らず、サーバーは受け取っても false にする。
        /// </summary>
        public bool Cover { get; }

        /// <summary>覗き込んでいるか。カバー中ならこれで覗く (<see cref="Fps.Core.CoverState.Step"/> の aimHeld)。</summary>
        public bool Aim { get; }

        /// <summary>
        /// この tick に Dodge が押されたか (回避を始める)。ルールの <see cref="Fps.Core.ActionRules.Dodge"/> が Off なら、
        /// オーナーは送らず、サーバーは受け取っても false にする。
        /// </summary>
        public bool Dodge { get; }

        /// <summary>
        /// この tick の <see cref="Jump"/> で前方の低い障害物を乗り越えてよいか (ルールの <see cref="Fps.Core.ActionRules.Vault"/> が On)。
        /// クライアントからは送らず、サーバーは自分の知るルールを、予測は同期されたルールを入れる。乗り越えられなければふつうにジャンプする。
        /// </summary>
        public bool VaultAllowed { get; }

        public MotorInput(Vector2 move, float yaw, bool sprint, bool jump, bool crouch)
            : this(move, yaw, sprint, jump, crouch, false)
        {
        }

        public MotorInput(Vector2 move, float yaw, bool sprint, bool jump, bool crouch, bool downed)
            : this(move, yaw, sprint, jump, crouch, downed, false, false)
        {
        }

        public MotorInput(Vector2 move, float yaw, bool sprint, bool jump, bool crouch, bool downed, bool cover, bool aim)
            : this(move, yaw, sprint, jump, crouch, downed, cover, aim, false, false)
        {
        }

        public MotorInput(
            Vector2 move, float yaw, bool sprint, bool jump, bool crouch, bool downed, bool cover, bool aim, bool dodge, bool vaultAllowed)
        {
            Move = Vector2.ClampMagnitude(move, 1f);
            Yaw = yaw;
            Sprint = sprint;
            Jump = jump;
            Crouch = crouch;
            Downed = downed;
            Cover = cover;
            Aim = aim;
            Dodge = dodge;
            VaultAllowed = vaultAllowed;
        }

        /// <summary>
        /// ダウン中に実際に使う入力。移動・ダッシュ・ジャンプ・カバー・覗き込み・回避を捨て、向きは残し、カプセルを低くするためしゃがませる
        /// (当たり判定と目の高さが寝かせた見た目に近くなる)。<see cref="Downed"/> でなければそのまま返す。
        /// </summary>
        public MotorInput Restrained()
        {
            return Downed ? new MotorInput(Vector2.zero, Yaw, false, false, true, true, false, false, false, false) : this;
        }

        /// <summary>Cover の押下だけを捨てた入力。カバーがルールで Off のときにサーバーが使う。</summary>
        public MotorInput WithoutCover()
        {
            return Cover ? new MotorInput(Move, Yaw, Sprint, Jump, Crouch, Downed, false, Aim, Dodge, VaultAllowed) : this;
        }

        /// <summary>Dodge の押下だけを捨てた入力。回避がルールで Off のときにサーバーが使う。</summary>
        public MotorInput WithoutDodge()
        {
            return Dodge ? new MotorInput(Move, Yaw, Sprint, Jump, Crouch, Downed, Cover, Aim, false, VaultAllowed) : this;
        }

        /// <summary><see cref="VaultAllowed"/> だけを差し替えた入力。サーバーと予測が、それぞれの知るルールを入れるのに使う。</summary>
        public MotorInput WithVaultAllowed(bool allowed)
        {
            return VaultAllowed == allowed ? this : new MotorInput(Move, Yaw, Sprint, Jump, Crouch, Downed, Cover, Aim, Dodge, allowed);
        }

        /// <summary>移動入力と向きがすべて有限値か。ネットワークから受け取った値の検査に使う。</summary>
        public CharacterMotorInput ToCore()
        {
            return new CharacterMotorInput(NetMath.ToNumerics(Move), Yaw, Sprint, Jump, Crouch, Downed, Cover, Aim, Dodge, VaultAllowed);
        }

        public static bool IsValid(Vector2 move, float yaw)
        {
            return NetMath.IsFinite(move.x) && NetMath.IsFinite(move.y) && NetMath.IsFinite(yaw);
        }
    }
}
