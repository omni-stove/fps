using Shooter.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed class InteractObjective : ObjectiveAreaBase
    {
        public const string InteractActionPath = InputActionPaths.Interact;

        // シーンに置いたまま再コンパイルすると、Unity が private の値も保存し直し、null の InputAction を空のアクションとして戻してしまう (再生でシーンを読み直さないので、そのまま使われる)。
        [System.NonSerialized]
        private InputAction _interactAction;
        private bool _enabledInteractAction;

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

        protected override void OnEnable()
        {
            base.OnEnable();

            // Start より前の最初の OnEnable ではまだ解決していないので、Start で購読する。
            if (_interactAction != null)
            {
                Subscribe();
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            Unsubscribe();
            KeySharing.Release(InputActionPaths.Interact, this);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            KeySharing.Release(InputActionPaths.Interact, this);
        }

        public bool OffersInteract => Zone != null && Zone.IsOccupied && Zone.IsActive;

        private void Update()
        {
            KeySharing.SetClaim(InputActionPaths.Interact, this, OffersInteract);
        }

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
