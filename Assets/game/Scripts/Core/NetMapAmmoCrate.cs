using System;
using System.Numerics;

namespace Shooter.Core
{
    // Unity 抜きのサーバーは AmmoCrate を読めないので、Editor がシーンの弾薬箱から書き出した位置・範囲・次に使えるまでの秒数を持つ。
    public sealed class NetMapAmmoCrate
    {
        public NetMapAmmoCrate(string id, Vector3 position, float radius, float cooldownSeconds)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("id must not be empty", nameof(id));
            }

            if (!NetMapLootContainer.IsFinite(position))
            {
                throw new ArgumentOutOfRangeException(nameof(position), position, "position must be finite");
            }

            if (!(radius > 0f) || float.IsInfinity(radius))
            {
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "radius must be positive and finite");
            }

            if (!(cooldownSeconds >= 0f) || float.IsInfinity(cooldownSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(cooldownSeconds), cooldownSeconds, "cooldownSeconds must be finite and not negative");
            }

            Id = id;
            Position = position;
            Radius = radius;
            CooldownSeconds = cooldownSeconds;
        }

        public string Id { get; }

        // 底面の中心
        public Vector3 Position { get; }

        public float Radius { get; }

        // オフラインの AmmoCrate と同じく箱ごとに数える (使った人ごとではない)
        public float CooldownSeconds { get; }
    }
}
