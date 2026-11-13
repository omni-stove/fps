using System;
using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary>
    /// 専用サーバーが <c>-register-lobby</c> で作るロビーの決まりごと。クライアントはこの属性を見て、
    /// どのサーバーに UDP でつなぐかを選ぶ。
    /// </summary>
    public static class ServerLobby
    {
        /// <summary>サーバーの公開アドレス (<c>address:port</c>)。</summary>
        public const string ServerKey = "server";

        /// <summary>試合のモード。</summary>
        public const string ModeKey = "mode";

        /// <summary>ビルドの版。版の違うクライアントとサーバーはつながない。</summary>
        public const string VersionKey = "version";

        /// <summary>サーバーに接続しているプレイヤーの人数。</summary>
        public const string PlayersKey = "players";

        /// <summary>ネット対戦のモード (NetTeamDeathmatch) を表す <see cref="ModeKey"/> の値。</summary>
        public const string TeamDeathmatchMode = "tdm";

        /// <summary>
        /// サーバーのロビーのバケット ID の接頭辞。<c>-lobby join</c> の検索 (<see cref="LobbyLaunchOptions.BucketPrefix"/>) が
        /// EOS の Host ではない専用サーバーのロビーを拾って P2P でつなごうとしないよう、バケットを分ける。
        /// </summary>
        public const string BucketPrefix = "fps-server:";

        /// <summary>サーバーが Device ID で初めてログインしたときに作るユーザーの表示名。プレイヤーと見分けるために分ける。</summary>
        public const string DisplayName = "fps-server";

        /// <summary>ロビーに載せる最大人数。</summary>
        public const int MaxPlayers = LobbyLaunchOptions.DefaultMaxPlayers;

        /// <summary>ログイン・作成・属性の更新に失敗したとき、やり直すまでの秒数。</summary>
        public const float RetryIntervalSeconds = 5f;

        /// <summary>
        /// 登録できないまま待つ秒数。起動直後にこれを過ぎたら認証情報や設定の誤りとみなして諦め、
        /// 一度登録できた後は、過ぎるたびにエラーを出しながら作り直しを続ける。
        /// </summary>
        public const float TimeoutSeconds = 60f;

        /// <summary>サーバーのロビーのバケット ID。版の違うビルド同士は同じバケットに入らない。</summary>
        /// <param name="version">アプリのバージョン。空なら "0"。</param>
        public static string BucketId(string version)
        {
            return BucketPrefix + LobbyLaunchOptions.BucketVersion(version);
        }

        /// <summary>ロビーを作るときに付ける属性 (server・mode・version・players)。</summary>
        /// <param name="endpoint">クライアントがつなぐ公開アドレス (<see cref="NetLaunchOptions.RegisterLobbyEndpoint"/>)。</param>
        /// <param name="version">アプリのバージョン。空なら "0"。</param>
        /// <param name="players">今の人数 (0 以上)。</param>
        public static IReadOnlyList<LobbyAttribute> Attributes(string endpoint, string version, int players)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                throw new ArgumentException("must not be null or blank", nameof(endpoint));
            }

            return new[]
            {
                LobbyAttribute.OfText(ServerKey, endpoint),
                LobbyAttribute.OfText(ModeKey, TeamDeathmatchMode),
                LobbyAttribute.OfText(VersionKey, LobbyLaunchOptions.BucketVersion(version)),
                PlayersAttribute(players),
            };
        }

        /// <summary>人数の属性。</summary>
        /// <param name="players">今の人数 (0 以上)。</param>
        public static LobbyAttribute PlayersAttribute(int players)
        {
            if (players < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(players), players, "must not be negative");
            }

            return LobbyAttribute.OfNumber(PlayersKey, players);
        }
    }
}
