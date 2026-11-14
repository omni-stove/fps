using UnityEngine;
using UnityEngine.InputSystem;

namespace Fps.Runtime
{
    /// <summary>
    /// Interact (キーボードは E の長押し) で <see cref="HordeRunner.TryInteract()"/> を呼び、近くの防衛物を通貨で直す・置く。
    /// 入力はプロジェクト全体のアクション (<see cref="InputSystem.actions"/>) の <see cref="InteractActionPath"/> をコードで解決する。
    /// 割り当てのキーの表示名を <see cref="HordeRunner.InteractKeyLabel"/> に入れ、HUD の案内に出させる。
    /// </summary>
    /// <remarks>
    /// Runner の土台 (<see cref="ModeRunnerBase"/>) が Unity のメッセージ (OnEnable など) を private で持っていて派生では受けられないので、
    /// 入力の購読はこの別のコンポーネントで行う。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class HordeInteractInput : MonoBehaviour
    {
        public const string LogTag = "[HordeInteractInput]";

        /// <summary>Assets/InputSystem_Actions.inputactions の Interact アクション。</summary>
        public const string InteractActionPath = InputActionPaths.Interact;

        [SerializeField, Tooltip("操作を渡す Runner。空なら同じ GameObject のものを使う")]
        private HordeRunner _runner;

        private InputAction _interactAction;
        private bool _enabledInteractAction;

        /// <summary>操作を渡す Runner。</summary>
        public HordeRunner Runner
        {
            get => _runner;
            set => _runner = value;
        }

        /// <summary>読んでいる Interact アクション。未解決なら null。</summary>
        public InputAction InteractAction => _interactAction;

        private void Start()
        {
            if (_runner == null)
            {
                _runner = GetComponent<HordeRunner>();
            }

            if (_runner == null)
            {
                Debug.LogError($"{LogTag} 操作を渡す HordeRunner がありません。Runner を配線してください。", this);
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
                _runner.TryInteract();
            }
        }
    }
}
