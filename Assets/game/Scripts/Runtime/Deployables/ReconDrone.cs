using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 操縦の入力を読んで手放させるのは DroneControl (持ち主の側) の役目で、ここは飛ぶこと・見回して印を付けること・電池と距離で落ちることだけを持つ。
    // 撃たれて落ちるのは置ける物の土台 (Deployable) が受け持つ。
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Deployable))]
    public sealed class ReconDrone : MonoBehaviour
    {
        public const string LogTag = DroneLog.Tag;

        // 壁に触れたまま押し付けて、次の移動で壁の中から始めないよう、手前で止める距離 (m)。
        public const float WallSkin = 0.02f;

        public const float MinCollisionRadius = 0.05f;

        private const int MaxHits = 32;

        private static readonly List<ReconDrone> _armed = new List<ReconDrone>();
        private static readonly RaycastHit[] _hits = new RaycastHit[MaxHits];

        private readonly HashSet<Damageable> _spotted = new HashSet<Damageable>();

        private Deployable _deployable;
        private DroneSpec _spec;
        private DroneState _state;
        private Vector3 _launch;
        private float _collisionRadius = MinCollisionRadius;

        public event Action<ReconDrone> Changed;

        // 持ち主ごとに今飛んでいるドローンを探せるよう、飛んでいる間のものだけを置いた順に持つ。
        public static IReadOnlyList<ReconDrone> Armed => _armed;

        public bool IsArmed => _state != null && _deployable != null && !_deployable.IsRemoved;

        public Deployable Deployable => _deployable;

        public Damageable Damageable => _deployable != null ? _deployable.Damageable : null;

        public GameObject Owner => _deployable != null ? _deployable.Owner : null;

        public DroneSpec Spec => _spec;

        public DroneState State => _state;

        public Vector3 Launch => _launch;

        public float CollisionRadius => _collisionRadius;

        public int SpottedCount => _state != null ? _state.SpottedCount : 0;

        public bool IsPiloted { get; internal set; }

        public DroneStatus? Status => IsArmed ? _state.Status(IsPiloted) : (DroneStatus?)null;

        public void Arm(DroneSpec spec, float collisionRadius)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (float.IsNaN(collisionRadius) || float.IsInfinity(collisionRadius) || collisionRadius < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(collisionRadius), collisionRadius, "collisionRadius must not be negative and must be finite");
            }

            if (_state != null)
            {
                throw new InvalidOperationException($"drone '{name}' is already armed");
            }

            var deployable = GetComponent<Deployable>();
            if (deployable == null || deployable.Damageable == null)
            {
                throw new InvalidOperationException($"drone '{name}' must be armed after its Deployable is initialized");
            }

            _deployable = deployable;
            _spec = spec;
            _state = new DroneState(spec);
            _launch = transform.position;
            _collisionRadius = Mathf.Max(MinCollisionRadius, collisionRadius);

            // Edit Mode では OnDisable / OnDestroy が呼ばれないので、消えたことは Deployable から受けて一覧から外す。
            deployable.Removed += OnRemoved;
            Register();
            Debug.Log(DroneLog.Armed(deployable.OwnerId, deployable.KindId, deployable.Serial, spec), this);
        }

        public static bool TryFindFor(GameObject owner, out ReconDrone drone)
        {
            for (var i = _armed.Count - 1; i >= 0; i--)
            {
                var candidate = _armed[i];
                if (candidate != null && candidate.IsArmed && candidate.Owner == owner)
                {
                    drone = candidate;
                    return true;
                }
            }

            drone = null;
            return false;
        }

        public void Tick(float deltaSeconds)
        {
            if (!IsArmed)
            {
                return;
            }

            if (_state.Tick(deltaSeconds, Vector3.Distance(transform.position, _launch)) > 0)
            {
                Sweep();
            }

            var over = _state.Over;
            if (over.HasValue)
            {
                _deployable.Remove(over.Value);
                return;
            }

            Changed?.Invoke(this);
        }

        // direction はドローンの向きでなく世界の向きで、長さ 1 までを速さの割合として受ける。
        public void Fly(Vector3 direction, float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must not be negative and must be finite");
            }

            if (!IsArmed)
            {
                return;
            }

            var move = Vector3.ClampMagnitude(direction, 1f) * (_spec.Speed * deltaSeconds);
            var distance = move.magnitude;
            if (distance <= 0f)
            {
                return;
            }

            var heading = move / distance;
            var allowed = Mathf.Min(distance, FreeDistance(heading, distance));
            if (allowed <= 0f)
            {
                return;
            }

            transform.position += heading * allowed;
        }

        public void Turn(float yawDegrees)
        {
            if (!IsArmed || float.IsNaN(yawDegrees) || float.IsInfinity(yawDegrees) || yawDegrees == 0f)
            {
                return;
            }

            transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y + yawDegrees, 0f);
        }

        public bool Recall()
        {
            if (!IsArmed)
            {
                return false;
            }

            Debug.Log(
                DroneLog.Recalled(_deployable.OwnerId, _deployable.KindId, _deployable.Serial, transform.position.ToNumerics()),
                this);
            _deployable.Remove(DeployRemoval.Recalled);
            return true;
        }

        public static int RecallAll(IReadOnlyList<Deployable> placed)
        {
            if (placed == null)
            {
                throw new ArgumentNullException(nameof(placed));
            }

            var recalled = 0;
            foreach (var deployable in placed)
            {
                var drone = deployable != null ? deployable.GetComponent<ReconDrone>() : null;
                if (drone != null && drone.Recall())
                {
                    recalled++;
                }
            }

            return recalled;
        }

        public bool HasSpotted(Damageable target)
        {
            return target != null && _spotted.Contains(target);
        }

        // 見つけ続けている敵も見回しのたびに印を延ばし、離れた敵は印の秒数が切れて消えるのに任せる。
        public int Sweep()
        {
            if (!IsArmed)
            {
                return 0;
            }

            var owner = _deployable.Owner;
            if (owner == null)
            {
                return 0;
            }

            var pings = owner.GetComponent<PingController>();
            var center = transform.position;
            var radius = _spec.SpotRadius;
            var seen = 0;
            var fresh = 0;

            // 印を断られても回り続けるのは、印の規則が切ってあってもミニマップには載せるため (UavReward と同じ)。
            var marking = pings != null;
            foreach (var candidate in FindObjectsByType<Damageable>(FindObjectsInactive.Exclude))
            {
                if (candidate == null || !UavReward.IsEnemyOf(owner, candidate))
                {
                    continue;
                }

                if ((candidate.AimPoint - center).sqrMagnitude > radius * radius)
                {
                    continue;
                }

                seen++;
                MinimapReveals.Reveal(candidate, _spec.RevealSeconds);
                var marked = false;
                if (marking)
                {
                    marked = pings.TryMarkEnemy(candidate, _spec.RevealSeconds, out _);
                    if (!marked && pings.LastRefusal != PingRefusal.NoTarget)
                    {
                        marking = false;
                    }
                }

                // 見回しのたびに同じ敵のログで埋まらないよう、初めて見つけたときだけ出す。
                if (_spotted.Add(candidate))
                {
                    fresh++;
                    Debug.Log(
                        DroneLog.Spotted(
                            _deployable.OwnerId, _deployable.KindId, _deployable.Serial, candidate.Id, candidate.GroundPoint.ToNumerics(), marked),
                        this);
                }
            }

            _state.CountSpotted(fresh);
            return seen;
        }

        private float FreeDistance(Vector3 heading, float distance)
        {
            var origin = transform.position + (Vector3.up * _collisionRadius);
            var count = Physics.SphereCastNonAlloc(
                origin, _collisionRadius, heading, _hits, distance + WallSkin, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            var free = distance;
            var owner = _deployable.Owner;
            for (var i = 0; i < count; i++)
            {
                var hit = _hits[i];
                var hitTransform = hit.collider.transform;

                // 始めから重なっている物 (置いた床) は距離 0 で返るので、止める相手にしない。
                if (hit.distance <= 0f || hitTransform.IsChildOf(transform) || (owner != null && hitTransform.IsChildOf(owner.transform)))
                {
                    continue;
                }

                free = Mathf.Min(free, hit.distance - WallSkin);
            }

            return Mathf.Max(0f, free);
        }

        private void OnRemoved(Deployable deployable, DeployRemoval reason)
        {
            deployable.Removed -= OnRemoved;
            _armed.Remove(this);
        }

        private void Register()
        {
            _armed.RemoveAll(d => d == null);
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
    }
}
