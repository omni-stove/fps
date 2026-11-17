using System;
using UnityEngine;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed class CoopDoor : MonoBehaviour
    {
        public const string LogTag = CoopInteractable.LogTag;

        public static readonly Vector3 DefaultOpenOffset = new Vector3(0f, 3f, 0f);

        public const float DefaultOpenSeconds = 1f;

        [SerializeField, Tooltip("済んだら開く二人で使う物。空なら同じ GameObject のものを使う")]
        private CoopInteractable _interactable;

        [SerializeField, Tooltip("動かす扉の板。開いたときに通れるよう、範囲のトリガーとは別の GameObject にする")]
        private Transform _panel;

        [SerializeField, Tooltip("開いたときに板を動かす量 (板の親の座標)。二人で使う物に値のアセットがあればそちらを使う")]
        private Vector3 _openOffset = DefaultOpenOffset;

        [SerializeField, Min(0f), Tooltip("開き切るまでの秒数。0 ならすぐ開く。二人で使う物に値のアセットがあればそちらを使う")]
        private float _openSeconds = DefaultOpenSeconds;

        private CoopInteractable _bound;
        private Vector3 _closedPosition;
        private float _openElapsed;
        // 開いている途中でアセットや Inspector の値が変わっても板が跳ばないよう、開き始めた時の値で動かし切る。
        private Vector3 _activeOffset;
        private float _activeSeconds;

        public CoopInteractable Interactable
        {
            get => _interactable;
            set => _interactable = value;
        }

        public Transform Panel
        {
            get => _panel;
            set => _panel = value;
        }

        public Vector3 OpenOffset
        {
            get => _openOffset;
            set => _openOffset = value;
        }

        public float OpenSeconds
        {
            get => _openSeconds;
            set
            {
                if (!(value >= 0f) || float.IsInfinity(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "OpenSeconds must not be negative and must be finite");
                }

                _openSeconds = value;
            }
        }

        public bool IsOpening { get; private set; }

        public bool IsOpen { get; private set; }

        private void Start()
        {
            if (!Bind())
            {
                enabled = false;
            }
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private void Update()
        {
            Advance(Time.deltaTime);
        }

        public bool Bind()
        {
            Unbind();

            if (_interactable == null)
            {
                _interactable = GetComponent<CoopInteractable>();
            }

            if (_interactable == null)
            {
                Debug.LogError($"{LogTag} 扉 '{name}' に開く合図を出す CoopInteractable がありません。Interactable を配線してください。", this);
                return false;
            }

            if (_panel == null)
            {
                Debug.LogError($"{LogTag} 扉 '{name}' に動かす板がありません。Panel を配線してください。", this);
                return false;
            }

            if (!TryResolveOpening(_interactable, out _, out _))
            {
                return false;
            }

            _bound = _interactable;
            _bound.Completed += OnCompleted;
            return true;
        }

        public bool Open()
        {
            if (IsOpening || _panel == null)
            {
                return false;
            }

            if (!TryResolveOpening(_bound != null ? _bound : _interactable, out var offset, out var seconds))
            {
                return false;
            }

            IsOpening = true;
            _closedPosition = _panel.localPosition;
            _openElapsed = 0f;
            _activeOffset = offset;
            _activeSeconds = seconds;
            Debug.Log($"{LogTag} door '{name}' opening", this);

            if (seconds <= 0f)
            {
                Finish();
            }

            return true;
        }

        public void Advance(float deltaSeconds)
        {
            if (!IsOpening || IsOpen || _panel == null)
            {
                return;
            }

            _openElapsed += Mathf.Max(0f, deltaSeconds);
            if (_openElapsed >= _activeSeconds)
            {
                Finish();
                return;
            }

            _panel.localPosition = _closedPosition + _activeOffset * (_openElapsed / _activeSeconds);
        }

        private void Finish()
        {
            _panel.localPosition = _closedPosition + _activeOffset;
            IsOpen = true;
            Debug.Log($"{LogTag} door '{name}' opened", this);
        }

        private bool TryResolveOpening(CoopInteractable interactable, out Vector3 offset, out float seconds)
        {
            var definition = interactable != null ? interactable.Definition : null;
            if (definition == null)
            {
                offset = _openOffset;
                seconds = _openSeconds;
                return true;
            }

            try
            {
                definition.ValidateDoor();
            }
            catch (InvalidOperationException e)
            {
                Debug.LogError($"{LogTag} 扉 '{name}' の開き方が不正です: {e.Message}", this);
                offset = default;
                seconds = 0f;
                return false;
            }

            offset = definition.OpenOffset;
            seconds = definition.OpenSeconds;
            return true;
        }

        private void Unbind()
        {
            if (_bound != null)
            {
                _bound.Completed -= OnCompleted;
            }

            _bound = null;
        }

        private void OnCompleted(CoopInteractable interactable)
        {
            Open();
        }
    }
}
