using System;
using System.Numerics;

namespace Shooter.Core
{
    // Unity 抜きのサーバーは PickupSpawnPoint を読めないので、Editor がシーンの出現点から書き出した中身・位置・範囲・再出現の秒数を持つ。
    // 中身は PickupSpec で持ち、検査もオフラインと同じ PickupSpec の作り方に任せる
    public sealed class NetMapPickup
    {
        public NetMapPickup(string id, PickupSpec spec, Vector3 position, float radius, float respawnSeconds, bool touch)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("id must not be empty", nameof(id));
            }

            Spec = spec ?? throw new ArgumentNullException(nameof(spec));

            // タグは倒れた人から出す物で、モード (Kill Confirmed) が置くので出現点には置けない
            if (spec.Kind == PickupKind.Tag)
            {
                throw new ArgumentException("a pickup spawn point cannot hold a tag", nameof(spec));
            }

            if (!NetMapLootContainer.IsFinite(position))
            {
                throw new ArgumentOutOfRangeException(nameof(position), position, "position must be finite");
            }

            if (!(radius > 0f) || float.IsInfinity(radius))
            {
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "radius must be positive and finite");
            }

            if (!(respawnSeconds >= 0f) || float.IsInfinity(respawnSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(respawnSeconds), respawnSeconds, "respawnSeconds must be finite and not negative");
            }

            Id = id;
            Position = position;
            Radius = radius;
            RespawnSeconds = respawnSeconds;
            Touch = touch;
        }

        public string Id { get; }

        public PickupSpec Spec { get; }

        // 足元。オフラインの Pickup と同じく、範囲の球は足元から立つ
        public Vector3 Position { get; }

        public float Radius { get; }

        // 0 なら一度きり
        public float RespawnSeconds { get; }

        // 触れたら拾う (PickupCollectMode.Touch)。false なら拾う要求 (Interact・Look) を待つ
        public bool Touch { get; }
    }
}
