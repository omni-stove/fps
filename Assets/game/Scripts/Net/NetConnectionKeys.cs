using System;
using System.Collections.Generic;
using Fps.Core;
using Unity.Netcode;
using UnityEngine;

namespace Fps.Net
{
    /// <summary>
    /// 接続の承認データで届いた、切断して戻ってきた人を見分ける鍵 (<see cref="RejoinKey"/>) をクライアント Id ごとに覚える。
    /// <see cref="NetworkSetup"/> が NetworkManager と同じ GameObject に付けて承認のコールバックにし、
    /// <see cref="NetTeamDeathmatch"/> が試合に加えるときに引く。
    /// </summary>
    /// <remarks>
    /// 承認は鍵の有無によらず通す。鍵が無い・読めない相手は、抜けたときに成績を残さないだけで遊べるようにする。
    /// クライアントが送る鍵は、UDP では <see cref="DeviceKey"/>、EOS ではログイン後に transport が差し替える ProductUserId。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class NetConnectionKeys : MonoBehaviour
    {
        private readonly Dictionary<ulong, string> _keys = new Dictionary<ulong, string>();
        private NetworkManager _manager;

        // 外すときに自分が付けたものか見分けられるよう、付けたデリゲートを持っておく
        private Action<NetworkManager.ConnectionApprovalRequest, NetworkManager.ConnectionApprovalResponse> _approve;

        /// <summary>
        /// このプロセスの端末の Id。起動して初めて引いたときに作り、終了するまで変えない。
        /// 終了すると変わるので、UDP では同じプロセスのままつなぎ直したときだけ成績を引き継ぐ。
        /// </summary>
        public static string DeviceKey { get; } = RejoinKey.NewDeviceKey();

        /// <summary>覚えている鍵の数。</summary>
        public int Count => _keys.Count;

        /// <summary>manager の承認のコールバックをこれにし、切断で鍵を忘れるようにする。1 度だけ呼ぶ。</summary>
        internal void Attach(NetworkManager manager)
        {
            if (_manager != null)
            {
                throw new InvalidOperationException("NetConnectionKeys is already attached to " + _manager.name);
            }

            if (manager == null)
            {
                throw new ArgumentNullException(nameof(manager));
            }

            _manager = manager;
            _approve = Approve;
            manager.ConnectionApprovalCallback = _approve;
            manager.OnClientDisconnectCallback += Forget;
        }

        /// <summary>clientId の鍵。鍵を送らなかった・読めなかった相手と、知らない相手では false。</summary>
        public bool TryGet(ulong clientId, out string key)
        {
            return _keys.TryGetValue(clientId, out key);
        }

        /// <summary>
        /// 接続を承認し、プレイヤーオブジェクトを作らせる。承認データが鍵として読めれば覚える。
        /// NetworkManager が呼ぶ。テストでは直接呼ぶ。
        /// </summary>
        internal void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            response.Approved = true;
            response.CreatePlayerObject = true;
            response.Pending = false;

            if (RejoinKey.TryDecode(request.Payload, out var key))
            {
                _keys[request.ClientNetworkId] = key;
                return;
            }

            _keys.Remove(request.ClientNetworkId);
            var length = request.Payload == null ? 0 : request.Payload.Length;
            Debug.LogWarning("[Net] client " + request.ClientNetworkId + " sent no valid rejoin key (" + length
                + " bytes); its stats will not be kept if it disconnects");
        }

        private void Forget(ulong clientId)
        {
            _keys.Remove(clientId);
        }

        private void OnDestroy()
        {
            if (_manager == null)
            {
                return;
            }

            _manager.OnClientDisconnectCallback -= Forget;
            if (_manager.ConnectionApprovalCallback == _approve)
            {
                _manager.ConnectionApprovalCallback = null;
            }

            _manager = null;
            _approve = null;
        }
    }
}
