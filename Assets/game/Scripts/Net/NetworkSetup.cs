using System;
using System.Collections.Generic;
using Fps.Core;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Fps.Net
{
    /// <summary>
    /// <see cref="NetworkManager"/> と transport (UDP なら <see cref="UnityTransport"/>、EOS なら
    /// <see cref="NetTransportFactories.Eos"/> が作るもの) をコードで組み立てる。
    /// シーンやプレハブに設定を持たせず、ここだけで構成が決まるようにする。
    /// 接続の承認は <see cref="NetConnectionKeys"/> が受け持ち、承認データ (<see cref="ConnectionPayload"/>) にはビルドの版と、
    /// 切断して戻ってきた人を見分ける鍵を載せる。サーバーは版が違うクライアントを断る。
    /// </summary>
    public static class NetworkSetup
    {
        /// <summary>クライアントが 1 回の接続試行で待つ時間 (ミリ秒)。</summary>
        public const int ConnectTimeoutMs = 1000;

        /// <summary>クライアントが諦めるまでの接続試行回数。<see cref="ConnectTimeoutMs"/> と掛けて約 10 秒。</summary>
        public const int MaxConnectAttempts = 10;

        /// <summary>
        /// UDP で組み立てる。<paramref name="options"/> の transport は UDP であること。
        /// 詳しくは <see cref="Configure(GameObject, NetTransportPlan, GameObject, IReadOnlyList{GameObject})"/>。
        /// </summary>
        public static NetworkManager Configure(
            GameObject target,
            NetLaunchOptions options,
            GameObject playerPrefab,
            IReadOnlyList<GameObject> networkPrefabs = null)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (options.Transport != NetTransportKind.Udp)
            {
                throw new ArgumentException(
                    "EOS needs credentials; configure with a NetTransportPlan instead", nameof(options));
            }

            return Configure(target, NetTransportPlan.Decide(options, null, null), playerPrefab, networkPrefabs);
        }

        /// <summary>
        /// <paramref name="target"/> に transport と <see cref="NetworkManager"/> を追加して設定する。
        /// 起動 (StartHost など) はしない。
        /// </summary>
        /// <param name="target">追加先。ルートの GameObject で、まだ NetworkManager を持たないこと。</param>
        /// <param name="plan">使う transport と接続先・待ち受けの設定。</param>
        /// <param name="playerPrefab">接続したクライアントごとに生成するプレハブ。NetworkObject を持つこと。</param>
        /// <param name="networkPrefabs">
        /// サーバーが実行中に Spawn するプレハブ (試合の進行役など)。どれも NetworkObject を持つこと。null なら無し。
        /// </param>
        /// <exception cref="InvalidOperationException">
        /// 構成済み、または EOS を使うのに EOS の transport がこのビルドに無い。
        /// </exception>
        public static NetworkManager Configure(
            GameObject target,
            NetTransportPlan plan,
            GameObject playerPrefab,
            IReadOnlyList<GameObject> networkPrefabs = null)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            if (playerPrefab == null)
            {
                throw new ArgumentNullException(nameof(playerPrefab));
            }

            if (playerPrefab.GetComponent<NetworkObject>() == null)
            {
                throw new ArgumentException(
                    "player prefab '" + playerPrefab.name + "' has no NetworkObject", nameof(playerPrefab));
            }

            var extraPrefabs = new List<GameObject>();
            if (networkPrefabs != null)
            {
                foreach (var prefab in networkPrefabs)
                {
                    if (prefab == null)
                    {
                        throw new ArgumentException("networkPrefabs must not contain null", nameof(networkPrefabs));
                    }

                    if (prefab.GetComponent<NetworkObject>() == null)
                    {
                        throw new ArgumentException(
                            "network prefab '" + prefab.name + "' has no NetworkObject", nameof(networkPrefabs));
                    }

                    // プレイヤーのプレハブは NetworkManager が自分で登録するので、重複させない
                    if (prefab != playerPrefab && !extraPrefabs.Contains(prefab))
                    {
                        extraPrefabs.Add(prefab);
                    }
                }
            }

            if (target.transform.parent != null)
            {
                throw new ArgumentException(
                    "NetworkManager must be on a root GameObject ('" + target.name + "' has a parent)", nameof(target));
            }

            if (target.GetComponent<NetworkManager>() != null
                || target.GetComponent<NetworkTransport>() != null
                || target.GetComponent<NetConnectionKeys>() != null)
            {
                throw new InvalidOperationException("'" + target.name + "' is already configured for networking");
            }

            if (!TryGetLocalVersion(out var version, out var versionError))
            {
                throw new InvalidOperationException("cannot send the build version on connect: " + versionError);
            }

            var options = plan.Options;
            NetworkTransport transport;
            switch (options.Transport)
            {
                case NetTransportKind.Udp:
                    transport = AddUnityTransport(target, options);
                    break;
                case NetTransportKind.Eos:
                    transport = AddEosTransport(target, plan);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(plan), options.Transport, "unknown transport");
            }

            var manager = target.AddComponent<NetworkManager>();
            if (manager.NetworkConfig == null)
            {
                manager.NetworkConfig = new NetworkConfig();
            }

            var config = manager.NetworkConfig;
            config.NetworkTransport = transport;
            config.PlayerPrefab = playerPrefab;
            // 承認データで版と、切断して戻ってきた人を見分ける鍵を受け取り、版が違う相手は断る (NetConnectionKeys)。
            // EOS のクライアントはログインした後に transport が鍵を ProductUserId へ差し替える
            config.ConnectionApproval = true;
            config.ConnectionData = ConnectionPayload.Encode(version, NetConnectionKeys.DeviceKey);
            target.AddComponent<NetConnectionKeys>().Attach(manager, version);
            // サーバーが -map で選んだシーンにクライアントを合わせるため、シーンの同期は Netcode に任せる。
            // 同期で読み直したシーンの NetBootstrap は、動いている NetworkManager を見て自分を消す (NetBootstrap.Awake)
            config.EnableSceneManagement = true;

            foreach (var prefab in extraPrefabs)
            {
                config.Prefabs.Add(new NetworkPrefab { Prefab = prefab });
            }

            return manager;
        }

        /// <summary>
        /// このビルドの版 (<see cref="Application.version"/>) を <see cref="BuildVersion.TryNormalize"/> で正規化したもの。
        /// 接続の承認データに載せ、サーバーはこれと同じ版のクライアントだけを入れる。
        /// </summary>
        /// <returns>版として読めなければ false (version は null、error に理由)。</returns>
        public static bool TryGetLocalVersion(out string version, out string error)
        {
            if (BuildVersion.TryNormalize(Application.version, out version, out error))
            {
                return true;
            }

            error = "Application.version: " + error;
            return false;
        }

        /// <summary>
        /// サーバーで、つないでいるリモートのクライアントすべてに reason (<see cref="DisconnectNotice"/> の符号) を送って切る。
        /// Host 自身のクライアントは切らない。サーバーとして待ち受けていなければ何もしない。
        /// </summary>
        /// <returns>切ったクライアントの数。</returns>
        public static int DisconnectRemoteClients(NetworkManager manager, string reason)
        {
            if (manager == null)
            {
                throw new ArgumentNullException(nameof(manager));
            }

            if (!manager.IsServer || !manager.IsListening || manager.ShutdownInProgress)
            {
                return 0;
            }

            // 切るたびに ConnectedClientsIds が縮むので、先に写してから回す
            var clients = new List<ulong>(manager.ConnectedClientsIds);
            var disconnected = 0;
            foreach (var clientId in clients)
            {
                if (clientId == NetworkManager.ServerClientId)
                {
                    continue;
                }

                manager.DisconnectClient(clientId, reason);
                disconnected++;
            }

            return disconnected;
        }

        /// <summary>
        /// クライアントで、サーバーとの接続を失った理由を <see cref="NetworkManager.DisconnectReason"/> から分ける。
        /// </summary>
        /// <param name="manager">切れたクライアントの NetworkManager。</param>
        /// <param name="wasConnected">切れる前に承認されてつながっていたか。</param>
        public static DisconnectNotice ClassifyDisconnect(NetworkManager manager, bool wasConnected)
        {
            if (manager == null)
            {
                throw new ArgumentNullException(nameof(manager));
            }

            TryGetLocalVersion(out var version, out _);
            return DisconnectNotice.Classify(manager.DisconnectReason, wasConnected, version);
        }

        private static UnityTransport AddUnityTransport(GameObject target, NetLaunchOptions options)
        {
            var transport = target.AddComponent<UnityTransport>();
            transport.ConnectTimeoutMS = ConnectTimeoutMs;
            transport.MaxConnectAttempts = MaxConnectAttempts;
            // ホスト・サーバーでは Address で待ち受け、クライアントでは Address へ接続する
            transport.SetConnectionData(options.Address, options.Port, options.Address);
            return transport;
        }

        private static NetworkTransport AddEosTransport(GameObject target, NetTransportPlan plan)
        {
            if (plan.Credentials == null)
            {
                throw new ArgumentException("EOS transport needs credentials", nameof(plan));
            }

            var factory = NetTransportFactories.Eos;
            if (factory == null)
            {
                throw new InvalidOperationException(
                    "the EOS transport is not in this build (is com.playeveryware.eos installed and Fps.NetEos compiled?)");
            }

            var transport = factory(target, plan);
            if (transport == null || transport.gameObject != target)
            {
                throw new InvalidOperationException("the EOS transport factory must add a transport to '" + target.name + "'");
            }

            return transport;
        }
    }
}
