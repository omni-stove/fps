using System;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 二人で使う物 (<see cref="CoopInteractable"/>) の使い方の例。済んだら扉の板 (<see cref="Panel"/>) を
    /// <see cref="OpenOffset"/> だけ <see cref="OpenSeconds"/> かけて動かし、通れるようにする。
    /// 板は自分の Collider ごと動くので、通れるかどうかは板の位置だけで決まる。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CoopDoor : MonoBehaviour
    {
        public const string LogTag = CoopInteractable.LogTag;

        /// <summary>板を真上に引き上げる。</summary>
        public static readonly Vector3 DefaultOpenOffset = new Vector3(0f, 3f, 0f);

        public const float DefaultOpenSeconds = 1f;

        [SerializeField, Tooltip("済んだら開く二人で使う物。空なら同じ GameObject のものを使う")]
        private CoopInteractable _interactable;

        [SerializeField, Tooltip("動かす扉の板。開いたときに通れるよう、範囲のトリガーとは別の GameObject にする")]
        private Transform _panel;

        [SerializeField, Tooltip("開いたときに板を動かす量 (板の親の座標)")]
        private Vector3 _openOffset = DefaultOpenOffset;

        [SerializeField, Min(0f), Tooltip("開き切るまでの秒数。0 ならすぐ開く")]
        private float _openSeconds = DefaultOpenSeconds;

        private CoopInteractable _bound;
        private Vector3 _closedPosition;
        private float _openElapsed;

        /// <summary>済んだら開く二人で使う物。差し替えたら <see cref="Bind"/> し直す。</summary>
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

        /// <summary>開き始めたら true (開き切る前も含む)。</summary>
        public bool IsOpening { get; private set; }

        /// <summary>開き切ったら true。</summary>
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

        /// <summary>
        /// <see cref="Interactable"/> (空なら同じ GameObject のもの) の <see cref="CoopInteractable.Completed"/> を受けるようにする。
        /// 二人で使う物か板が無ければ理由を Debug.LogError に出して false を返す。
        /// </summary>
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

            _bound = _interactable;
            _bound.Completed += OnCompleted;
            return true;
        }

        /// <summary>
        /// 開き始める。既に開き始めていれば何もせず false を返す。<see cref="OpenSeconds"/> が 0 ならすぐ開き切る。
        /// </summary>
        public bool Open()
        {
            if (IsOpening || _panel == null)
            {
                return false;
            }

            IsOpening = true;
            _closedPosition = _panel.localPosition;
            _openElapsed = 0f;
            Debug.Log($"{LogTag} door '{name}' opening", this);

            if (_openSeconds <= 0f)
            {
                Finish();
            }

            return true;
        }

        /// <summary>開いている途中なら deltaSeconds だけ板を動かす (Update から呼ぶ)。</summary>
        public void Advance(float deltaSeconds)
        {
            if (!IsOpening || IsOpen || _panel == null)
            {
                return;
            }

            _openElapsed += Mathf.Max(0f, deltaSeconds);
            if (_openElapsed >= _openSeconds)
            {
                Finish();
                return;
            }

            _panel.localPosition = _closedPosition + _openOffset * (_openElapsed / _openSeconds);
        }

        private void Finish()
        {
            _panel.localPosition = _closedPosition + _openOffset;
            IsOpen = true;
            Debug.Log($"{LogTag} door '{name}' opened", this);
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
