using UnityEngine;

namespace Fps.Net
{
    /// <summary><see cref="BotInputPlanner"/> が作る 1 tick 分の Bot の入力。人のプレイヤーと同じ移動と射撃に渡す。</summary>
    public readonly struct BotInput
    {
        /// <summary><see cref="NetworkPlayerMotor"/> に渡す移動入力。ジャンプはせず、ダウンはサーバーが入れる。</summary>
        public MotorInput Motor { get; }

        /// <summary>上下の向き (度、正が下)。目の localEulerAngles.x にそのまま入れられる。</summary>
        public float Pitch { get; }

        /// <summary>この tick に撃つか。true なら <see cref="FireOrigin"/> と <see cref="FireDirection"/> を ServerFire に渡す。</summary>
        public bool Fire { get; }

        /// <summary>射撃の始点 (目)。</summary>
        public Vector3 FireOrigin { get; }

        /// <summary>射撃の向き (長さ 1)。<see cref="MotorInput.Yaw"/> と <see cref="Pitch"/> の正面。</summary>
        public Vector3 FireDirection { get; }

        public BotInput(MotorInput motor, float pitch, bool fire, Vector3 fireOrigin, Vector3 fireDirection)
        {
            Motor = motor;
            Pitch = pitch;
            Fire = fire;
            FireOrigin = fireOrigin;
            FireDirection = fireDirection;
        }
    }
}
