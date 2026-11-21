using System;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;

namespace Shooter.NetV2
{
    // パーティと参加コードはメニューで使い、試合をまたいで残るので、試合ごとに作る NetV2Client ではなく、シーンを替えても残るこの部品で回す。
    [DisallowMultipleComponent]
    public sealed class NetV2OnlineServices : MonoBehaviour
    {
        public const string ObjectName = "NetV2OnlineServices";

        private NetV2PartyControl _party;
        private NetV2JoinByCode _joinByCode;
        private NetV2ServerBrowser _serverBrowser;
        private NetV2Leaderboard _leaderboard;

        public static NetV2OnlineServices Current { get; private set; }

        public NetV2PartyControl Party => _party;

        public NetV2JoinByCode JoinByCode => _joinByCode;

        // メインメニューのサーバー一覧が開く口 (GameSignals.ServerBrowserControl)
        public NetV2ServerBrowser ServerBrowser => _serverBrowser;

        // メインメニューのリーダーボードの画面が開く口 (GameSignals.LeaderboardControl)。EOS のリーダーボードを作れなければ null
        public NetV2Leaderboard Leaderboard => _leaderboard;

        // EOS が無いビルドや認証情報が無いときは作らず、GameSignals に口を出さないことで UI にパーティと参加コードを出させない。
        public static NetV2OnlineServices Install()
        {
            if (Current != null)
            {
                return Current;
            }

            var factory = NetV2Eos.Factory;
            if (factory == null)
            {
                Debug.Log(PartyLog.Off("EOS is not in this build"));
                return null;
            }

            if (!factory.TryLoadCredentials(out var credentials, out var missing, out var loadError))
            {
                Debug.LogError(PartyLog.Off("cannot load EOS credentials: " + loadError));
                return null;
            }

            if (credentials == null)
            {
                Debug.Log(PartyLog.Off("no EOS credentials; missing " + string.Join(", ", missing)));
                return null;
            }

            if (!BuildVersion.TryNormalize(Application.version, out var version, out var versionError))
            {
                Debug.LogError(PartyLog.Off("cannot read the build version (Application.version): " + versionError));
                return null;
            }

            var partyBackend = factory.CreateLobbyBackend(credentials);
            if (partyBackend == null)
            {
                Debug.LogError(PartyLog.Off("the EOS lobby factory returned no backend"));
                return null;
            }

            var host = new GameObject(ObjectName);
            DontDestroyOnLoad(host);
            var services = host.AddComponent<NetV2OnlineServices>();
            Action<string> log = line => Debug.Log(line, services);
            services._party = new NetV2PartyControl(new PartySession(partyBackend, version, log));
            services._joinByCode = new NetV2JoinByCode(
                () => factory.CreateLobbyBackend(credentials),
                version,
                () => services._party != null ? services._party.MatchPlayers : 1,
                NetV2Client.ConnectByJoinCode,
                log);
            services._serverBrowser = new NetV2ServerBrowser(
                () => factory.CreateLobbyBackend(credentials),
                new NetV2ServerPinger(),
                version,
                () => services._party != null ? services._party.MatchPlayers : 1,
                NetV2Client.ConnectToServer,
                log);

            // ボードの並びはサーバーの送る統計 (server-data/tuning/leaderboards.json) と同じ調整値から読む
            services._leaderboard = NetV2Leaderboard.Create(KitTuning.Leaderboards, log);
            Debug.Log(PartyLog.On(version), services);
            return services;
        }

        private void Awake()
        {
            Current = this;
        }

        private void Update()
        {
            var now = Time.unscaledTime;
            _party?.Tick(now);
            _joinByCode?.Tick(now);
            _serverBrowser?.Tick(now);
            _leaderboard?.Tick(Time.unscaledDeltaTime);
        }

        private void OnDestroy()
        {
            if (Current == this)
            {
                Current = null;
            }

            _leaderboard?.Dispose();
            _leaderboard = null;
            _serverBrowser?.Dispose();
            _serverBrowser = null;
            _joinByCode?.Dispose();
            _joinByCode = null;
            _party?.Dispose();
            _party = null;
        }
    }
}
