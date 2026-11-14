using System;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// <see cref="IMovementOverride.Step"/> が返す、1 Tick 分の動き。<see cref="PlayerController"/> は次の順に当てる:
    /// <see cref="Crouched"/> で姿勢を決め、<see cref="Position"/> があればそこへ置き、<see cref="YawDegrees"/> があれば向きを変え、
    /// <see cref="Velocity"/> を <see cref="PlayerMotor"/> の速度にし、最後に <see cref="Displacement"/> だけ CharacterController で動かす (壁には当たる)。
    /// </summary>
    public readonly struct MovementOverrideStep
    {
        /// <exception cref="ArgumentException">どれかの値が有限でない。</exception>
        public MovementOverrideStep(
            Vector3 displacement,
            Vector3 velocity,
            Vector3? position = null,
            float? yawDegrees = null,
            bool? crouched = null)
        {
            RequireFinite(displacement, nameof(displacement));
            RequireFinite(velocity, nameof(velocity));
            if (position.HasValue)
            {
                RequireFinite(position.Value, nameof(position));
            }

            if (yawDegrees.HasValue && !IsFinite(yawDegrees.Value))
            {
                throw new ArgumentException("yawDegrees must be finite", nameof(yawDegrees));
            }

            Displacement = displacement;
            Velocity = velocity;
            Position = position;
            YawDegrees = yawDegrees;
            Crouched = crouched;
        }

        /// <summary>この Tick で動かす量 (m、ワールド)。CharacterController.Move に渡すので、壁や床に当たれば止まる。</summary>
        public Vector3 Displacement { get; }

        /// <summary>
        /// この Tick の終わりの速度 (m/s、ワールド)。PlayerMotor に入れるので、射撃のばらつきや落下ダメージが読み、横取りが終わった後の勢いにもなる。
        /// </summary>
        public Vector3 Velocity { get; }

        /// <summary>動かす前に置く位置 (ワールド)。壁を無視して置く (はしごに付くなど)。null なら置き直さない。</summary>
        public Vector3? Position { get; }

        /// <summary>本体の yaw (度、親に対する)。null なら向きを変えない。</summary>
        public float? YawDegrees { get; }

        /// <summary>しゃがむか。null ならしゃがみの入力どおり。立つのは頭上が空いているときだけなのは入力と同じ。</summary>
        public bool? Crouched { get; }

        /// <summary>displacement だけ動かし、速度を velocity にする。</summary>
        public static MovementOverrideStep Move(Vector3 displacement, Vector3 velocity)
        {
            return new MovementOverrideStep(displacement, velocity);
        }

        private static void RequireFinite(Vector3 value, string paramName)
        {
            if (!IsFinite(value.x) || !IsFinite(value.y) || !IsFinite(value.z))
            {
                throw new ArgumentException($"{paramName} must be finite: {value}", paramName);
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
