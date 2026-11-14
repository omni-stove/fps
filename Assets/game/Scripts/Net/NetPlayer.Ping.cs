using Fps.Core;
using Fps.Runtime;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Fps.Net
{
    // 持ち主は照準の向きだけを送り、何に付けるかはサーバーが決める (NetTeamDeathmatch.Ping.cs)。
    // 印の表示はオフラインと同じ PingController と HUD の部品を使い、サーバーから届いた印 (NetTeamDeathmatch.SharedPings) を出させる。
    public sealed partial class NetPlayer
    {
        public const string PingActionPath = InputActionPaths.Ping;

        private InputAction _pingAction;
        private bool _enabledPingAction;
        private PingController _pingDisplay;
        private bool _addedPingDisplay;
        private SharedPingMarkers _pingDisplaySource;

        internal PingController PingDisplay => _pingDisplay;

        // 持ち主の Update から毎フレーム呼ぶ
        private void UpdatePingInput()
        {
            if (_pingAction == null || !_pingAction.WasPressedThisFrame() || GameUi.IsPauseOpen)
            {
                return;
            }

            var match = NetTeamDeathmatch.Current;
            if (!NetTeamDeathmatch.CanRequestPing(match != null && match.SyncedRules.PingEnabled, IsAlive, IsWatchingDeathCamera))
            {
                return;
            }

            var direction = AimDirection();
            if (!NetMath.IsFinite(direction) || direction.sqrMagnitude < 1e-8f)
            {
                return;
            }

            Debug.Log(PingLog.Tag + " net request", this);
            PingRpc(direction.normalized);
        }

        [Rpc(SendTo.Server, Delivery = RpcDelivery.Reliable)]
        private void PingRpc(Vector3 direction, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId)
            {
                Debug.LogWarning("[Net] ignored ping from client " + rpcParams.Receive.SenderClientId
                    + " for player owned by " + OwnerClientId);
                return;
            }

            var match = NetTeamDeathmatch.Current;
            if (match != null)
            {
                match.ServerPing(this, direction);
            }
        }

        private void ResolvePingAction()
        {
            if (Application.isBatchMode)
            {
                return;
            }

            var actions = InputSystem.actions;
            var ping = actions != null ? actions.FindAction(PingActionPath) : null;
            if (ping == null)
            {
                Debug.LogWarning("[Net] input action '" + PingActionPath + "' was not found; pinging is disabled");
                return;
            }

            _pingAction = ping;
            if (!_pingAction.enabled)
            {
                _pingAction.Enable();
                _enabledPingAction = true;
            }
        }

        private void ReleasePingAction()
        {
            if (_enabledPingAction && _pingAction != null)
            {
                _pingAction.Disable();
            }

            _enabledPingAction = false;
            _pingAction = null;
        }

        // 印を見る画面があるのは視点を持つ自分のプレイヤーだけなので、表示もそこにだけ付ける。
        private void AttachPingDisplay()
        {
            if (!_hasLocalView || _pingDisplay != null)
            {
                return;
            }

            _pingDisplay = GetComponent<PingController>();
            if (_pingDisplay == null)
            {
                _pingDisplay = gameObject.AddComponent<PingController>();
                _addedPingDisplay = true;
            }

            _pingDisplaySource = null;
            RefreshPingDisplay();
        }

        private void DetachPingDisplay()
        {
            if (_pingDisplay != null && _addedPingDisplay)
            {
                Destroy(_pingDisplay);
            }

            _pingDisplay = null;
            _addedPingDisplay = false;
            _pingDisplaySource = null;
        }

        // 試合とプレイヤーのどちらが先に Spawn されるかは決まっていないので、試合が替わるたびに付け直す。
        private void RefreshPingDisplay()
        {
            if (_pingDisplay == null)
            {
                return;
            }

            var match = NetTeamDeathmatch.Current;
            var source = match != null ? match.SharedPings : null;
            if (source == null || ReferenceEquals(source, _pingDisplaySource))
            {
                return;
            }

            _pingDisplaySource = source;
            _pingDisplay.ShowShared(source);
        }
    }
}
