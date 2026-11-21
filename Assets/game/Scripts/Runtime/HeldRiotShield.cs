using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 盾の武器を持っている間に持ち主の前へ出す盾。弾を止めるのは BulletShield で、これはその作り方と、持ち主の体との当たりの外し方を持つ。
    [DisallowMultipleComponent]
    public sealed class HeldRiotShield : MonoBehaviour
    {
        public const string PlaceholderName = "RiotShield";

        public const float PlaceholderForward = RiotShieldSpec.HeldForward;

        public const float PlaceholderThickness = RiotShieldSpec.HeldThickness;

        private BulletShield _plate;
        private Collider[] _holderColliders = Array.Empty<Collider>();

        public BulletShield Plate => _plate;

        public RiotShieldSpec Spec { get; private set; }

        public string WeaponId { get; private set; } = string.Empty;

        public bool IsRaised => _plate != null && _plate.IsRaised;

        public static HeldRiotShield Create(Transform holder, string weaponId, RiotShieldSpec spec, GameObject prefab)
        {
            if (holder == null)
            {
                throw new ArgumentNullException(nameof(holder));
            }

            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            var plate = CreatePlate(holder, spec, prefab);
            plate.BlockArcDegrees = spec.BlockArcDegrees;
            SetLayer(plate.gameObject, BulletLayerOf(holder));

            var held = plate.gameObject.AddComponent<HeldRiotShield>();
            held._plate = plate;
            held.Spec = spec;
            held.WeaponId = weaponId ?? string.Empty;
            held._holderColliders = holder.GetComponentsInChildren<Collider>(true);
            held.IgnoreHolder();
            return held;
        }

        // 走っている間は盾を下げ、弾を止めない。下げると当たり判定を切るので、上げ直したら持ち主の体との当たりを外し直す。
        public bool SetRaised(bool raised)
        {
            if (_plate == null || _plate.IsRaised == raised)
            {
                return false;
            }

            _plate.SetRaised(raised);
            if (raised)
            {
                IgnoreHolder();
            }

            return true;
        }

        // 構えたままでも、置き直しや復活で持ち主の CharacterController が切られて戻ると IgnoreCollision の設定が消えるので、呼び手が毎回かけ直す。
        public void ReapplyIgnoredCollisions()
        {
            if (IsRaised)
            {
                IgnoreHolder();
            }
        }

        // 使えないプレハブで盾の無い武器にすると撃てないうえ弾も止まらないので、誤りは出したうえで仮の板で続ける。
        private static BulletShield CreatePlate(Transform holder, RiotShieldSpec spec, GameObject prefab)
        {
            if (prefab != null)
            {
                try
                {
                    return BulletShield.Create(holder, prefab);
                }
                catch (ArgumentException e)
                {
                    Debug.LogError($"{ShieldLog.LogTag} {e.Message}; using the placeholder riot shield", holder);
                }
            }

            var placeholder = BulletShield.CreatePlaceholder(
                holder,
                new Vector3(0f, 0f, PlaceholderForward),
                new Vector3(spec.Width, spec.Height, PlaceholderThickness));
            placeholder.name = PlaceholderName;
            return placeholder;
        }

        // 持ち主に弾が当たる層 (Hitbox の層) に置く。持ち主の体と違う層に置くと、持ち主を撃てる弾が盾には当たらず素通りする。
        private static int BulletLayerOf(Transform holder)
        {
            var hitbox = holder.GetComponentInChildren<Hitbox>(true);
            return hitbox != null ? hitbox.gameObject.layer : holder.gameObject.layer;
        }

        private static void SetLayer(GameObject root, int layer)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = layer;
            }
        }

        // 盾は持ち主の子なので、外さないと CharacterController が自分の盾に押されて前へ進めない。
        private void IgnoreHolder()
        {
            var own = _plate != null ? _plate.Collider : null;
            if (own == null)
            {
                return;
            }

            foreach (var other in _holderColliders)
            {
                if (other != null && other != own && other.enabled)
                {
                    Physics.IgnoreCollision(own, other, true);
                }
            }
        }
    }
}
