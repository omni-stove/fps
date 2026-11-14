using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    // EnemyBrain の判断そのものは変えず、知覚 (IPerceptionContributor) と振る舞い (IEnemyBehavior) の差し込み口から効かせる。
    // ルールが Off の敵は判断を何も変えないことを、差し込まないことで保つ。
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyAgent))]
    [RequireComponent(typeof(SuppressionReceiver))]
    public sealed class EnemySuppression : MonoBehaviour
    {
        // 波で後から出る敵にも同じルールを付けるため、最後に ConfigureAll されたルールを覚えておく。
        private static Suppression _sceneRule = Suppression.Off;
        private static SuppressionSpec _sceneSpec = SuppressionSpec.Default;

        private readonly SuppressedCoverBehavior _behavior = new SuppressedCoverBehavior();
        private EnemyAgent _agent;
        private SuppressionReceiver _receiver;
        private SuppressionPerception _perception;
        private EnemyBrain _brainWithBehavior;
        private bool _loggedHolding;

        public EnemyAgent Agent => _agent != null ? _agent : (_agent = GetComponent<EnemyAgent>());

        public SuppressionReceiver Receiver => _receiver != null ? _receiver : (_receiver = GetComponent<SuppressionReceiver>());

        public SuppressedCoverBehavior Behavior => _behavior;

        public SuppressionPerception Perception => _perception;

        public bool IsOn => Receiver != null && Receiver.Rule == Suppression.On;

        // 振る舞いは判断が進んだ tick にしか呼ばれず、倒れた後は前の値が残るので、今も TakeCover かを合わせて見る。
        public bool IsHoldingCover => _behavior.IsHolding && Agent != null && Agent.State == EnemyState.TakeCover;

        // Off では敵に部品を増やさず、もう付いているものだけを Off にする。
        /// <exception cref="ArgumentNullException">spec が null。</exception>
        public static void ConfigureAll(Suppression rule, SuppressionSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            _sceneRule = rule;
            _sceneSpec = spec;
            EnemyAgent.Initialized -= OnEnemyInitialized;
            if (rule == Suppression.On)
            {
                EnemyAgent.Initialized += OnEnemyInitialized;
                foreach (var agent in FindObjectsByType<EnemyAgent>(FindObjectsInactive.Include))
                {
                    PlayerActions.GetOrAdd<EnemySuppression>(agent.gameObject).Configure(rule, spec);
                }

                return;
            }

            foreach (var existing in FindObjectsByType<EnemySuppression>(FindObjectsInactive.Include))
            {
                existing.Configure(rule, spec);
            }
        }

        /// <exception cref="ArgumentNullException">spec が null。</exception>
        public void Configure(Suppression rule, SuppressionSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            Receiver.Configure(rule, spec);
            enabled = rule == Suppression.On;

            // Edit Mode では enabled を変えても OnEnable / OnDisable が走らないので、差し込みはここでも揃える。
            Sync();
        }

        private static void OnEnemyInitialized(EnemyAgent agent)
        {
            if (_sceneRule != Suppression.On || agent == null)
            {
                return;
            }

            PlayerActions.GetOrAdd<EnemySuppression>(agent.gameObject).Configure(_sceneRule, _sceneSpec);
        }

        private void OnEnable()
        {
            Sync();
        }

        private void Start()
        {
            // 敵の Awake より先に Configure された (無効な敵に付けた) ときは、ここで初期化済みの判断に差し込む。
            Sync();
        }

        private void OnDisable()
        {
            Detach();
            LogHolding(false);
        }

        private void OnDestroy()
        {
            Detach();
        }

        private void LateUpdate()
        {
            Sync();
            LogHolding(IsHoldingCover);
        }

        private void Sync()
        {
            if (enabled && IsOn)
            {
                Attach();
            }
            else
            {
                Detach();
            }
        }

        private void Attach()
        {
            var agent = Agent;
            var receiver = Receiver;
            if (agent == null || receiver == null)
            {
                return;
            }

            // 調整値を替えると receiver は度合いの計算を作り直すので、知覚もそれを読むものに替える。
            if (_perception == null || !ReferenceEquals(_perception.Meter, receiver.Meter))
            {
                if (_perception != null)
                {
                    agent.RemovePerceptionContributor(_perception);
                }

                _perception = new SuppressionPerception(receiver.Meter);
            }

            agent.AddPerceptionContributor(_perception);

            // 判断は初期化 (TryInitialize) まで無いので、できた後の Sync で差し込む。
            var brain = agent.Brain;
            if (brain == null || ReferenceEquals(brain, _brainWithBehavior))
            {
                return;
            }

            DetachBehavior();
            if (!Contains(brain, _behavior))
            {
                brain.AddBehavior(_behavior);
            }

            _brainWithBehavior = brain;
        }

        private void Detach()
        {
            if (_perception != null && Agent != null)
            {
                Agent.RemovePerceptionContributor(_perception);
            }

            DetachBehavior();
        }

        private void DetachBehavior()
        {
            if (_brainWithBehavior != null)
            {
                _brainWithBehavior.RemoveBehavior(_behavior);
                _brainWithBehavior = null;
            }
        }

        private void LogHolding(bool holding)
        {
            if (holding == _loggedHolding)
            {
                return;
            }

            _loggedHolding = holding;
            var agent = Agent;
            Debug.Log(SuppressionLog.HoldCover(agent != null ? agent.Id : name, holding), this);
        }

        private static bool Contains(EnemyBrain brain, IEnemyBehavior behavior)
        {
            foreach (var existing in brain.Behaviors)
            {
                if (ReferenceEquals(existing, behavior))
                {
                    return true;
                }
            }

            return false;
        }

        // ドメインの再読み込みを切った Play で、前の Play のルールが残らないようにする。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSceneRule()
        {
            _sceneRule = Suppression.Off;
            _sceneSpec = SuppressionSpec.Default;
        }
    }
}
