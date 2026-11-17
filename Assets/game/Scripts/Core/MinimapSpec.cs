using System;

namespace Shooter.Core
{
    /// <summary>ミニマップとコンパスの調整値。<see cref="MinimapJson"/> の形で <c>Resources/Tuning/minimap.json</c> から読む。</summary>
    public sealed class MinimapSpec
    {
        public const float DefaultRadius = 50f;
        public const float DefaultScale = 2f;
        public const float DefaultUpdateIntervalSeconds = 0.1f;
        public const float DefaultSpottedSeconds = 3f;
        public const bool DefaultRotateWithPlayer = true;

        public static MinimapSpec Default { get; } = new MinimapSpec();

        /// <summary>自分から何メートルまでを地図に映すか。外のものは縁に寄せる。</summary>
        public float Radius { get; }

        /// <summary>ワールドの 1 メートルを地図の何単位にするか。</summary>
        public float Scale { get; }

        public float UpdateIntervalSeconds { get; }

        /// <summary>UAV などで見つけた敵を、見つけてから何秒載せるか。</summary>
        public float SpottedSeconds { get; }

        /// <summary>true なら自分の向きを上に、false なら北 (+Z) を上にする。</summary>
        public bool RotateWithPlayer { get; }

        /// <summary>地図の縁までの単位数 (<see cref="Radius"/> × <see cref="Scale"/>)。</summary>
        public float MapRadius => Radius * Scale;

        /// <exception cref="ArgumentOutOfRangeException">数が正の有限の値でないとき。</exception>
        public MinimapSpec(
            float radius = DefaultRadius,
            float scale = DefaultScale,
            float updateIntervalSeconds = DefaultUpdateIntervalSeconds,
            float spottedSeconds = DefaultSpottedSeconds,
            bool rotateWithPlayer = DefaultRotateWithPlayer)
        {
            RequireInRange(radius, nameof(radius));
            RequireInRange(scale, nameof(scale));
            RequireInRange(updateIntervalSeconds, nameof(updateIntervalSeconds));
            RequireInRange(spottedSeconds, nameof(spottedSeconds));

            Radius = radius;
            Scale = scale;
            UpdateIntervalSeconds = updateIntervalSeconds;
            SpottedSeconds = spottedSeconds;
            RotateWithPlayer = rotateWithPlayer;
        }

        /// <summary>数の項目が受け付ける値か (正で有限)。</summary>
        public static bool IsInRange(float value)
        {
            return value > 0f && !float.IsInfinity(value);
        }

        public override string ToString()
        {
            return FormattableString.Invariant(
                $"radius={Radius:0.##} scale={Scale:0.##} update={UpdateIntervalSeconds:0.###}s spotted={SpottedSeconds:0.##}s rotate={(RotateWithPlayer ? "true" : "false")}");
        }

        private static void RequireInRange(float value, string name)
        {
            if (!IsInRange(value))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be positive and finite");
            }
        }
    }
}
