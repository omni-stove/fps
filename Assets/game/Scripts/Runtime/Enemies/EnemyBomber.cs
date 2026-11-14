using System.Collections.Generic;
using Fps.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Fps.Runtime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyAgent))]
    // EnemyAgent の Update で信管が進んだ後に、同じフレームで爆発させる。
    [DefaultExecutionOrder(10)]
    public sealed class EnemyBomber : MonoBehaviour
    {
        public const string SignalPlaceholderName = "BomberSignal";
        public const float DefaultBlinkSeconds = 0.15f;

        [SerializeField, Tooltip("合図の間だけ点滅させる目印。空なら実行時に合図の見た目のプレハブから作る")]
        private Transform _signal;

        [SerializeField, Tooltip("合図の目印の見た目。持ち主の子に置き、プレハブの位置・向き・大きさをそのまま使う。当たり判定は外す。空なら仮の球を作る")]
        private GameObject _signalPrefab;

        [SerializeField, Min(0.01f), Tooltip("合図の目印の点滅の間隔 (秒)")]
        private float _blinkSeconds = DefaultBlinkSeconds;

        private readonly List<Damageable> _targets = new List<Damageable>();
        private EnemyAgent _agent;
        private Damageable _self;
        private BomberEnemyBehavior _behavior;
        private EnemyBrain _attachedBrain;
        private float _blinkElapsed;
        private bool _active;

        public BomberEnemyBehavior Behavior => EnsureBehavior();

        public Transform Signal
        {
            get => _signal;
            set => _signal = value;
        }

        public GameObject SignalPrefab
        {
            get => _signalPrefab;
            set => _signalPrefab = value;
        }

        public float BlinkSeconds
        {
            get => _blinkSeconds;
            set => _blinkSeconds = Mathf.Max(0.01f, value);
        }

        public bool IsActive => _active;

        public bool IsAttached => _attachedBrain != null;

        public bool IsSignalVisible => _signal != null && _signal.gameObject.activeSelf;

        private string Id => _agent != null ? _agent.Id : name;

        private void Awake()
        {
            EnsureInitialized();
            SetSignal(false);
        }

        // Edit Mode のテストでは Awake が走らないので、Tick からも呼ぶ。
        private void EnsureInitialized()
        {
            if (_agent != null)
            {
                return;
            }

            _agent = GetComponent<EnemyAgent>();
            _self = GetComponent<Damageable>();
            if (_self != null)
            {
                _self.Died += OnDied;
            }

            EnsureBehavior();
            if (_signal == null)
            {
                _signal = CreateSignal(transform, _signalPrefab);
            }
        }

        private void OnDisable()
        {
            SetSignal(false);
            Detach();
        }

        private void OnDestroy()
        {
            if (_self != null)
            {
                _self.Died -= OnDied;
            }

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

        // 信管で爆発したフレームに true を返す。
        public bool Tick(float deltaSeconds)
        {
            EnsureInitialized();
            if (!TryAttach())
            {
                SetSignal(false);
                return false;
            }

            var spec = _attachedBrain.Spec;
            if (spec.Archetype != EnemyArchetype.Bomber || _attachedBrain.IsDead)
            {
                Deactivate();
                return false;
            }

            _active = true;
            _agent.SprintAllowed = false;
            _agent.GaitSpeedMultiplier = spec.Bomber.RunSpeedMultiplier;

            // 怯みや吹き飛ばしの間は判断が進まないので、信管だけをここで進める。
            if (_agent.IsKnockedBack || _agent.IsInterrupted)
            {
                _behavior.AdvanceFuse(deltaSeconds);
            }

            if (_behavior.IsFuseBurnt)
            {
                Detonate(spec.Bomber.Blast, BomberLog.CauseFuse, attributeToSelf: true);
                return true;
            }

            UpdateSignal(deltaSeconds);
            return false;
        }

        public static Transform CreateSignal(Transform parent, GameObject prefab)
        {
            if (prefab == null)
            {
                return CreateSignalPlaceholder(parent);
            }

            var signal = EnemyVisualPrefab.Instantiate(prefab, parent);
            signal.SetActive(false);
            return signal.transform;
        }

        public static Transform CreateSignalPlaceholder(Transform parent)
        {
            var signal = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            signal.name = SignalPlaceholderName;

            // 目印が弾や視線や爆発の線を遮らないよう、当たり判定は持たせない。
            var collider = signal.GetComponent<Collider>();
            if (collider != null)
            {
                DestroyImmediate(collider);
            }

            var renderer = signal.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }

            if (parent != null)
            {
                signal.transform.SetParent(parent, false);
            }

            signal.transform.localPosition = new Vector3(0f, 1.15f, 0f);
            signal.transform.localScale = Vector3.one * 0.35f;
            signal.SetActive(false);
            return signal.transform;
        }

        private void UpdateSignal(float deltaSeconds)
        {
            if (_behavior.Phase != BomberPhase.Fusing)
            {
                _blinkElapsed = 0f;
                SetSignal(false);
                return;
            }

            _blinkElapsed += deltaSeconds;
            var on = ((int)(_blinkElapsed / _blinkSeconds) % 2) == 0;
            SetSignal(on);
        }

        // 倒された時点で大小を決める。合図の前なら小さく、合図を始めた後なら火の付いた爆弾として本来の大きさで爆発する。
        private void OnDied(Damageable self)
        {
            // 最初の Update より前に倒されても爆発させるため、ここでも付ける。
            if (!enabled || !TryAttach() || _attachedBrain.Spec.Archetype != EnemyArchetype.Bomber)
            {
                return;
            }

            var phase = _behavior.Phase;
            if (phase == BomberPhase.Exploded)
            {
                return;
            }

            var spec = _attachedBrain.Spec.Bomber;
            if (phase == BomberPhase.Fusing)
            {
                Detonate(spec.Blast, BomberLog.CauseKilledArmed, attributeToSelf: false);
            }
            else
            {
                Detonate(spec.DeathBlast, BomberLog.CauseKilled, attributeToSelf: false);
            }
        }

        // 倒されて爆発したときは帰属をそのまま (倒した者) にする。撃ち倒して周りを巻き込んだのを、倒した者の手柄として数えるため。
        private void Detonate(ExplosionDamage blast, string cause, bool attributeToSelf)
        {
            if (_behavior.Detonate() == BomberPhase.Exploded)
            {
                return;
            }

            SetSignal(false);
            var center = transform.position;
            var damaged = 0;
            var killed = 0;
            using (attributeToSelf ? CombatEvents.Attribute(gameObject, string.Empty) : CombatEvents.Attribute(CombatEvents.CurrentAttribution))
            {
                CollectTargets(center, blast.Radius);
                foreach (var target in _targets)
                {
                    if (TryDamage(target, center, blast, out var killedTarget))
                    {
                        damaged++;
                        if (killedTarget)
                        {
                            killed++;
                        }
                    }
                }
            }

            _targets.Clear();
            Debug.Log(BomberLog.Exploded(Id, cause, blast.Radius, damaged, killed), this);
            ThrownGrenade.PlayExplosionEffects(center);
            CombatEvents.RaiseExploded(center, string.Empty);

            // 自分の爆発で倒れるのは自滅として出す。帰属を付けると、キルフィードに自分が自分を倒した行が出る。
            if (_self != null && !_self.IsDead)
            {
                using (CombatEvents.Attribute(null, string.Empty))
                {
                    _self.Kill();
                }
            }
        }

        private bool TryDamage(Damageable target, Vector3 center, ExplosionDamage blast, out bool killedTarget)
        {
            killedTarget = false;
            if (target == null || target == _self || target.IsDead)
            {
                return false;
            }

            var distance = ThrownGrenade.DistanceTo(center, target);
            if (!blast.Reaches(distance))
            {
                return false;
            }

            var damage = blast.At(distance, ThrownGrenade.IsObstructed(center, target));
            if (damage <= 0)
            {
                return false;
            }

            // 倒れると体の当たり判定が隠され狙う点が動くので、向きはダメージの前に取る。
            var push = target.AimPoint - center;
            target.TakeDamage(damage, center, push, DamageKind.Explosion);
            killedTarget = target.IsDead;
            Knockback.Apply(target, DamageKind.Explosion, push, blast.StrengthAt(distance));
            Debug.Log(BomberLog.Hit(Id, target.Id, damage, distance), target);
            return true;
        }

        private void CollectTargets(Vector3 center, float radius)
        {
            _targets.Clear();
            foreach (var collider in Physics.OverlapSphere(center, radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (collider == null)
                {
                    continue;
                }

                var hitbox = Hitbox.FindFor(collider);
                var damageable = hitbox != null && hitbox.Owner != null ? hitbox.Owner : collider.GetComponentInParent<Damageable>();
                if (damageable != null && !_targets.Contains(damageable))
                {
                    _targets.Add(damageable);
                }
            }

            // OverlapSphere の並びは決まっていないので、ログと倒れる順が毎回同じになるよう Id で並べる。
            _targets.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
        }

        private void Deactivate()
        {
            SetSignal(false);
            if (!_active)
            {
                return;
            }

            _active = false;
            _agent.SprintAllowed = true;
            _agent.GaitSpeedMultiplier = 1f;
        }

        private void SetSignal(bool visible)
        {
            if (_signal != null && _signal.gameObject.activeSelf != visible)
            {
                _signal.gameObject.SetActive(visible);
            }
        }

        private void OnPhaseChanged(BomberPhase previous, BomberPhase next)
        {
            Debug.Log(BomberLog.Phase(Id, previous, next), this);
            if (next == BomberPhase.Fusing)
            {
                var target = _agent != null ? _agent.Target : null;
                Debug.Log(BomberLog.Fuse(Id, target != null ? target.Id : "none", _behavior.Spec.FuseSeconds), this);
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

        // 倒れた時の Phase は爆発の大小を決めるのに要るので、倒れた時ではなく生き返った時に初めからにする。
        private void OnBrainStateChanged(EnemyState previous, EnemyState next)
        {
            if (previous == EnemyState.Dead && next != EnemyState.Dead)
            {
                _behavior.Reset();
                _blinkElapsed = 0f;
            }
        }

        private BomberEnemyBehavior EnsureBehavior()
        {
            if (_behavior == null)
            {
                _behavior = new BomberEnemyBehavior();
                _behavior.PhaseChanged += OnPhaseChanged;
            }

            return _behavior;
        }
    }
}
