using System;
using System.Numerics;

namespace Shooter.Core
{
    public sealed class DodgeMotion
    {
        private Vector2 _direction;
        private float _elapsed;
        private float _cooldownRemaining;
        private bool _isRolling;

        public DodgeMotion(MotorSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            Distance = spec.DodgeDistance;
            Seconds = spec.DodgeSeconds;
            CooldownSeconds = spec.DodgeCooldownSeconds;
        }

        public float Distance { get; }

        public float Seconds { get; }

        public float CooldownSeconds { get; }

        public bool IsRolling => _isRolling;

        public bool IsReady => !_isRolling && _cooldownRemaining <= 0f;

        public Vector2 Direction => _direction;

        public float Elapsed => _isRolling ? _elapsed : 0f;

        public float CooldownRemaining => _cooldownRemaining;

        public Vector2 Velocity => _isRolling ? _direction * (Distance / Seconds) : Vector2.Zero;

        // 入力の大きさを向きにだけ使うのは、スティックを少し倒しただけでも同じだけ転がるようにするため。
        public static Vector2 ResolveDirection(Vector2 moveInput, float yawRadians)
        {
            if (!IsFinite(moveInput.X) || !IsFinite(moveInput.Y))
            {
                throw new ArgumentOutOfRangeException(nameof(moveInput), moveInput, "moveInput must be finite");
            }

            if (!IsFinite(yawRadians))
            {
                throw new ArgumentOutOfRangeException(nameof(yawRadians), yawRadians, "yawRadians must be finite");
            }

            // スティックの遊び程度の入力で向きが暴れないよう、ほぼ 0 は無入力として後ろへ転がす。
            var local = moveInput.LengthSquared() > 1e-4f ? Vector2.Normalize(moveInput) : new Vector2(0f, -1f);
            return Vector2.Normalize(PlayerMotor.ToWorld(local, yawRadians));
        }

        public bool TryStart(Vector2 direction)
        {
            if (!IsFinite(direction.X) || !IsFinite(direction.Y) || direction.LengthSquared() <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(direction), direction, "direction must be finite and not zero");
            }

            if (!IsReady)
            {
                return false;
            }

            _direction = Vector2.Normalize(direction);
            _elapsed = 0f;
            _isRolling = true;
            return true;
        }

        // 終わる Step で余った時間を待ち時間に回すのは、動く距離と待ちの合計をフレームの刻みによらずそろえるため。
        public Vector2 Step(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be finite and not negative");
            }

            if (!_isRolling)
            {
                _cooldownRemaining = Math.Max(0f, _cooldownRemaining - deltaSeconds);
                return Vector2.Zero;
            }

            var before = _elapsed;
            var after = Math.Min(Seconds, before + deltaSeconds);
            _elapsed = after;

            // 進んだ割合の差で出すと、途中の丸めが積み重ならず最後に始点からちょうど Distance の所で止まる。
            var displacement = _direction * (Distance * ((after / Seconds) - (before / Seconds)));
            if (after >= Seconds)
            {
                Finish(deltaSeconds - (after - before));
            }

            return displacement;
        }

        public bool Cancel()
        {
            if (!_isRolling)
            {
                return false;
            }

            Finish(0f);
            return true;
        }

        public DodgeSnapshot Capture()
        {
            return new DodgeSnapshot(_isRolling, _direction, Elapsed, _cooldownRemaining);
        }

        public void Restore(DodgeSnapshot snapshot)
        {
            if (!snapshot.IsValid)
            {
                throw new ArgumentOutOfRangeException(nameof(snapshot), "snapshot must be valid");
            }

            _isRolling = snapshot.IsRolling;

            // 進みが Seconds を超えたまま Step すると、差が負になって逆向きに動くため切り詰める
            _elapsed = snapshot.IsRolling ? Math.Min(Seconds, snapshot.Elapsed) : 0f;
            _cooldownRemaining = snapshot.CooldownRemaining;
            _direction = snapshot.Direction.LengthSquared() > 0f ? Vector2.Normalize(snapshot.Direction) : Vector2.Zero;
        }

        public void Reset()
        {
            _isRolling = false;
            _elapsed = 0f;
            _cooldownRemaining = 0f;
        }

        private void Finish(float leftoverSeconds)
        {
            _isRolling = false;
            _elapsed = 0f;
            _cooldownRemaining = Math.Max(0f, CooldownSeconds - leftoverSeconds);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
