using System;
using System.Numerics;

namespace Fps.Core
{
    public readonly struct CharacterMotorInput
    {
        public CharacterMotorInput(
            Vector2 move, float yaw, bool sprint, bool jump, bool crouch, bool downed, bool cover, bool aim, bool dodge, bool vaultAllowed)
            : this(move, yaw, sprint, jump, crouch, downed, cover, aim, dodge, vaultAllowed, false, false, false)
        {
        }

        /// <summary>
        /// prone はこの tick に伏せのボタンが押されたか。proneAllowed (ルール) が false なら伏せていても解き、
        /// ladderAllowed (ルール) が false ならはしごにつかまっていても離す。
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
            bool ladderAllowed)
        {
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

        public CharacterMotorInput Restrained()
        {
            return Downed
                ? new CharacterMotorInput(Vector2.Zero, Yaw, false, false, true, true, false, false, false, false, false, false, false)
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
            bool? cover = null, bool? dodge = null, bool? vaultAllowed = null, bool? prone = null, bool? proneAllowed = null, bool? ladderAllowed = null)
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
                ladderAllowed ?? LadderAllowed);
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
