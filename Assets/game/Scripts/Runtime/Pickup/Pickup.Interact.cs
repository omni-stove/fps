using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shooter.Runtime
{
    public sealed partial class Pickup
    {
        private InteractHintSlot _hint;
        // シーンに置いたまま再コンパイルすると、Unity が private の値も保存し直し、null の InputAction を空のアクションとして戻してしまう (再生でシーンを読み直さないので、そのまま使われる)。
        [NonSerialized]
        private InputAction _interactAction;
        private bool _enabledInteractAction;
        private int _armedFrame = -1;

        public InputAction InteractAction => _interactAction;

        public bool Interact()
        {
            if (_collected)
            {
                return false;
            }

            foreach (var collider in SnapshotInside())
            {
                var collector = CollectorOf(collider);
                if (collector != null && collector.IsLocalPlayer)
                {
                    if (_collectMode == PickupCollectMode.Look && !IsLookedAtBy(collector))
                    {
                        return false;
                    }

                    return TryCollect(collector, out _);
                }
            }

            return false;
        }

        private void RefreshInteractAction()
        {
            if (_collectMode == PickupCollectMode.Touch || !Application.isPlaying)
            {
                ReleaseInteractAction();
                return;
            }

            if (_interactAction != null)
            {
                return;
            }

            var actions = InputSystem.actions;
            if (actions == null)
            {
                Debug.LogError($"{LogTag} プロジェクト全体の Input Actions が設定されていないため、'{name}' は Interact で拾えません (Project Settings > Input System Package)。", this);
                return;
            }

            var action = actions.FindAction(InteractActionPath);
            if (action == null)
            {
                Debug.LogError($"{LogTag} Input Actions '{actions.name}' に '{InteractActionPath}' がないため、'{name}' は Interact で拾えません。", this);
                return;
            }

            _interactAction = action;
            _interactAction.performed += OnInteractPerformed;

            // プロジェクト全体のアクションは他の部品と共有なので、自分が有効にしたときだけ後で無効に戻す。
            if (!_interactAction.enabled)
            {
                _interactAction.Enable();
                _enabledInteractAction = true;
            }
        }

        private void ReleaseInteractAction()
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
            _interactAction = null;
        }

        private void OnInteractPerformed(InputAction.CallbackContext context)
        {
            // 持ち替えで置いた物は、置いた押下の続きで拾い直さないよう、次のフレームから受け付ける。
            if (Time.frameCount == _armedFrame)
            {
                return;
            }

            Interact();
        }

        internal void UpdatePrompt()
        {
            var player = _collectMode != PickupCollectMode.Touch && !_collected && _inside.Count > 0 ? LocalPlayerInside() : null;
            if (player != null && _collectMode == PickupCollectMode.Look && !IsLookedAtBy(player))
            {
                player = null;
            }

            if (player == null || !TryDescribe(player, out var hint))
            {
                HidePrompt();
                return;
            }

            (_hint ?? (_hint = new InteractHintSlot(this))).Show(hint);
        }

        private void HidePrompt()
        {
            _hint?.Clear();
        }
    }
}
