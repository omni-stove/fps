using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [CreateAssetMenu(fileName = "Pickup", menuName = "Shooter/Pickup")]
    public sealed class PickupDefinition : ScriptableObject
    {
        [SerializeField, Tooltip("中身")]
        private PickupContents _contents = PickupContents.ForHealth(PickupContents.DefaultHealthAmount);

        [SerializeField, Tooltip("拾い方")]
        private PickupCollectMode _collectMode = PickupCollectMode.Touch;

        [SerializeField, Min(0f), Tooltip("拾われてから再び出るまでの秒数。0 なら一度きり")]
        private float _respawnSeconds = PickupSpawnPoint.DefaultRespawnSeconds;

        [SerializeField, Min(Pickup.MinRadius), Tooltip("拾える範囲の半径 (m)")]
        private float _radius = Pickup.DefaultRadius;

        [SerializeField, Range(0f, Pickup.MaxLookAngleDegrees), Tooltip("Look で、照準の向きからこの角度 (度) 以内に見た目が見えていれば照準が合っているとする。0 なら見た目に照準の線が当たったときだけ")]
        private float _lookAngleDegrees = Pickup.DefaultLookAngleDegrees;

        // 拾う物の範囲 (_radius) とは既定も使われ方も違うので、弾薬箱には別の欄を持つ。
        [SerializeField, Min(AmmoCrate.MinRadius), Tooltip("弾薬箱 (AmmoCrate) に入れたとき、使える範囲の半径 (m)。足元から球が立つ")]
        private float _ammoCrateRadius = AmmoCrate.DefaultRadius;

        [SerializeField, Min(0f), Tooltip("弾薬箱 (AmmoCrate) に入れたとき、補給してから次に補給できるまでの秒数")]
        private float _ammoCrateCooldownSeconds = AmmoCrate.DefaultCooldownSeconds;

        public PickupContents Contents
        {
            get => _contents;
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                value.ToSpec();
                _contents = value;
            }
        }

        public PickupCollectMode CollectMode
        {
            get => _collectMode;
            set => _collectMode = value;
        }

        public float RespawnSeconds
        {
            get => _respawnSeconds;
            set => _respawnSeconds = value;
        }

        public float Radius
        {
            get => _radius;
            set => _radius = value;
        }

        public float LookAngleDegrees
        {
            get => _lookAngleDegrees;
            set => _lookAngleDegrees = value;
        }

        public float AmmoCrateRadius
        {
            get => _ammoCrateRadius;
            set
            {
                if (!AmmoCrate.IsValidRadius(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, $"ammo crate radius must be at least {AmmoCrate.MinRadius} and finite");
                }

                _ammoCrateRadius = value;
            }
        }

        public float AmmoCrateCooldownSeconds
        {
            get => _ammoCrateCooldownSeconds;
            set
            {
                if (!AmmoCrate.IsValidCooldown(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "ammo crate cooldown must be non-negative and finite");
                }

                _ammoCrateCooldownSeconds = value;
            }
        }

        // 中身は弾薬箱では使わないので見ない。
        public void ValidateAmmoCrate()
        {
            if (!AmmoCrate.IsValidRadius(_ammoCrateRadius))
            {
                throw new ArgumentException(
                    $"pickup definition '{SourceName}' ammo crate radius {_ammoCrateRadius} must be a finite number of at least {AmmoCrate.MinRadius}");
            }

            if (!AmmoCrate.IsValidCooldown(_ammoCrateCooldownSeconds))
            {
                throw new ArgumentException(
                    $"pickup definition '{SourceName}' ammo crate cooldown {_ammoCrateCooldownSeconds} must be a non-negative finite number");
            }
        }

        public PickupSpec ToSpec()
        {
            if (_contents == null)
            {
                throw new ArgumentException($"pickup definition '{SourceName}' has no contents");
            }

            try
            {
                return _contents.ToSpec();
            }
            catch (ArgumentException e)
            {
                throw new ArgumentException($"pickup definition '{SourceName}' has invalid contents: {e.Message}", e);
            }
        }

        public PickupRespawn CreateRespawn()
        {
            try
            {
                return new PickupRespawn(_respawnSeconds);
            }
            catch (ArgumentException e)
            {
                throw new ArgumentException($"pickup definition '{SourceName}' has an invalid respawn time: {e.Message}", e);
            }
        }

        public void Validate()
        {
            ToSpec();
            CreateRespawn();
            if (!(_radius >= Pickup.MinRadius) || float.IsInfinity(_radius))
            {
                throw new ArgumentException($"pickup definition '{SourceName}' radius {_radius} must be a finite number of at least {Pickup.MinRadius}");
            }

            if (!(_lookAngleDegrees >= 0f) || _lookAngleDegrees > Pickup.MaxLookAngleDegrees)
            {
                throw new ArgumentException(
                    $"pickup definition '{SourceName}' look angle {_lookAngleDegrees} must be between 0 and {Pickup.MaxLookAngleDegrees}");
            }
        }

        private string SourceName => string.IsNullOrEmpty(name) ? "(unnamed pickup definition)" : name;
    }
}
