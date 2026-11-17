using System;

namespace Shooter.Core
{
    public sealed class MineState
    {
        private readonly MineSpec _spec;
        private float _elapsed;
        private bool _armedAnnounced;

        public MineState(MineSpec spec)
        {
            _spec = spec ?? throw new ArgumentNullException(nameof(spec));
        }

        public bool IsArmed => _elapsed >= _spec.ArmSeconds && !Triggered;

        public bool Triggered { get; private set; }

        public float SecondsUntilArmed => Math.Max(0f, _spec.ArmSeconds - _elapsed);

        // 有効になった Tick でだけ true を返し、知らせる音とログを 1 回にする。
        public bool Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must not be negative and must be finite");
            }

            if (Triggered)
            {
                return false;
            }

            _elapsed = Math.Min(_spec.ArmSeconds, _elapsed + deltaSeconds);
            if (_armedAnnounced || _elapsed < _spec.ArmSeconds)
            {
                return false;
            }

            _armedAnnounced = true;
            return true;
        }

        public bool TryTrigger(bool enemyInRange)
        {
            if (!enemyInRange || !IsArmed)
            {
                return false;
            }

            Triggered = true;
            return true;
        }

        // 置いた人と味方では起きない。陣営の無い射撃場では、タレットが撃つ相手と同じく bot だけを敵とみなす (陣営の無いプレイヤーや標的ダミーでは起きない)。
        public static bool IsTriggeredBy(bool isOwner, Side? mineSide, Side? otherSide, bool otherIsBot)
        {
            return TurretTargeting.IsHostile(isOwner, mineSide, otherSide, otherIsBot);
        }
    }
}
