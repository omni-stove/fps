using UnityEngine;
using UnityEngine.InputSystem;

namespace Shooter.Runtime
{
    // Runner の土台 (ModeRunnerBase) が Unity のメッセージ (OnEnable など) を private で持っていて派生では受けられないので、
    // 入力の購読はこの別のコンポーネントで行う。
    [DisallowMultipleComponent]
    public sealed class ExtractionInteractInput : MonoBehaviour
    {
        public const string LogTag = "[ExtractionInteractInput]";

        public const string InteractActionPath = InputActionPaths.Interact;

        [SerializeField, Tooltip("操作を渡す Runner。空なら同じ GameObject のものを使う")]
        private ExtractionRunner _runner;

        // シーンに置いたまま再コンパイルすると、Unity が private の値も保存し直し、null の InputAction を空のアクションとして戻してしまう (再生でシーンを読み直さないので、そのまま使われる)。
        [System.NonSerialized]
        private InputAction _interactAction;
        private bool _enabledInteractAction;

        public ExtractionRunner Runner
        {
            get => _runner;
            set => _runner = value;
        }

        public InputAction InteractAction => _interactAction;

        private void Start()
        {
            if (_runner == null)
            {
                _runner = GetComponent<ExtractionRunner>();
            }

            if (_runner == null)
            {
                Debug.LogError($"{LogTag} 操作を渡す ExtractionRunner がありません。Runner を配線してください。", this);
                enabled = false;
                return;
            }

            if (!TryResolveAction())
            {
                enabled = false;
                return;
            }

            _runner.InteractKeyLabel = _interactAction.GetBindingDisplayString(InputBinding.DisplayStringOptions.DontIncludeInteractions);
            Subscribe();
        }

        private void OnEnable()
        {
            // Start より前の最初の OnEnable ではまだ解決していないので、Start で購読する。
            if (_interactAction != null)
            {
                Subscribe();
            }
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private bool TryResolveAction()
        {
            var actions = InputSystem.actions;
            if (actions == null)
            {
                Debug.LogError($"{LogTag} プロジェクト全体の Input Actions が設定されていません (Project Settings > Input System Package)。", this);
                return false;
            }

            var action = actions.FindAction(InteractActionPath);
            if (action == null)
            {
                Debug.LogError($"{LogTag} Input Actions '{actions.name}' に '{InteractActionPath}' がありません。", this);
                return false;
            }

            _interactAction = action;
            return true;
        }

        private void Subscribe()
        {
            Unsubscribe();
            _interactAction.performed += OnInteractPerformed;

            // プロジェクト全体のアクションは他の部品と共有なので、自分が有効にしたときだけ後で無効に戻す。
            if (!_interactAction.enabled)
            {
                _interactAction.Enable();
                _enabledInteractAction = true;
            }
        }

        private void Unsubscribe()
        {
            if (_interactAction == null)
            {
                return;
            }

            _interactAction.performed -= OnInteractPerformed;
            if (_enabledInteractAction)
            {
                _interactAction.Disable();
            }

            _enabledInteractAction = false;
        }

        private void OnInteractPerformed(InputAction.CallbackContext context)
        {
            if (_runner != null && _runner.IsStarted)
            {
                _runner.TryLoot();
            }
        }
    }
}
