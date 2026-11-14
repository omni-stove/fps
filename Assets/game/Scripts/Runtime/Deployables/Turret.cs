using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    // 撃つのはプレイヤーや敵と同じ Hitscan.Fire なので、減衰・部位・置いた物での止まり方・建物への被害は同じ規則を通る。
    // 狙う相手は毎フレームではなく SearchIntervalSeconds ごとに選び直す。候補を集めるのにシーンの Damageable をすべて見るため。
    // 弾倉とリロードは持たせない。置いた時点の弾の数 (TurretSpec.Rounds) を撃ち切るか、持続の秒数が過ぎたら消える。
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Deployable))]
    public sealed class Turret : MonoBehaviour
    {
        public const string LogTag = TurretLog.Tag;
        public const string HeadName = "Head";
        public const float SearchIntervalSeconds = 0.25f;

        private static readonly List<Turret> _armed = new List<Turret>();

        private Deployable _deployable;
        private Transform _head;
        private TurretSpec _spec;
        private WeaponSpec _weapon;
        private TurretState _state;
        private FriendlyFire _friendlyFire;
        private Damageable _target;
        private float _searchIn;
        private Damageable _heldFor;
        private int _shotIndex;

        // 敵の AI が狙う相手の候補に入れるための一覧。毎回シーンを探させないよう、撃てる状態のものだけをここに持つ。
        public static IReadOnlyList<Turret> Armed => _armed;

        public bool IsArmed => _state != null && _deployable != null && !_deployable.IsRemoved;

        public Deployable Deployable => _deployable;

        public Damageable Damageable => _deployable != null ? _deployable.Damageable : null;

        public GameObject Owner => _deployable != null ? _deployable.Owner : null;

        public Side? Side => _deployable != null ? _deployable.Side : null;

        public TurretSpec Spec => _spec;

        public WeaponSpec Weapon => _weapon;

        public TurretState State => _state;

        public FriendlyFire FriendlyFire => _friendlyFire;

        public Damageable Target => _target;

        public Transform Head => _head != null ? _head : transform;

        public Vector3 Eye => Head.position;

        public float Reach => Mathf.Min(_spec.SightRange, _weapon.Range);

        public void Arm(TurretSpec spec, WeaponSpec weapon, FriendlyFire friendlyFire)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (weapon == null)
            {
                throw new ArgumentNullException(nameof(weapon));
            }

            if (!Enum.IsDefined(typeof(FriendlyFire), friendlyFire))
            {
                throw new ArgumentOutOfRangeException(nameof(friendlyFire), friendlyFire, "friendlyFire is not a defined FriendlyFire");
            }

            if (_state != null)
            {
                throw new InvalidOperationException($"turret '{name}' is already armed");
            }

            var deployable = GetComponent<Deployable>();
            if (deployable == null || deployable.Damageable == null)
            {
                throw new InvalidOperationException($"turret '{name}' must be armed after its Deployable is initialized");
            }

            _deployable = deployable;
            _spec = spec;
            _weapon = weapon;
            _friendlyFire = friendlyFire;
            _state = new TurretState(spec, weapon.FireInterval);
            _head = FindHead(transform);
            _target = null;
            _heldFor = null;
            _searchIn = 0f;
            _shotIndex = 0;

            // Edit Mode では OnDisable / OnDestroy が呼ばれないので、消えたことは Deployable から受けて一覧から外す。
            deployable.Removed += OnRemoved;
            Register();
            Debug.Log(
                TurretLog.Armed(deployable.OwnerId, deployable.KindId, deployable.Serial, weapon.Id, spec.Rounds, spec.LifetimeSeconds, friendlyFire),
                this);
        }

        public void Tick(float deltaSeconds)
        {
            if (!IsArmed)
            {
                return;
            }

            _state.Tick(deltaSeconds);
            var over = _state.Over;
            if (over.HasValue)
            {
                _deployable.Remove(over.Value);
                return;
            }

            _searchIn -= deltaSeconds;
            if (_searchIn <= 0f || (_target != null && _target.IsDead))
            {
                _searchIn = SearchIntervalSeconds;
                Retarget();
            }

            if (_target == null)
            {
                return;
            }

            var eye = Eye;
            var toTarget = _target.AimPoint - eye;
            if (!TurnTowards(toTarget, deltaSeconds) || !_state.IsReady)
            {
                return;
            }

            TryShoot(eye, toTarget);
        }

        public bool IsAllyOf(GameObject other)
        {
            if (other == null)
            {
                return false;
            }

            var member = SideMember.Of(other);
            return TurretTargeting.IsAlly(IsOwnedBy(other), Side, member != null ? member.Side : (Side?)null);
        }

        public bool IsHostileTo(GameObject other)
        {
            if (other == null)
            {
                return false;
            }

            var member = SideMember.Of(other);
            var isBot = other.GetComponentInParent<EnemyAgent>() != null;
            return TurretTargeting.IsHostile(IsOwnedBy(other), Side, member != null ? member.Side : (Side?)null, isBot);
        }

        private bool IsOwnedBy(GameObject other)
        {
            var owner = Owner;
            return owner != null && other.transform.IsChildOf(owner.transform);
        }

        private void Retarget()
        {
            var eye = Eye;
            var reach = Reach;
            var self = Damageable;
            var candidates = new List<Damageable>();
            var options = new List<TargetOption>();
            foreach (var damageable in FindObjectsByType<Damageable>(FindObjectsInactive.Exclude))
            {
                // 置いた物どうしは撃ち合わせない。陣営も bot でもないもの (標的ダミーなど) は狙う相手にしない。
                if (damageable == self || damageable.IsDead || damageable.GetComponent<Deployable>() != null)
                {
                    continue;
                }

                var member = damageable.GetComponent<SideMember>();
                var isBot = damageable.GetComponent<EnemyAgent>() != null;
                if (member == null && !isBot)
                {
                    continue;
                }

                if (!TurretTargeting.IsHostile(IsOwnedBy(damageable.gameObject), Side, member != null ? member.Side : (Side?)null, isBot))
                {
                    continue;
                }

                if (Vector3.Distance(eye, damageable.AimPoint) > reach || !Hitscan.HasLineOfSight(eye, damageable, reach, transform))
                {
                    continue;
                }

                candidates.Add(damageable);
                options.Add(new TargetOption(true, damageable.GroundPoint.ToNumerics(), true));
            }

            var index = TurretTargeting.Nearest(transform.position.ToNumerics(), options);
            var next = index >= 0 ? candidates[index] : null;
            if (next == _target)
            {
                return;
            }

            _target = next;
            Debug.Log(TurretLog.Target(_deployable.OwnerId, _deployable.KindId, _deployable.Serial, next != null ? next.Id : null), this);
        }

        // 向き終えるまでは撃たない。上下は回さず、撃つ向きは目から相手の狙う点へそのまま引く。
        private bool TurnTowards(Vector3 toTarget, float deltaSeconds)
        {
            var flat = new Vector3(toTarget.x, 0f, toTarget.z);
            if (flat.sqrMagnitude < 1e-8f)
            {
                return true;
            }

            var head = Head;
            var desired = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
            var next = TurretTargeting.StepYaw(head.eulerAngles.y, desired, _spec.TurnDegreesPerSecond * Mathf.Max(0f, deltaSeconds));
            head.rotation = Quaternion.Euler(0f, next, 0f);
            return Mathf.Abs(TurretTargeting.DeltaDegrees(next, desired)) <= _spec.AimToleranceDegrees;
        }

        private void TryShoot(Vector3 eye, Vector3 toTarget)
        {
            var distance = toTarget.magnitude;
            if (distance > Reach)
            {
                _searchIn = 0f;
                return;
            }

            // 弾を減らす前に、射線の最初に当たる物を見る。味方なら味方撃ちのルールで控え、別の敵ならそのまま撃ち、
            // 壁など撃っても仕方の無い物なら撃たずに選び直す。
            if (distance > 1e-4f && Hitscan.TryFirstHit(eye, toTarget, distance + 0.01f, transform, out var first))
            {
                var blocker = OwnerOf(first.collider);
                if (blocker != _target)
                {
                    if (blocker != null && IsAllyOf(blocker.gameObject))
                    {
                        if (TurretTargeting.HoldsFire(_friendlyFire, true))
                        {
                            LogHeld(blocker);
                            return;
                        }
                    }
                    else if (blocker == null || !IsHostileTo(blocker.gameObject))
                    {
                        _searchIn = 0f;
                        return;
                    }
                }
            }

            _heldFor = null;
            var direction = toTarget.sqrMagnitude > 1e-8f ? toTarget : Head.forward;
            var cone = SpreadPattern.ConeDegrees(_weapon, 0f, 0f);
            if (cone > 0f)
            {
                direction = SpreadPattern.Direction(direction.ToNumerics(), cone, _deployable.Serial, _shotIndex).ToUnity();
            }

            _shotIndex = unchecked(_shotIndex + 1);
            _state.Fire();
            var target = _target;
            var result = Hitscan.Fire(eye, direction, _weapon.Range, _weapon.Damage, _weapon.Falloff, transform, _weapon.ExplosionRadius, _weapon.Id);
            var hit = result.Hitbox != null ? result.Hitbox.Owner : null;
            Debug.Log(
                TurretLog.Fired(
                    _deployable.OwnerId,
                    _deployable.KindId,
                    _deployable.Serial,
                    target != null ? target.Id : null,
                    DescribeHit(result),
                    result.Damage,
                    _state.RoundsLeft),
                this);

            if (hit != null && result.Damage > 0 && hit.IsDead)
            {
                Debug.Log(TurretLog.Killed(_deployable.OwnerId, _deployable.KindId, _deployable.Serial, hit.Id), this);
            }
        }

        // 毎フレーム出すとログが埋まるので、控える相手が変わったときだけ出す。
        private void LogHeld(Damageable ally)
        {
            if (_heldFor == ally)
            {
                return;
            }

            _heldFor = ally;
            Debug.Log(TurretLog.Held(_deployable.OwnerId, _deployable.KindId, _deployable.Serial, ally.Id, _friendlyFire), this);
        }

        private void OnRemoved(Deployable deployable, DeployRemoval reason)
        {
            deployable.Removed -= OnRemoved;
            _armed.Remove(this);
            _target = null;
        }

        private void Register()
        {
            _armed.RemoveAll(t => t == null);
            if (!_armed.Contains(this))
            {
                _armed.Add(this);
            }
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        private void OnEnable()
        {
            if (IsArmed)
            {
                Register();
            }
        }

        private void OnDisable()
        {
            _armed.Remove(this);
        }

        private void OnDestroy()
        {
            _armed.Remove(this);
        }

        private static Damageable OwnerOf(Collider collider)
        {
            var hitbox = Hitbox.FindFor(collider);
            if (hitbox != null && hitbox.Owner != null)
            {
                return hitbox.Owner;
            }

            return collider.GetComponentInParent<Damageable>();
        }

        private static string DescribeHit(HitscanResult result)
        {
            if (!result.Hit)
            {
                return null;
            }

            if (result.Hitbox != null && result.Hitbox.Owner != null)
            {
                return result.Hitbox.Owner.Id;
            }

            return result.RaycastHit.collider != null ? result.RaycastHit.collider.name : null;
        }

        private static Transform FindHead(Transform root)
        {
            var direct = root.Find(HeadName);
            if (direct != null)
            {
                return direct;
            }

            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child != root && child.name == HeadName)
                {
                    return child;
                }
            }

            return root;
        }
    }
}
