using System.Numerics;

namespace Fps.Core
{
    /// <summary>
    /// <see cref="BotDriver"/> が決めた 1 tick 分の Bot の入力。人のプレイヤーと同じ移動と射撃に渡す。
    /// </summary>
    public readonly struct BotInputPlan
    {
        /// <summary>移動入力。X が右、Y が前 (<see cref="Yaw"/> を向いた本体から見た向き)。長さは 0 か 1。</summary>
        public Vector2 Move { get; }

        /// <summary>この tick の向き (左右, 度)。[0, 360)、0 で +Z、正で右回り。</summary>
        public float Yaw { get; }

        /// <summary>この tick の向き (上下, 度)。正が下 (Unity の X 軸回転と同じ)。</summary>
        public float Pitch { get; }

        public bool Sprint { get; }

        public bool Crouch { get; }

        /// <summary>この tick に撃つか。</summary>
        public bool Fire { get; }

        /// <summary>射撃の始点 (目)。<see cref="Fire"/> が false でも今の目の位置が入る。</summary>
        public Vector3 FireOrigin { get; }

        /// <summary>射撃の向き (長さ 1)。この tick の <see cref="Yaw"/> と <see cref="Pitch"/> の正面。</summary>
        public Vector3 FireDirection { get; }

        public BotInputPlan(
            Vector2 move, float yaw, float pitch, bool sprint, bool crouch, bool fire, Vector3 fireOrigin, Vector3 fireDirection)
        {
            Move = move;
            Yaw = yaw;
            Pitch = pitch;
            Sprint = sprint;
            Crouch = crouch;
            Fire = fire;
            FireOrigin = fireOrigin;
            FireDirection = fireDirection;
        }
    }
}
