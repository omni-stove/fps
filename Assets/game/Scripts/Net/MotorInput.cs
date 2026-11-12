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

        public MotorInput(Vector2 move, float yaw, bool sprint, bool jump)
        {
            Move = Vector2.ClampMagnitude(move, 1f);
            Yaw = yaw;
            Sprint = sprint;
            Jump = jump;
        }

        /// <summary>移動入力と向きがすべて有限値か。ネットワークから受け取った値の検査に使う。</summary>
        public static bool IsValid(Vector2 move, float yaw)
        {
            return NetMath.IsFinite(move.x) && NetMath.IsFinite(move.y) && NetMath.IsFinite(yaw);
        }
    }
}
