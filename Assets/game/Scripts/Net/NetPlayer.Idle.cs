using System.Globalization;
using Fps.Core;
using Fps.Runtime;
using Unity.Netcode;
using UnityEngine;

namespace Fps.Net
{
    /// <summary>
    /// <see cref="NetPlayer"/> のうち、放置の警告をサーバーから持ち主へ送り、持ち主の HUD (<see cref="INetHud.ShowIdleWarning"/>) に出す処理。
    /// いつ送るかは <see cref="NetTeamDeathmatch"/> (.Idle.cs) が決める。
    /// </summary>
    public sealed partial class NetPlayer
    {
        /// <summary>サーバーで、持ち主に「あと remainingSeconds 秒で切断する」と警告する。</summary>
        internal void ServerSendIdleWarning(float remainingSeconds)
        {
            RequireServer();
            IdleWarningRpc(remainingSeconds);
        }

        /// <summary>サーバーで、持ち主に出している放置の警告を消させる。</summary>
        internal void ServerClearIdleWarning()
        {
            RequireServer();
            IdleWarningClearedRpc();
        }

        [Rpc(SendTo.Owner, Delivery = RpcDelivery.Reliable)]
        private void IdleWarningRpc(float remainingSeconds, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != NetworkManager.ServerClientId)
            {
                return;
            }

            if (!(remainingSeconds >= 0f) || float.IsInfinity(remainingSeconds))
            {
                Debug.LogWarning("[Net] ignored an idle warning with invalid seconds " + remainingSeconds.ToString(CultureInfo.InvariantCulture), this);
                return;
            }

            Debug.Log("[Net] idle warning seconds=" + HudFormat.DisplayedSeconds(remainingSeconds).ToString(CultureInfo.InvariantCulture), this);
            if (GameUi.IsAlive(_hud))
            {
                _hud.ShowIdleWarning(remainingSeconds);
            }
        }

        [Rpc(SendTo.Owner, Delivery = RpcDelivery.Reliable)]
        private void IdleWarningClearedRpc(RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != NetworkManager.ServerClientId)
            {
                return;
            }

            Debug.Log("[Net] idle warning cleared", this);
            if (GameUi.IsAlive(_hud))
            {
                _hud.ClearIdleWarning();
            }
        }
    }
}
