using System;
using System.Collections.Generic;
using Fps.Core;
using Unity.Netcode;
using UnityEngine;

namespace Fps.Net
{
    /// <summary>
    /// 接続の承認データ (<see cref="ConnectionPayload"/>) を見てクライアントを入れるか決め (<see cref="ConnectionApproval"/>)、
    /// 入れた相手の、切断して戻ってきた人を見分ける鍵 (<see cref="RejoinKey"/>) をクライアント Id ごとに覚える。
    /// <see cref="NetworkSetup"/> が NetworkManager と同じ GameObject に付けて承認のコールバックにし、
    /// <see cref="NetTeamDeathmatch"/> が試合に加えるときに引く。
    /// </summary>
    /// <remarks>
    /// 版がサーバーと違う相手と、承認データが読めない相手 (版を載せない古いビルドを含む) は、理由の符号
    /// (<see cref="DisconnectNotice"/>) を添えて断る。クライアントはその符号でメニューに理由を出す。
    /// クライアントが送る鍵は、UDP では <see cref="DeviceKey"/>、EOS ではログイン後に transport が差し替える ProductUserId。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class NetConnectionKeys : MonoBehaviour
    {
        private readonly Dictionary<ulong, string> _keys = new Dictionary<ulong, string>();
        private NetworkManager _manager;
        private string _serverVersion;

        // 外すときに自分が付けたものか見分けられるよう、付けたデリゲートを持っておく
        private Action<NetworkManager.ConnectionApprovalRequest, NetworkManager.ConnectionApprovalResponse> _approve;

        /// <summary>
        /// このプロセスの端末の Id。起動して初めて引いたときに作り、終了するまで変えない。
        /// 終了すると変わるので、UDP では同じプロセスのままつなぎ直したときだけ成績を引き継ぐ。
        /// </summary>
        public static string DeviceKey { get; } = RejoinKey.NewDeviceKey();

        /// <summary>覚えている鍵の数。</summary>
        public int Count => _keys.Count;

        /// <summary>
        /// このサーバーの版。これと同じ版のクライアントだけを入れる。<see cref="Attach"/> が入れる (テストでは直接入れる)。
        /// </summary>
        /// <exception cref="ArgumentException">正規化済みの版 (<see cref="ConnectionPayload.IsNormalizedVersion"/>) でない。</exception>
        public string ServerVersion
        {
            get => _serverVersion;
            internal set
            {
                if (!ConnectionPayload.IsNormalizedVersion(value))
                {
                    throw new ArgumentException("server version '" + value + "' is not a normalized build version", nameof(value));
                }

                _serverVersion = value;
            }
        }

        /// <summary>
        /// manager の承認のコールバックをこれにし、切断で鍵を忘れるようにする。1 度だけ呼ぶ。
        /// </summary>
        /// <param name="manager">承認を任せる NetworkManager。</param>
        /// <param name="serverVersion">このサーバーの版 (<see cref="ServerVersion"/>)。</param>
        internal void Attach(NetworkManager manager, string serverVersion)
        {
            if (_manager != null)
            {
                throw new InvalidOperationException("NetConnectionKeys is already attached to " + _manager.name);
            }

            if (manager == null)
            {
                throw new ArgumentNullException(nameof(manager));
            }

            ServerVersion = serverVersion;
            _manager = manager;
            _approve = Approve;
            manager.ConnectionApprovalCallback = _approve;
            manager.OnClientDisconnectCallback += Forget;
        }

        /// <summary>clientId の鍵。承認していない相手 (断った相手を含む) では false。</summary>
        public bool TryGet(ulong clientId, out string key)
        {
            return _keys.TryGetValue(clientId, out key);
        }

        /// <summary>
        /// 承認データの版が <see cref="ServerVersion"/> と同じなら承認してプレイヤーオブジェクトを作らせ、鍵を覚える。
        /// 違う・読めなければ、理由の符号を <see cref="NetworkManager.ConnectionApprovalResponse.Reason"/> に入れて断る。
        /// NetworkManager が呼ぶ。テストでは直接呼ぶ。
        /// </summary>
        /// <exception cref="InvalidOperationException"><see cref="ServerVersion"/> がまだ無い。</exception>
        internal void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            if (_serverVersion == null)
            {
                throw new InvalidOperationException("NetConnectionKeys has no server version; attach it through NetworkSetup.Configure");
            }

            var verdict = ConnectionApproval.Judge(_serverVersion, request.Payload);
            response.Pending = false;
            response.Approved = verdict.Approved;
            response.CreatePlayerObject = verdict.Approved;

            if (verdict.Approved)
            {
                _keys[request.ClientNetworkId] = verdict.RejoinKey;
                return;
            }

            _keys.Remove(request.ClientNetworkId);
            response.Reason = verdict.RejectReason;
            Debug.LogWarning("[Net] rejected client " + request.ClientNetworkId + ": " + verdict.LogText);
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
