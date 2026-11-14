using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    // 弾は Hitscan と WeaponController が先にここで振り分ける (味方の弾は素通り、敵の弾は止めて耐久で受ける)。
    // 爆発と近接は Damageable へそのまま届くので、ここでは扱わない。
    // 敵の AI が隠れに来ないよう、CoverPoint は付けない。
    [DisallowMultipleComponent]
    public sealed class Deployable : MonoBehaviour
    {
        public const string LogTag = DeployLog.Tag;

        private GameObject _owner;
        private string _ownerId = string.Empty;
        private string _kindId = string.Empty;
        private Side? _side;
        private Damageable _damageable;
        private bool _removed;

        public event Action<Deployable, DeployRemoval> Removed;

        public event Action<Deployable, int> Blocked;

        public GameObject Owner => _owner;

        public string OwnerId => _ownerId;

        public string KindId => _kindId;

        public int Serial { get; private set; }

        public Side? Side => _side;

        public Damageable Damageable => _damageable;

        public bool IsRemoved => _removed;

        public void Initialize(GameObject owner, string ownerId, string kindId, int serial, Side? side, int maxHealth)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            if (serial < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(serial), serial, "serial must be at least 1");
            }

            if (maxHealth < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxHealth), maxHealth, "maxHealth must be at least 1");
            }

            if (_damageable != null)
            {
                throw new InvalidOperationException($"deployable '{name}' is already initialized");
            }

            _owner = owner;
            _ownerId = ownerId ?? string.Empty;
            _kindId = kindId ?? string.Empty;
            Serial = serial;
            _side = side;

            var damageable = GetComponent<Damageable>();
            if (damageable == null)
            {
                damageable = gameObject.AddComponent<Damageable>();
            }

            damageable.ReportsKills = false;
            damageable.Initialize($"{_kindId}#{serial}", maxHealth);
            damageable.Died += OnDied;
            _damageable = damageable;
        }

        public bool LetsThrough(GameObject shooter)
        {
            if (shooter == null)
            {
                return false;
            }

            var isOwner = _owner != null && shooter.transform.IsChildOf(_owner.transform);
            var member = SideMember.Of(shooter);
            return DeployPlacement.LetsThrough(isOwner, _side, member != null ? member.Side : (Side?)null);
        }

        public static bool LetsThrough(Collider collider, GameObject shooter)
        {
            var deployable = Of(collider);
            return deployable != null && deployable.LetsThrough(shooter);
        }

        public static Deployable Of(Collider collider)
        {
            if (collider == null)
            {
                throw new ArgumentNullException(nameof(collider));
            }

            var deployable = collider.GetComponentInParent<Deployable>();
            return deployable != null && !deployable._removed && deployable._damageable != null ? deployable : null;
        }

        public static bool TryStopBullet(
            Collider collider,
            int baseDamage,
            float distance,
            DamageFalloff falloff,
            Vector3? sourcePosition,
            Vector3? direction,
            GameObject shooter)
        {
            if (falloff == null)
            {
                throw new ArgumentNullException(nameof(falloff));
            }

            if (baseDamage < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(baseDamage), baseDamage, "baseDamage must not be negative");
            }

            var deployable = Of(collider);
            if (deployable == null || deployable.LetsThrough(shooter))
            {
                return false;
            }

            deployable.Absorb(baseDamage, distance, falloff, sourcePosition, direction, shooter);
            return true;
        }

        public void Remove(DeployRemoval reason)
        {
            if (_removed)
            {
                return;
            }

            _removed = true;
            Debug.Log(DeployLog.Removed(_ownerId, _kindId, Serial, reason), this);
            Removed?.Invoke(this, reason);

            // Destroy はフレームの終わりまで残り、その間の弾が消えた物に当たるので、先に切る。
            gameObject.SetActive(false);
            if (Application.isPlaying)
            {
                Destroy(gameObject);
            }
            else
            {
                DestroyImmediate(gameObject);
            }
        }

        private void Absorb(int baseDamage, float distance, DamageFalloff falloff, Vector3? sourcePosition, Vector3? direction, GameObject shooter)
        {
            var damageable = _damageable;
            var damage = DamageCalculator.Calculate(baseDamage, HitZone.Body, Mathf.Max(0f, distance), falloff, damageable.ZoneMultipliers);
            var remaining = Mathf.Max(0, damageable.Health.Current - damage);

            // 倒れると Died の中で消えて Removed のログが出るので、止めたログはダメージを渡す前に出す。
            Debug.Log(DeployLog.Blocked(_ownerId, _kindId, Serial, damage, remaining, DescribeShooter(shooter)), this);
            Blocked?.Invoke(this, damage);
            damageable.TakeDamage(damage, sourcePosition, direction, DamageKind.Bullet);
        }

        private void OnDied(Damageable _)
        {
            Remove(DeployRemoval.Destroyed);
        }

        private void OnDestroy()
        {
            if (_damageable != null)
            {
                _damageable.Died -= OnDied;
            }

            if (_removed)
            {
                return;
            }

            // シーンごと消えたときなど、Remove を通らずに消えた物も持ち主の一覧から外させる。
            _removed = true;
            Removed?.Invoke(this, DeployRemoval.Discarded);
        }

        private static string DescribeShooter(GameObject shooter)
        {
            if (shooter == null)
            {
                return null;
            }

            var damageable = shooter.GetComponentInParent<Damageable>();
            return damageable != null ? damageable.Id : shooter.name;
        }
    }
}
