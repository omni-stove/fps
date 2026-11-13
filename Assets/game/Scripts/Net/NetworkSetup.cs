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
    /// 接続の承認は <see cref="NetConnectionKeys"/> が受け持ち、承認データには切断して戻ってきた人を見分ける鍵を載せる。
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
            // 切断して戻ってきた人を見分ける鍵を承認データで受け取る (NetConnectionKeys)。承認そのものはいつも通す。
            // EOS のクライアントはログインした後に transport が ProductUserId へ差し替える
            config.ConnectionApproval = true;
            config.ConnectionData = RejoinKey.Encode(NetConnectionKeys.DeviceKey);
            target.AddComponent<NetConnectionKeys>().Attach(manager);
            // シーンの同期はまだ要らない。有効だとクライアントが NetTest を読み直して Bootstrap が二重になる
            config.EnableSceneManagement = false;

            foreach (var prefab in extraPrefabs)
            {
                config.Prefabs.Add(new NetworkPrefab { Prefab = prefab });
            }

            return manager;
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
