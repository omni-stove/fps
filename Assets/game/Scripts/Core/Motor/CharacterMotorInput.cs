using System;
using System.Numerics;

namespace Fps.Core
{
    public readonly struct CharacterMotorInput
    {
        // 倍率そのものを持つと、予測の記録の空きなど default のままの入力が倍率 0 (動けない) になるため、1 との差で持つ。
        private readonly float _carrySpeedOffset;

        public CharacterMotorInput(
            Vector2 move, float yaw, bool sprint, bool jump, bool crouch, bool downed, bool cover, bool aim, bool dodge, bool vaultAllowed)
            : this(move, yaw, sprint, jump, crouch, downed, cover, aim, dodge, vaultAllowed, false, false, false)
        {
        }

        /// <summary>
        /// prone はこの tick に伏せのボタンが押されたか。proneAllowed (ルール) が false なら伏せていても解き、
        /// ladderAllowed (ルール) が false ならはしごにつかまっていても離す。
        /// carrySpeedMultiplier は持ち物による速さの倍率 (<see cref="CarryLoadSpec.SpeedMultiplier"/>)。送られてこず、動かす側が持っている武器から入れる。
        /// </summary>
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
            float carrySpeedMultiplier = 1f)
        {
            if (!(carrySpeedMultiplier >= 0f) || float.IsInfinity(carrySpeedMultiplier))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(carrySpeedMultiplier), carrySpeedMultiplier, "carrySpeedMultiplier must be finite and not negative");
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

        public CharacterMotorInput Restrained()
        {
            return Downed
                ? new CharacterMotorInput(Vector2.Zero, Yaw, false, false, true, true, false, false, false, false, false, false, false, CarrySpeedMultiplier)
                : this;
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

            var input = rules.Cover == Fps.Core.Cover.On ? this : WithoutCover();
            input = rules.Dodge == Fps.Core.Dodge.On ? input : input.WithoutDodge();
            return input.With(
                vaultAllowed: rules.Vault == Fps.Core.Vault.On,
                proneAllowed: rules.Prone == Fps.Core.Prone.On,
                ladderAllowed: rules.Ladder == Fps.Core.Ladder.On);
        }

        public CharacterMotorInput With(
            bool? cover = null, bool? dodge = null, bool? vaultAllowed = null, bool? prone = null, bool? proneAllowed = null, bool? ladderAllowed = null,
            float? carrySpeedMultiplier = null)
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
                carrySpeedMultiplier ?? CarrySpeedMultiplier);
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
