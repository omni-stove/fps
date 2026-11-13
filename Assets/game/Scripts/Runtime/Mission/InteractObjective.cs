using Fps.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Fps.Runtime
{
    /// <summary>
    /// プレイヤーがトリガーの Collider の中で Interact (キーボードは E の長押し) を行ったら目標を進める。
    /// 入力はプロジェクト全体のアクション (<see cref="InputSystem.actions"/>) の <see cref="InteractActionPath"/> を
    /// コードで解決するので、Inspector の配線は要らない。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InteractObjective : ObjectiveAreaBase
    {
        /// <summary>Assets/InputSystem_Actions.inputactions の Interact アクション。</summary>
        public const string InteractActionPath = InputActionPaths.Interact;

        private InputAction _interactAction;
        private bool _enabledInteractAction;

        /// <summary>読んでいる Interact アクション。未解決なら null。</summary>
        public InputAction InteractAction => _interactAction;

        protected override ObjectiveZoneMode Mode => ObjectiveZoneMode.Interact;

        protected override void Start()
        {
            base.Start();
            if (!enabled)
            {
                return;
            }

            if (!TryResolveAction())
            {
                enabled = false;
                return;
            }

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

        /// <summary>
        /// 入力を経ずに操作したことにする。範囲の中にプレイヤーがいて目標が Active なら進めて true を返す。
        /// </summary>
        public bool Interact()
        {
            if (Zone == null)
            {
                return false;
            }

            var progressed = Zone.Interact();
            if (progressed)
            {
                Debug.Log($"{MissionRunner.LogTag} interacted objective={ObjectiveId}", this);
            }

            return progressed;
        }

        private bool TryResolveAction()
        {
            var actions = InputSystem.actions;
            if (actions == null)
            {
                Debug.LogError(
                    "[InteractObjective] プロジェクト全体の Input Actions が設定されていません (Project Settings > Input System Package)。",
                    this);
                return false;
            }

            var action = actions.FindAction(InteractActionPath);
            if (action == null)
            {
                Debug.LogError($"[InteractObjective] Input Actions '{actions.name}' に '{InteractActionPath}' がありません。", this);
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
            Interact();
        }
    }
}
