using Fps.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Fps.Runtime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyAgent))]
    // EnemyAgent の Update で判断 (ChargerEnemyBehavior) が進んだ後に、同じフレームで体を動かして当たりを見る。
    [DefaultExecutionOrder(10)]
    public sealed class EnemyCharger : MonoBehaviour
    {
        public const string TellPlaceholderName = "ChargeTell";

        public const string CancelInterrupt = "interrupt";
        public const string CancelKnockback = "knockback";
        public const string CancelLostSight = "lost-sight";

        // 壁に触れる手前で止めるための余白。0 だと、1 フレームで進む分だけ壁にめり込んでから止まる。
        public const float ObstacleSkin = 0.1f;

        // NavMesh の端で Move が切り詰められたとみなす割合。浮動小数の誤差で少し短くなっただけでは止めない。
        public const float BlockedProgressRatio = 0.5f;

        // これより上を向いた面は床や坂として通る。突進の線を胴の高さで引くので、低い段差に引っかからないように。
        public const float WalkableNormalY = 0.7f;

        public const float FallbackBodyRadius = 0.5f;

        [SerializeField, Tooltip("構えと突進の間だけ見せる目印。シーン生成スクリプトが配線する。空なら実行時に仮の板を作る")]
        private Transform _tell;

        [SerializeField, Tooltip("突進を止める壁や障害物を探すレイヤー")]
        private LayerMask _obstacleMask = Physics.DefaultRaycastLayers;

        private EnemyAgent _agent;
        private NavMeshAgent _navAgent;
        private ChargerEnemyBehavior _behavior;
        private EnemyBrain _attachedBrain;
        private string _cancelReason;
        private float _travelled;
        private bool _active;

        public ChargerEnemyBehavior Behavior => EnsureBehavior();

        public Transform Tell
        {
            get => _tell;
            set => _tell = value;
        }

        public LayerMask ObstacleMask
        {
            get => _obstacleMask;
            set => _obstacleMask = value;
        }

        public bool IsActive => _active;

        public bool IsAttached => _attachedBrain != null;

        public bool IsTellVisible => _tell != null && _tell.gameObject.activeSelf;

        public float Travelled => _travelled;

        private string Id => _agent != null ? _agent.Id : name;

        private void Awake()
        {
            EnsureInitialized();
            SetTell(false);
        }

        // Edit Mode のテストでは Awake が走らないので、Tick からも呼ぶ。
        private void EnsureInitialized()
        {
            if (_agent != null)
            {
                return;
            }

            _agent = GetComponent<EnemyAgent>();
            _navAgent = GetComponent<NavMeshAgent>();
            EnsureBehavior();
            if (_tell == null)
            {
                _tell = CreateTellPlaceholder(transform);
            }
        }

        private void OnDisable()
        {
            SetTell(false);
            Detach();
        }

        private void OnDestroy()
        {
            if (_behavior != null)
            {
                _behavior.PhaseChanged -= OnPhaseChanged;
            }

            Detach();
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        // 当てたフレームに true を返す。
        public bool Tick(float deltaSeconds)
        {
            EnsureInitialized();
            if (!TryAttach())
            {
                SetTell(false);
                return false;
            }

            var spec = _attachedBrain.Spec;
            if (spec.Archetype != EnemyArchetype.Charger || _attachedBrain.IsDead)
            {
                Deactivate();
                return false;
            }

            _active = true;

            // 怯みや吹き飛ばしの間は判断が進まないので、構えや突進をここでやめる。続けると、飛ばされた先から元の向きへ走り出す。
            if (_agent.IsKnockedBack)
            {
                Cancel(CancelKnockback);
            }
            else if (_agent.IsInterrupted)
            {
                Cancel(CancelInterrupt);
            }

            var hit = _behavior.IsCharging && Drive(spec.Charger, deltaSeconds);
            SetTell(_behavior.Phase == ChargerPhase.WindingUp || _behavior.Phase == ChargerPhase.Charging);
            return hit;
        }

        public static Transform CreateTellPlaceholder(Transform parent)
        {
            var tell = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tell.name = TellPlaceholderName;

            // 目印が弾や視線や突進の線を遮らないよう、当たり判定は持たせない。
            var collider = tell.GetComponent<Collider>();
            if (collider != null)
            {
                DestroyImmediate(collider);
            }

            if (parent != null)
            {
                tell.transform.SetParent(parent, false);
            }

            tell.transform.localPosition = new Vector3(0f, 0.55f, 0.45f);
            tell.transform.localScale = new Vector3(0.7f, 0.15f, 0.1f);
            tell.SetActive(false);
            return tell.transform;
        }

        private bool Drive(ChargerSpec spec, float deltaSeconds)
        {
            var heading = _behavior.ChargeDirection;
            if (!heading.HasValue)
            {
                return false;
            }

            var direction = heading.Value.ToUnity();
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-6f)
            {
                return false;
            }

            direction.Normalize();
            if (TryStrike(spec, direction))
            {
                return true;
            }

            var step = spec.ChargeSpeed * deltaSeconds;
            if (!(step > 0f))
            {
                return false;
            }

            if (TryFindObstacle(direction, step, out var obstacle))
            {
                Debug.Log(ChargerLog.Blocked(Id, obstacle.name), this);
                _behavior.EndCharge(ChargeEnd.Obstacle);
                return false;
            }

            var moved = Move(direction * step);
            _travelled += moved;
            if (moved < step * BlockedProgressRatio)
            {
                _behavior.EndCharge(ChargeEnd.Edge);
                return false;
            }

            return TryStrike(spec, direction);
        }

        // 当てるのは狙っている相手だけにする。途中の味方を巻き込むと、群れで出したときに突進のたびに同士討ちになる。
        private bool TryStrike(ChargerSpec spec, Vector3 direction)
        {
            var target = _agent.Target;
            if (target == null || target.IsDead)
            {
                return false;
            }

            var center = transform.position;
            var closest = target.TryGetBounds(out var bounds) ? bounds.ClosestPoint(center) : target.transform.position;
            var offset = closest - center;
            if (offset.magnitude > spec.HitReach)
            {
                return false;
            }

            // 低い壁越しでも距離は届くので、間に壁があれば当てない。
            if (IsWallBetween(center, closest))
            {
                return false;
            }

            // 近さだけで当てると、すれ違った後ろの相手まで突進の向きへ飛ばしてしまう。
            offset.y = 0f;
            if (offset.sqrMagnitude > 1e-6f && Vector3.Dot(offset.normalized, direction) < 0f)
            {
                return false;
            }

            // 吹き飛ばしは受け手が被弾の知らせ (種類 Charge と向き) から自分で行う (Knockback.ApplyFromReport)。
            // 武器で当てたのではないので、帰属の武器は空にする (キルフィードに持っている銃を出さない)。
            using (CombatEvents.Attribute(gameObject, string.Empty))
            {
                target.TakeDamage(spec.ImpactDamage, center, direction, DamageKind.Charge);
            }

            Debug.Log(ChargerLog.Hit(Id, target.Id, spec.ImpactDamage, _travelled), this);
            _behavior.EndCharge(ChargeEnd.Hit);
            return true;
        }

        private bool IsWallBetween(Vector3 from, Vector3 to)
        {
            // 壁で止める判定 (TryFindObstacle) と同じ太さで調べる。細い線だと壁の上端より高い所を通り、止まった位置から壁越しに当ててしまう。
            var offset = to - from;
            var hits = Physics.SphereCastAll(
                from, BodyRadius(), offset.normalized, offset.magnitude, _obstacleMask, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                var collider = hit.collider;
                if (collider == null || collider.transform.IsChildOf(transform) || FindDamageable(collider) != null)
                {
                    continue;
                }

                if (hit.distance > 0f && hit.normal.y > WalkableNormalY)
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private bool TryFindObstacle(Vector3 direction, float step, out Collider obstacle)
        {
            obstacle = null;
            var nearest = float.MaxValue;
            var hits = Physics.SphereCastAll(
                transform.position, BodyRadius(), direction, step + ObstacleSkin, _obstacleMask, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                var collider = hit.collider;
                if (collider == null || collider.transform.IsChildOf(transform))
                {
                    continue;
                }

                // 人や標的は壁として扱わない。狙った相手は TryStrike が見て、ほかの人は突き抜けさせる。
                if (FindDamageable(collider) != null)
                {
                    continue;
                }

                // 始めから重なっている物 (distance が 0) は向きが取れないので、床かどうかを見ずに障害物とする。
                if (hit.distance > 0f && hit.normal.y > WalkableNormalY)
                {
                    continue;
                }

                if (hit.distance < nearest)
                {
                    nearest = hit.distance;
                    obstacle = collider;
                }
            }

            return obstacle != null;
        }

        // NavMesh の上なら NavMeshAgent.Move で動かし、端で止める。NavMesh が無いとき (Edit Mode のテスト) は Transform を直に動かす。
        private float Move(Vector3 offset)
        {
            var before = transform.position;
            if (_navAgent != null && _navAgent.enabled && _navAgent.isOnNavMesh)
            {
                _navAgent.Move(offset);
            }
            else
            {
                transform.position = before + offset;
            }

            var delta = transform.position - before;
            delta.y = 0f;
            return delta.magnitude;
        }

        private float BodyRadius()
        {
            if (_navAgent == null)
            {
                return FallbackBodyRadius;
            }

            var scale = transform.lossyScale;
            return _navAgent.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        }

        private void Cancel(string reason)
        {
            _cancelReason = reason;
            try
            {
                _behavior.Cancel();
            }
            finally
            {
                _cancelReason = null;
            }
        }

        private void Deactivate()
        {
            SetTell(false);
            if (!_active)
            {
                return;
            }

            _active = false;
            _behavior.Reset();
        }

        private void SetTell(bool visible)
        {
            if (_tell != null && _tell.gameObject.activeSelf != visible)
            {
                _tell.gameObject.SetActive(visible);
            }
        }

        private void OnPhaseChanged(ChargerPhase previous, ChargerPhase next)
        {
            Debug.Log(ChargerLog.Phase(Id, previous, next), this);
            var spec = _behavior.Spec;
            switch (next)
            {
                case ChargerPhase.WindingUp:
                    var target = _agent != null ? _agent.Target : null;
                    Debug.Log(ChargerLog.Windup(Id, target != null ? target.Id : "none", spec.WindupSeconds), this);
                    break;
                case ChargerPhase.Charging:
                    _travelled = 0f;
                    Debug.Log(ChargerLog.Charge(Id, _behavior.ChargeDirection ?? System.Numerics.Vector3.Zero, spec.ChargeDistance), this);
                    break;
                case ChargerPhase.Stunned:
                    Debug.Log(ChargerLog.Missed(Id, _behavior.LastEnd, _travelled, spec.StunSeconds), this);
                    break;
                case ChargerPhase.Ready when _behavior.LastEnd == ChargeEnd.Cancelled:
                    Debug.Log(ChargerLog.Cancelled(Id, _cancelReason ?? CancelLostSight), this);
                    break;
                case ChargerPhase.Ready when previous == ChargerPhase.Stunned || previous == ChargerPhase.Recovering:
                    Debug.Log(ChargerLog.Ready(Id), this);
                    break;
            }
        }

        // EnemyAgent は Awake で判断を作るが、同じ GameObject の Awake の順は決まらないので、使うときに付ける。
        private bool TryAttach()
        {
            var brain = _agent != null ? _agent.Brain : null;
            if (brain == null)
            {
                return false;
            }

            if (ReferenceEquals(brain, _attachedBrain))
            {
                return true;
            }

            Detach();
            brain.AddBehavior(EnsureBehavior());
            brain.StateChanged += OnBrainStateChanged;
            _attachedBrain = brain;
            return true;
        }

        private void Detach()
        {
            if (_attachedBrain == null)
            {
                return;
            }

            _attachedBrain.RemoveBehavior(_behavior);
            _attachedBrain.StateChanged -= OnBrainStateChanged;
            _attachedBrain = null;
        }

        // 倒れて生き返ったら、前の命の構えや突進を持ち越さない。
        private void OnBrainStateChanged(EnemyState previous, EnemyState next)
        {
            if (next == EnemyState.Dead)
            {
                _behavior.Reset();
                SetTell(false);
            }
        }

        private ChargerEnemyBehavior EnsureBehavior()
        {
            if (_behavior == null)
            {
                _behavior = new ChargerEnemyBehavior();
                _behavior.PhaseChanged += OnPhaseChanged;
            }

            return _behavior;
        }

        private static Damageable FindDamageable(Collider collider)
        {
            var hitbox = Hitbox.FindFor(collider);
            if (hitbox != null && hitbox.Owner != null)
            {
                return hitbox.Owner;
            }

            return collider.GetComponentInParent<Damageable>();
        }
    }
}
