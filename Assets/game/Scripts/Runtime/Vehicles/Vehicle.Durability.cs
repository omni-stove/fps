using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed partial class Vehicle
    {
        public const string ExplosionWeaponId = VehicleSpec.ExplosionWeaponId;

        private readonly List<Collider> _blastColliders = new List<Collider>();
        private readonly Dictionary<Hitbox, VehiclePart> _partHitboxes = new Dictionary<Hitbox, VehiclePart>();
        private VehicleDurability _durability;
        private ShotAttribution? _lastAttribution;
        private bool _exploded;
        private bool _burnTicking;

        public event Action<Vehicle, VehicleDamageStage> DamageStageChanged;

        public event Action<Vehicle, GrenadeExplosion> Exploded;

        // DamageStageChanged に部位を足さず別にするのは、爆発や燃焼のように部位を通らないダメージでは段階だけが動くため。
        public event Action<Vehicle, VehiclePart, int> PartDamaged;

        public Damageable Damageable
        {
            get
            {
                EnsureInitialized();
                return _damageable;
            }
        }

        public VehicleDamageStage DamageStage => _exploded ? VehicleDamageStage.Destroyed : Durability.Stage;

        public bool HasExploded => _exploded;

        // 体力を持つ Damageable と段階を決める VehicleDurability を分けたので、乗り物を作った直後の値は定義か Inspector から取る。
        private VehicleDurability Durability
        {
            get
            {
                EnsureInitialized();
                return _durability ??= new VehicleDurability(_seats.Length > 0 ? Spec.Durability : VehicleDurabilitySpec.Default);
            }
        }

        public int TickDurability(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            EnsureInitialized();
            if (_exploded || _durability == null || _damageable == null || _damageable.IsDead)
            {
                return 0;
            }

            var damage = _durability.TickBurn(deltaSeconds);
            if (damage <= 0)
            {
                return 0;
            }

            // 燃えて爆発したときも、火を付けた者が倒したことにするため、最後の帰属のまま減らす。
            _burnTicking = true;
            try
            {
                using (CombatEvents.Attribute(_lastAttribution))
                {
                    _damageable.TakeDamage(damage, null, null, DamageKind.StatusEffect);
                }
            }
            finally
            {
                _burnTicking = false;
            }

            return damage;
        }

        private void BindDamageable()
        {
            _damageable = GetComponent<Damageable>();
            if (_damageable == null)
            {
                _damageable = gameObject.AddComponent<Damageable>();
                _damageable.MaxHealth = Mathf.Max(1, _maxHealth);
                _damageable.ResetHealth();
            }

            // 乗り物が壊れるのは人が倒れるのと違うので、キルフィードと得点に載せない。
            _damageable.ReportsKills = false;
            if (GetComponent<Hitbox>() == null)
            {
                gameObject.AddComponent<Hitbox>().Initialize(HitZone.Body, _damageable);
            }

            _damageable.Died += OnDied;
            _damageable.Damaged += OnDamaged;
            _damageable.HealthReset += OnHealthReset;
        }

        private void UnbindDamageable()
        {
            UnbindParts();
            if (_damageable == null)
            {
                return;
            }

            _damageable.Died -= OnDied;
            _damageable.Damaged -= OnDamaged;
            _damageable.HealthReset -= OnHealthReset;
        }

        private void ConfigureDurability(VehicleSpec spec)
        {
            if (_durability == null)
            {
                _durability = new VehicleDurability(spec.Durability);
            }
            else
            {
                _durability.Configure(spec.Durability);
            }

            ApplyParts(spec);
        }

        private void ApplyParts(VehicleSpec spec)
        {
            // 設定し直すたびに部位を集め直すので、前に付けた購読を外してから付け直し、1 発で 2 度知らせないようにする。
            UnbindParts();
            foreach (var part in GetComponentsInChildren<VehiclePart>(true))
            {
                // 入れ子の乗り物 (荷台に載せた別の乗り物など) の部位は、その乗り物の値で決まる。
                if (part.GetComponentInParent<Vehicle>(true) != this)
                {
                    continue;
                }

                var hitbox = part.EnsureHitbox(_damageable);
                if (!string.IsNullOrEmpty(part.PartId))
                {
                    hitbox.Hit += OnPartHit;
                    _partHitboxes[hitbox] = part;
                }

                var partSpec = string.IsNullOrEmpty(part.PartId) ? null : spec.FindPart(part.PartId);
                if (partSpec == null)
                {
                    Debug.LogWarning(VehicleLog.UnknownPart(name, part.PartId), part);
                    hitbox.DamageMultiplier = 1f;
                    continue;
                }

                hitbox.DamageMultiplier = partSpec.DamageMultiplier;
            }
        }

        private void UnbindParts()
        {
            foreach (var hitbox in _partHitboxes.Keys)
            {
                if (hitbox != null)
                {
                    hitbox.Hit -= OnPartHit;
                }
            }

            _partHitboxes.Clear();
        }

        private void OnPartHit(Hitbox hitbox, int damage)
        {
            if (_exploded || !_partHitboxes.TryGetValue(hitbox, out var part) || part == null)
            {
                return;
            }

            PartDamaged?.Invoke(this, part, damage);
        }

        private void OnDamaged(Damageable damageable, DamageReport report)
        {
            RememberAttribution();
            if (_exploded)
            {
                return;
            }

            if (!_burnTicking)
            {
                Debug.Log(VehicleLog.Damaged(name, report.Amount, damageable.Health.Current, damageable.Health.Max), this);
            }

            ObserveHealth();
        }

        private void OnHealthReset(Health _)
        {
            if (!_exploded)
            {
                ObserveHealth();
            }
        }

        private void ObserveHealth()
        {
            var health = _damageable.Health;
            if (health.IsDead || !Durability.Observe(health.Current, health.Max))
            {
                return;
            }

            Debug.Log(VehicleLog.Stage(name, _durability.Stage, health.Current, health.Max), this);
            DamageStageChanged?.Invoke(this, _durability.Stage);
        }

        private void OnDied(Damageable _)
        {
            RememberAttribution();
            Explode();
        }

        private void RememberAttribution()
        {
            var attribution = CombatEvents.CurrentAttribution;
            if (attribution.HasValue)
            {
                _lastAttribution = attribution;
            }
        }

        private void Explode()
        {
            if (_exploded)
            {
                return;
            }

            _exploded = true;
            var durability = Durability;
            durability.MarkDestroyed();
            var riders = _seating != null ? CountLiveRiders() : 0;
            var center = _damageable.AimPoint;
            var instigator = _lastAttribution.HasValue ? CombatEvents.OfflineObject(_lastAttribution.Value.KillerId) : null;

            // 車体は外からの爆発を乗っている人から遮る (Explosion.Blocks) ので、自分の爆発が中の人と周りに届くよう、その間だけ当たり判定から外す。
            // 残骸として残すので、ExplosiveProp のように消さず、爆発の後に戻す。
            GetComponentsInChildren(_blastColliders);
            _blastColliders.RemoveAll(collider => !collider.enabled);
            foreach (var collider in _blastColliders)
            {
                collider.enabled = false;
            }

            GrenadeExplosion blast;
            try
            {
                blast = Explosion.Detonate(center, durability.Spec.Explosion, instigator, ExplosionWeaponId);
            }
            finally
            {
                foreach (var collider in _blastColliders)
                {
                    if (collider != null)
                    {
                        collider.enabled = true;
                    }
                }

                _blastColliders.Clear();
                DeathBodyPresenter.ReapplyAnchoredIgnores(transform);
            }

            Debug.Log(VehicleLog.Exploded(name, center.x, center.y, center.z, riders, blast.Damaged, blast.Killed), this);
            DamageStageChanged?.Invoke(this, VehicleDamageStage.Destroyed);
            Wreck();
            Exploded?.Invoke(this, blast);
        }
    }
}
