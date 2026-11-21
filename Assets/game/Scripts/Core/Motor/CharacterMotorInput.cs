using System;
using System.Numerics;

namespace Shooter.Core
{
    public readonly struct CharacterMotorInput
    {
        // 倍率そのものを持つと、予測の記録の空きなど default のままの入力が倍率 0 (動けない) になるため、1 との差で持つ。
        private readonly float _carrySpeedOffset;

        // 許すかを持つと、default のままの入力と古い呼び出しが跳べなくなるため、禁じるかで持つ。
        private readonly bool _jumpForbidden;

        private readonly sbyte _lean;

        private readonly PlatformMotion _platform;

        public CharacterMotorInput(
            Vector2 move, float yaw, bool sprint, bool jump, bool crouch, bool downed, bool cover, bool aim, bool dodge, bool vaultAllowed)
            : this(move, yaw, sprint, jump, crouch, downed, cover, aim, dodge, vaultAllowed, false, false, false)
        {
        }

        public CharacterMotorInput(
            Vector2 move,
            float yaw,
            bool sprint,
            bool jump,
            bool crouch,
            bool downed,
            bool cover,
            bool aim,
            bool dodge,
            bool vaultAllowed,
            bool prone,
            bool proneAllowed,
            bool ladderAllowed,
            float carrySpeedMultiplier = 1f,
            bool jumpAllowed = true,
            int lean = 0,
            PlatformMotion platform = default,
            bool openParachute = false,
            bool closeParachute = false,
            bool parachuteAllowed = false,
            bool slide = false,
            bool slideAllowed = false)
        {
            if (!(carrySpeedMultiplier >= 0f) || float.IsInfinity(carrySpeedMultiplier))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(carrySpeedMultiplier), carrySpeedMultiplier, "carrySpeedMultiplier must be finite and not negative");
            }

            if (lean < -1 || lean > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(lean), lean, "lean must be -1, 0 or 1");
            }

            Move = ClampLength(move);
            Yaw = yaw;
            Sprint = sprint;
            Jump = jump;
            Crouch = crouch;
            Downed = downed;
            Cover = cover;
            Aim = aim;
            Dodge = dodge;
            VaultAllowed = vaultAllowed;
            Prone = prone;
            ProneAllowed = proneAllowed;
            LadderAllowed = ladderAllowed;
            _carrySpeedOffset = carrySpeedMultiplier - 1f;
            _jumpForbidden = !jumpAllowed;
            _lean = (sbyte)lean;
            _platform = platform;
            OpenParachute = openParachute;
            CloseParachute = closeParachute;
            ParachuteAllowed = parachuteAllowed;
            Slide = slide;
            SlideAllowed = slideAllowed;
        }

        public Vector2 Move { get; }

        public float Yaw { get; }

        public bool Sprint { get; }

        public bool Jump { get; }

        public bool Crouch { get; }

        public bool Downed { get; }

        public bool Cover { get; }

        public bool Aim { get; }

        public bool Dodge { get; }

        public bool VaultAllowed { get; }

        public bool Prone { get; }

        public bool ProneAllowed { get; }

        public bool LadderAllowed { get; }

        public float CarrySpeedMultiplier => _carrySpeedOffset + 1f;

        public bool JumpAllowed => !_jumpForbidden;

        public int Lean => _lean;

        public PlatformMotion Platform => _platform;

        public bool OpenParachute { get; }

        public bool CloseParachute { get; }

        public bool ParachuteAllowed { get; }

        // 滑り出したいか。始められるか (走っている速さ・立っているか・接地) はシミュレーションが状態で決める
        public bool Slide { get; }

        public bool SlideAllowed { get; }

        // 開いているパラシュートを許すかは残す。倒れただけで閉じると、空中で倒れた人がそのまま落ちて死ぬため。
        public CharacterMotorInput Restrained()
        {
            return Downed
                ? new CharacterMotorInput(
                    Vector2.Zero, Yaw, false, false, true, true, false, false, false, false, false, false, false, CarrySpeedMultiplier, JumpAllowed,
                    platform: Platform, parachuteAllowed: ParachuteAllowed)
                : this;
        }

        public CharacterMotorInput WithLean(int lean)
        {
            return Lean == lean ? this : With(lean: lean);
        }

        public CharacterMotorInput WithoutCover()
        {
            return Cover ? With(cover: false) : this;
        }

        public CharacterMotorInput WithoutDodge()
        {
            return Dodge ? With(dodge: false) : this;
        }

        public CharacterMotorInput WithVaultAllowed(bool allowed)
        {
            return VaultAllowed == allowed ? this : With(vaultAllowed: allowed);
        }

        public CharacterMotorInput WithPlatform(PlatformMotion platform)
        {
            return Platform.Equals(platform) ? this : With(platform: platform);
        }

        public CharacterMotorInput WithCarrySpeedMultiplier(float carrySpeedMultiplier)
        {
            return CarrySpeedMultiplier == carrySpeedMultiplier ? this : With(carrySpeedMultiplier: carrySpeedMultiplier);
        }

        public CharacterMotorInput WithRules(ActionRules rules)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            var input = rules.Cover == Shooter.Core.Cover.On ? this : WithoutCover();
            input = rules.Dodge == Shooter.Core.Dodge.On ? input : input.WithoutDodge();
            return input.With(
                vaultAllowed: rules.Vault == Shooter.Core.Vault.On,
                proneAllowed: rules.Prone == Shooter.Core.Prone.On,
                ladderAllowed: rules.Ladder == Shooter.Core.Ladder.On,
                jumpAllowed: rules.Jump == Shooter.Core.Jump.On,
                parachuteAllowed: rules.Parachute == Shooter.Core.Parachute.On,
                slideAllowed: rules.Slide == Shooter.Core.Slide.On);
        }

        public CharacterMotorInput With(
            bool? cover = null, bool? dodge = null, bool? vaultAllowed = null, bool? prone = null, bool? proneAllowed = null, bool? ladderAllowed = null,
            float? carrySpeedMultiplier = null, bool? jumpAllowed = null, int? lean = null, PlatformMotion? platform = null,
            bool? openParachute = null, bool? closeParachute = null, bool? parachuteAllowed = null, bool? slide = null, bool? slideAllowed = null)
        {
            return new CharacterMotorInput(
                Move,
                Yaw,
                Sprint,
                Jump,
                Crouch,
                Downed,
                cover ?? Cover,
                Aim,
                dodge ?? Dodge,
                vaultAllowed ?? VaultAllowed,
                prone ?? Prone,
                proneAllowed ?? ProneAllowed,
                ladderAllowed ?? LadderAllowed,
                carrySpeedMultiplier ?? CarrySpeedMultiplier,
                jumpAllowed ?? JumpAllowed,
                lean ?? Lean,
                platform ?? Platform,
                openParachute ?? OpenParachute,
                closeParachute ?? CloseParachute,
                parachuteAllowed ?? ParachuteAllowed,
                slide ?? Slide,
                slideAllowed ?? SlideAllowed);
        }

        public static bool IsValid(Vector2 move, float yaw)
        {
            return IsFinite(move.X) && IsFinite(move.Y) && IsFinite(yaw);
        }

        // Unity の Vector2.ClampMagnitude と同じく、長さ 1 を超えるときだけ縮める
        private static Vector2 ClampLength(Vector2 move)
        {
            var lengthSquared = (move.X * move.X) + (move.Y * move.Y);
            if (!(lengthSquared > 1f))
            {
                return move;
            }

            var length = MathF.Sqrt(lengthSquared);
            return new Vector2(move.X / length, move.Y / length);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
