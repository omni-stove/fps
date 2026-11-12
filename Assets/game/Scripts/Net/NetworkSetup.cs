using System;
using System.Collections.Generic;
using Fps.Core;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Fps.Net
{
    /// <summary>
    /// <see cref="NetworkManager"/> と <see cref="UnityTransport"/> をコードで組み立てる。
    /// シーンやプレハブに設定を持たせず、ここだけで構成が決まるようにする。
    /// </summary>
    public static class NetworkSetup
    {
        /// <summary>クライアントが 1 回の接続試行で待つ時間 (ミリ秒)。</summary>
        public const int ConnectTimeoutMs = 1000;

        /// <summary>クライアントが諦めるまでの接続試行回数。<see cref="ConnectTimeoutMs"/> と掛けて約 10 秒。</summary>
        public const int MaxConnectAttempts = 10;

        /// <summary>
        /// <paramref name="target"/> に <see cref="UnityTransport"/> と <see cref="NetworkManager"/> を追加して設定する。
        /// 起動 (StartHost など) はしない。
        /// </summary>
        /// <param name="target">追加先。ルートの GameObject で、まだ NetworkManager を持たないこと。</param>
        /// <param name="options">接続先・待ち受けの設定。</param>
        /// <param name="playerPrefab">接続したクライアントごとに生成するプレハブ。NetworkObject を持つこと。</param>
        /// <param name="networkPrefabs">
        /// サーバーが実行中に Spawn するプレハブ (試合の進行役など)。どれも NetworkObject を持つこと。null なら無し。
        /// </param>
        public static NetworkManager Configure(
            GameObject target,
            NetLaunchOptions options,
            GameObject playerPrefab,
            IReadOnlyList<GameObject> networkPrefabs = null)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
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

            if (target.GetComponent<NetworkManager>() != null || target.GetComponent<UnityTransport>() != null)
            {
                throw new InvalidOperationException("'" + target.name + "' is already configured for networking");
            }

            var transport = target.AddComponent<UnityTransport>();
            transport.ConnectTimeoutMS = ConnectTimeoutMs;
            transport.MaxConnectAttempts = MaxConnectAttempts;
            // ホスト・サーバーでは Address で待ち受け、クライアントでは Address へ接続する
            transport.SetConnectionData(options.Address, options.Port, options.Address);

            var manager = target.AddComponent<NetworkManager>();
            if (manager.NetworkConfig == null)
            {
                manager.NetworkConfig = new NetworkConfig();
            }

            var config = manager.NetworkConfig;
            config.NetworkTransport = transport;
            config.PlayerPrefab = playerPrefab;
            config.ConnectionApproval = false;
            // シーンの同期はまだ要らない。有効だとクライアントが NetTest を読み直して Bootstrap が二重になる
            config.EnableSceneManagement = false;

            foreach (var prefab in extraPrefabs)
            {
                config.Prefabs.Add(new NetworkPrefab { Prefab = prefab });
            }

            return manager;
        }
    }
}
