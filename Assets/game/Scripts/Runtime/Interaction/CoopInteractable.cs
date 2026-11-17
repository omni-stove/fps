using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Fps.Runtime
{
    /// <summary>
    /// トリガーの Collider の範囲の中で、決まった人数が同時に Interact を長押ししている間だけ進み、
    /// 決まった秒数に届いたら <see cref="Completed"/> を出す。何が起きるか (扉が開くなど) は使う側がイベントで決める。
    /// </summary>
    /// <remarks>
    /// 押している人は 2 通りで数える。
    /// 範囲に入った <see cref="PlayerTag"/> の Collider は、この端末の Interact (<see cref="InputSystem.actions"/> の
    /// <see cref="InteractActionPath"/>) が押されている間だけ押しているとみなす。
    /// Bot など入力を経ない者は <see cref="BeginHold"/>・<see cref="EndHold"/> で押す・離すを伝え、範囲の外に出たら離したことにする。
    /// 範囲はこの GameObject の isTrigger の Collider で、<see cref="Collider.ClosestPoint"/> が使える形 (箱・球・カプセル・凸のメッシュ) に限る。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class CoopInteractable : MonoBehaviour
    {
        public const string LogTag = CoopHoldLog.Tag;

        /// <summary>Assets/InputSystem_Actions.inputactions の Interact アクション。</summary>
        public const string InteractActionPath = InputActionPaths.Interact;

        /// <summary>GameBootstrap がプレイヤーに付ける Unity 組み込みのタグ。</summary>
        public const string DefaultPlayerTag = ObjectiveAreaBase.DefaultPlayerTag;

        // 足元の位置が床に置いたトリガーの下の面にちょうど乗っても、浮動小数の誤差で外と判定しない程度の幅。
        private const float RangeToleranceMeters = 0.05f;

        [SerializeField, Tooltip("この物の値のアセット。配線すると下の人数・秒数・欠けたときに戻すか (と、これで開く扉の開き方) の代わりにこれを読む。空なら下の値を使う")]
        private CoopInteractableDefinition _definition;

        [SerializeField, Min(1), Tooltip("同時に押している必要のある人数")]
        private int _requiredCount = CoopHoldSpec.DefaultRequiredCount;

        [SerializeField, Min(0f), Tooltip("揃って押し続ける秒数")]
        private float _holdSeconds = CoopHoldSpec.DefaultHoldSeconds;

        [SerializeField, Tooltip("人数が欠けたときに経過を 0 に戻すか。外すと経過を残して止める")]
        private bool _resetOnInterrupt = CoopHoldSpec.DefaultResetOnInterrupt;

        [SerializeField, Tooltip("この端末の入力で押すプレイヤーとみなす Collider のタグ")]
        private string _playerTag = DefaultPlayerTag;

        private static readonly List<Collider> _colliderBuffer = new List<Collider>();

        private readonly HashSet<Collider> _playersInside = new HashSet<Collider>();
        private readonly HashSet<GameObject> _directHolders = new HashSet<GameObject>();
        private readonly HashSet<GameObject> _holders = new HashSet<GameObject>();
        private readonly List<Collider> _staleColliders = new List<Collider>();
        private readonly List<GameObject> _staleHolders = new List<GameObject>();

        private CoopHold _hold;
        // シーンに置いたまま再コンパイルすると、Unity が private の値も保存し直し、null の InputAction を空のアクションとして戻してしまう (再生でシーンを読み直さないので、そのまま使われる)。
        [NonSerialized]
        private InputAction _interactAction;
        private bool _enabledInteractAction;
        private int _loggedHoldingCount;

        /// <summary>済んだとき (押し続ける秒数に届いたとき) に 1 度出す。<see cref="ResetHold"/> すればまた出る。</summary>
        public event Action<CoopInteractable> Completed;

        /// <summary>
        /// 値のアセット。null なら Inspector の値 (既定は <see cref="CoopHoldSpec"/> と <see cref="CoopDoor"/> の Default* の値) を使う。
        /// 差し替えは <see cref="Configure(CoopInteractableDefinition)"/> で行う。
        /// </summary>
        public CoopInteractableDefinition Definition => _definition;

        /// <summary>今の設定。<see cref="Configure(CoopHoldSpec)"/> で差し替える。<see cref="Definition"/> か Inspector の値が不正なら例外を投げる。</summary>
        public CoopHoldSpec Spec => Hold.Spec;

        public int RequiredCount => Hold.Spec.RequiredCount;

        public float HoldSeconds => Hold.Spec.HoldSeconds;

        /// <summary>直前の <see cref="Tick"/> で押していた人数。</summary>
        public int HoldingCount => Hold.HoldingCount;

        /// <summary>進み具合 (0 から 1)。</summary>
        public float Progress => Hold.Progress;

        /// <summary>人数が揃って進んでいる間は true。</summary>
        public bool IsRunning => Hold.IsRunning;

        public bool IsCompleted => Hold.IsCompleted;

        public string PlayerTag
        {
            get => _playerTag;
            set => _playerTag = value;
        }

        /// <summary>読んでいる Interact アクション。未解決なら null。</summary>
        public InputAction InteractAction => _interactAction;

        private CoopHold Hold => _hold ??= new CoopHold(CreateSpec());

        private void Start()
        {
            if (!HasTriggerCollider())
            {
                Debug.LogError($"{LogTag} '{name}' に isTrigger の Collider がありません。", this);
                enabled = false;
                return;
            }

            try
            {
                _ = Hold;
            }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException)
            {
                Debug.LogError($"{LogTag} '{name}' の設定が不正です: {e.Message}", this);
                enabled = false;
                return;
            }

            // 入力が無くても Bot は押せるので、止めずに続ける。
            if (TryResolveAction())
            {
                EnableAction();
            }
        }

        private void OnEnable()
        {
            // Start より前の最初の OnEnable ではまだ解決していないので、Start で有効にする。
            if (_interactAction != null)
            {
                EnableAction();
            }
        }

        private void OnDisable()
        {
            DisableAction();
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        private void OnTriggerEnter(Collider other)
        {
            HandleEnter(other);
        }

        private void OnTriggerExit(Collider other)
        {
            HandleExit(other);
        }

        /// <summary>
        /// 設定を差し替え、進みを最初からにする。ゲームのデータから人数や秒数を上書きするときに使う。
        /// </summary>
        public void Configure(CoopHoldSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            _requiredCount = spec.RequiredCount;
            _holdSeconds = spec.HoldSeconds;
            _resetOnInterrupt = spec.ResetOnInterrupt;
            _hold = new CoopHold(spec);
            _loggedHoldingCount = 0;
        }

        /// <summary>
        /// definition を <see cref="Definition"/> に入れ、その人数と秒数で <see cref="Configure(CoopHoldSpec)"/> する。
        /// これで開く扉 (<see cref="CoopDoor"/>) は、次に開くときから definition の開き方を使う。
        /// </summary>
        /// <exception cref="ArgumentNullException">definition が null。</exception>
        /// <exception cref="InvalidOperationException">definition の値が不正。今の設定はそのまま残る。</exception>
        public void Configure(CoopInteractableDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            var spec = definition.ToSpec();
            definition.ValidateDoor();
            _definition = definition;
            Configure(spec);
        }

        /// <summary>経過と完了を消して、もう一度使えるようにする。押している人はそのまま。</summary>
        public void ResetHold()
        {
            Hold.Reset();
            _loggedHoldingCount = 0;
            Debug.Log($"{LogTag} '{name}' reset", this);
        }

        /// <summary>Collider が範囲に入った。<see cref="PlayerTag"/> の Collider なら覚える。</summary>
        public void HandleEnter(Collider other)
        {
            if (other != null && !string.IsNullOrEmpty(_playerTag) && other.CompareTag(_playerTag))
            {
                _playersInside.Add(other);
            }
        }

        /// <summary>Collider が範囲から出た。</summary>
        public void HandleExit(Collider other)
        {
            if (other != null)
            {
                _playersInside.Remove(other);
            }
        }

        /// <summary>
        /// 入力を経ずに holder が押し始めたことにする (Bot 用)。holder が範囲の中にいれば覚えて true を返し、
        /// <see cref="EndHold"/> するか範囲から出るまで押しているとみなす。済んだあとや範囲の外なら false。
        /// </summary>
        public bool BeginHold(GameObject holder)
        {
            if (holder == null)
            {
                throw new ArgumentNullException(nameof(holder));
            }

            if (Hold.IsCompleted)
            {
                return false;
            }

            if (!IsInRange(holder.transform.position))
            {
                Debug.Log($"{LogTag} '{name}' hold refused holder={holder.name} reason=out_of_range", this);
                return false;
            }

            if (_directHolders.Add(holder))
            {
                Debug.Log($"{LogTag} '{name}' hold begin holder={holder.name}", this);
            }

            return true;
        }

        /// <summary>入力を経ずに holder が離したことにする (Bot 用)。押していたら true。</summary>
        public bool EndHold(GameObject holder)
        {
            if (holder == null || !_directHolders.Remove(holder))
            {
                return false;
            }

            Debug.Log($"{LogTag} '{name}' hold end holder={holder.name}", this);
            return true;
        }

        /// <summary><see cref="BeginHold"/> で押していて、まだ離していなければ true。</summary>
        public bool IsHeldBy(GameObject holder)
        {
            return holder != null && _directHolders.Contains(holder);
        }

        /// <summary>point が範囲 (この GameObject の有効な isTrigger の Collider のどれか) の中なら true。</summary>
        public bool IsInRange(Vector3 point)
        {
            GetComponents(_colliderBuffer);
            try
            {
                foreach (var collider in _colliderBuffer)
                {
                    if (collider.isTrigger
                        && collider.enabled
                        && (collider.ClosestPoint(point) - point).sqrMagnitude <= RangeToleranceMeters * RangeToleranceMeters)
                    {
                        return true;
                    }
                }

                return false;
            }
            finally
            {
                _colliderBuffer.Clear();
            }
        }

        /// <summary>
        /// 今押している人を数えて deltaSeconds だけ進める (Update から呼ぶ)。人数が変わったとき・進み始めたとき・
        /// 止まったとき・済んだときに <see cref="LogTag"/> 付きのログを出し、済んだら <see cref="Completed"/> を出す。
        /// </summary>
        public void Tick(float deltaSeconds)
        {
            var hold = Hold;
            if (hold.IsCompleted)
            {
                return;
            }

            CollectHolders();
            var count = _holders.Count;
            if (count != _loggedHoldingCount)
            {
                _loggedHoldingCount = count;
                Debug.Log($"{LogTag} '{name}' holding {count}/{hold.Spec.RequiredCount}", this);
            }

            switch (hold.Tick(count, deltaSeconds))
            {
                case CoopHoldStep.Started:
                    Debug.Log($"{LogTag} '{name}' started {CoopHoldLog.Describe(hold)}", this);
                    break;
                case CoopHoldStep.Interrupted:
                    Debug.Log($"{LogTag} '{name}' interrupted {CoopHoldLog.Describe(hold)}", this);
                    break;
                case CoopHoldStep.Completed:
                    Debug.Log($"{LogTag} '{name}' completed {CoopHoldLog.Describe(hold)}", this);
                    _directHolders.Clear();
                    Completed?.Invoke(this);
                    break;
            }
        }

        private void CollectHolders()
        {
            _holders.Clear();

            // 無効にしたり壊したりした Collider には OnTriggerExit が来ないので、ここで外す。
            foreach (var collider in _playersInside)
            {
                if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
                {
                    _staleColliders.Add(collider);
                }
            }

            foreach (var collider in _staleColliders)
            {
                _playersInside.Remove(collider);
            }

            _staleColliders.Clear();

            if (HeldInput.IsHeld(_interactAction))
            {
                foreach (var collider in _playersInside)
                {
                    _holders.Add(HolderOf(collider));
                }
            }

            foreach (var holder in _directHolders)
            {
                if (holder == null || !holder.activeInHierarchy || !IsInRange(holder.transform.position))
                {
                    _staleHolders.Add(holder);
                }
            }

            foreach (var holder in _staleHolders)
            {
                _directHolders.Remove(holder);
                var holderName = holder == null ? "-" : holder.name;
                Debug.Log($"{LogTag} '{name}' hold end holder={holderName} reason=left", this);
            }

            _staleHolders.Clear();
            _holders.UnionWith(_directHolders);
        }

        // 1 人が複数の Collider を持っていても 1 人と数えるため、Rigidbody があればその持ち主でまとめる。
        private static GameObject HolderOf(Collider collider)
        {
            return collider.attachedRigidbody != null ? collider.attachedRigidbody.gameObject : collider.gameObject;
        }

        private CoopHoldSpec CreateSpec()
        {
            return _definition != null
                ? _definition.ToSpec()
                : new CoopHoldSpec(_requiredCount, _holdSeconds, _resetOnInterrupt);
        }

        private bool HasTriggerCollider()
        {
            foreach (var collider in GetComponents<Collider>())
            {
                if (collider.isTrigger)
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryResolveAction()
        {
            var actions = InputSystem.actions;
            if (actions == null)
            {
                Debug.LogWarning(
                    $"{LogTag} プロジェクト全体の Input Actions が設定されていません (Project Settings > Input System Package)。入力では押せません。",
                    this);
                return false;
            }

            var action = actions.FindAction(InteractActionPath);
            if (action == null)
            {
                Debug.LogWarning($"{LogTag} Input Actions '{actions.name}' に '{InteractActionPath}' がありません。入力では押せません。", this);
                return false;
            }

            _interactAction = action;
            return true;
        }

        private void EnableAction()
        {
            // プロジェクト全体のアクションは他の部品と共有なので、自分が有効にしたときだけ後で無効に戻す。
            if (!_interactAction.enabled)
            {
                _interactAction.Enable();
                _enabledInteractAction = true;
            }
        }

        private void DisableAction()
        {
            if (_interactAction != null && _enabledInteractAction)
            {
                _interactAction.Disable();
            }

            _enabledInteractAction = false;
        }
    }
}
