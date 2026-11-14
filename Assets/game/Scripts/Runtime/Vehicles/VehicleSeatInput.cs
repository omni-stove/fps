using System;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 座席に座っている人の 1 フレーム分の入力。<see cref="VehicleSeat.SubmitInput"/> で渡し、<see cref="IVehicleInputSink"/> が動き方や武器に使う。
    /// プレイヤーはふだんの移動・ジャンプ・しゃがみ・ダッシュ・射撃・覗き込み・リロードのボタンから作り、敵や Bot はコードで作る。
    /// </summary>
    public readonly struct VehicleSeatInput
    {
        /// <summary>何も押していない入力。狙いは乗り物の正面を向いていない (向きが 0) ので、sink は狙いを動かさない。</summary>
        public static VehicleSeatInput Idle => default;

        /// <param name="move">移動の軸。x が右、y が前。各成分 -1 以上 1 以下。</param>
        /// <param name="vertical">上下の軸 (ジャンプで +1、しゃがみで -1 など)。-1 以上 1 以下。</param>
        /// <param name="boost">加速・ブレーキなどに使うボタン (ダッシュ)。</param>
        /// <param name="fireHeld">撃つボタンを押しているか。</param>
        /// <param name="aimHeld">覗き込みのボタンを押しているか。</param>
        /// <param name="reloadPressed">このフレームにリロードを押したか。</param>
        /// <param name="aimOrigin">狙いの始点 (ワールド)。有限値。</param>
        /// <param name="aimDirection">狙いの向き (ワールド)。有限値。0 なら狙いなし。それ以外は正規化して持つ。</param>
        public VehicleSeatInput(
            Vector2 move,
            float vertical,
            bool boost,
            bool fireHeld,
            bool aimHeld,
            bool reloadPressed,
            Vector3 aimOrigin,
            Vector3 aimDirection)
        {
            if (!(Mathf.Abs(move.x) <= 1f && Mathf.Abs(move.y) <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(move), move, "move components must be between -1 and 1");
            }

            if (!(Mathf.Abs(vertical) <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(vertical), vertical, "vertical must be between -1 and 1");
            }

            if (!IsFinite(aimOrigin))
            {
                throw new ArgumentException($"aimOrigin must be finite: {aimOrigin}", nameof(aimOrigin));
            }

            if (!IsFinite(aimDirection))
            {
                throw new ArgumentException($"aimDirection must be finite: {aimDirection}", nameof(aimDirection));
            }

            Move = move;
            Vertical = vertical;
            Boost = boost;
            FireHeld = fireHeld;
            AimHeld = aimHeld;
            ReloadPressed = reloadPressed;
            AimOrigin = aimOrigin;
            AimDirection = aimDirection.sqrMagnitude > 1e-12f ? aimDirection.normalized : Vector3.zero;
        }

        public Vector2 Move { get; }

        public float Vertical { get; }

        public bool Boost { get; }

        public bool FireHeld { get; }

        public bool AimHeld { get; }

        public bool ReloadPressed { get; }

        public Vector3 AimOrigin { get; }

        /// <summary>狙いの向き (単位ベクトル)。狙いが無ければ 0。</summary>
        public Vector3 AimDirection { get; }

        public bool HasAim => AimDirection != Vector3.zero;

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }
    }
}
