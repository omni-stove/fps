using System;
using System.Collections.Generic;

namespace Shooter.Core
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

        /// <summary>参加コードを知る人だけが入れる非公開の試合か (整数。0 以外なら非公開)。<see cref="LobbyInfo.IsPrivate"/> で読む。</summary>
        public const string PrivateKey = "private";

        /// <summary>非公開の試合に入る参加コード (文字列)。<see cref="LobbyInfo.JoinCode"/> で読む。</summary>
        public const string JoinCodeKey = "join_code";

        /// <summary>試合にいるプレイヤーの実力の平均 (整数)。<see cref="LobbyInfo.SkillAverage"/> で読む。</summary>
        public const string SkillAverageKey = "skill_average";

        /// <summary>試合のボイスチャットが使えるか (整数。0 以外なら使える)。<see cref="LobbyInfo.VoiceEnabled"/> で読む。</summary>
        public const string VoiceEnabledKey = "voice_enabled";

        /// <summary>ネット対戦のモード (チームで戦う試合) を表す <see cref="ModeKey"/> の値。</summary>
        public const string TeamDeathmatchMode = "tdm";

        /// <summary>サーバーのロビーのバケット ID の接頭辞。同じ EOS のサンドボックスを使う別のゲームのロビーと混ざらないようにする。</summary>
        public const string BucketPrefix = "shooter-server:";

        /// <summary>サーバーが Device ID で初めてログインしたときに作るユーザーの表示名。プレイヤーと見分けるために分ける。</summary>
        public const string DisplayName = "shooter-server";

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

        /// <summary>
        /// ロビーを作るときに付ける属性 (server・mode・version・players・voice_enabled)。
        /// サーバーのロビーはいつも RTC 部屋を開いて作るので、voice_enabled は 1。
        /// </summary>
        /// <param name="endpoint">クライアントがつなぐ公開アドレス (<see cref="NetLaunchOptions.RegisterLobbyEndpoint"/>)。</param>
        /// <param name="version">アプリのバージョン。空なら "0"。</param>
        /// <param name="players">今の人数 (0 以上)。</param>
        public static IReadOnlyList<LobbyAttribute> Attributes(string endpoint, string version, int players)
        {
            return Attributes(endpoint, version, players, NetMatchSettings.Default);
        }

        /// <summary>
        /// 試合の設定 <paramref name="match"/> のサーバーのロビーに付ける属性。<see cref="Attributes(string, string, int)"/> の属性に加え、
        /// 非公開 (<see cref="NetMatchSettings.PrivateMatch"/>) なら <see cref="PrivateKey"/> を 1 にし、参加コードがあれば
        /// 揃えたコード (<see cref="JoinCode.TryNormalize"/>) を <see cref="JoinCodeKey"/> に載せる。
        /// </summary>
        /// <param name="endpoint">クライアントがつなぐ公開アドレス (<see cref="NetLaunchOptions.RegisterLobbyEndpoint"/>)。</param>
        /// <param name="version">アプリのバージョン。空なら "0"。</param>
        /// <param name="players">今の人数 (0 以上)。</param>
        /// <param name="match">サーバーの試合の設定。</param>
        public static IReadOnlyList<LobbyAttribute> Attributes(string endpoint, string version, int players, NetMatchSettings match)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                throw new ArgumentException("must not be null or blank", nameof(endpoint));
            }

            if (match == null)
            {
                throw new ArgumentNullException(nameof(match));
            }

            var attributes = new List<LobbyAttribute>
            {
                LobbyAttribute.OfText(ServerKey, endpoint),
                LobbyAttribute.OfText(ModeKey, TeamDeathmatchMode),
                LobbyAttribute.OfText(VersionKey, LobbyLaunchOptions.BucketVersion(version)),
                PlayersAttribute(players),
                LobbyAttribute.OfNumber(VoiceEnabledKey, 1),
            };

            if (match.PrivateMatch)
            {
                // コードが無くても非公開の印は付け、誰も入れないサーバーを Quick Match が選ばないようにする
                attributes.Add(LobbyAttribute.OfNumber(PrivateKey, 1));
                if (JoinCode.TryNormalize(match.JoinCode, out var code))
                {
                    attributes.Add(LobbyAttribute.OfText(JoinCodeKey, code));
                }
            }

            return attributes;
        }

        /// <summary>
        /// 試合の人数 <paramref name="maxPlayers"/> のサーバーが作るロビーのメンバーの上限。
        /// ボイスのためにプレイヤーもメンバーとして入り、オーナーのサーバー自身も 1 人分を使うので 1 足す。
        /// EOS のロビーの上限 (<see cref="LobbyLaunchOptions.MaxMaxPlayers"/>) を超えないよう切り詰める。
        /// </summary>
        /// <param name="maxPlayers">試合に入れる人数 (<see cref="NetMatchSettings.MaxPlayers"/>)。</param>
        public static int MaxMembers(int maxPlayers)
        {
            if (maxPlayers < NetMatchSettings.MinMaxPlayers || maxPlayers > NetMatchSettings.MaxMaxPlayers)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxPlayers), maxPlayers,
                    "must be between " + NetMatchSettings.MinMaxPlayers + " and " + NetMatchSettings.MaxMaxPlayers);
            }

            return Math.Min(maxPlayers + 1, LobbyLaunchOptions.MaxMaxPlayers);
        }

        /// <summary>
        /// サーバーのロビー <paramref name="lobby"/> に入れるプレイヤーの数。ボイスのあるロビー (<see cref="LobbyInfo.VoiceEnabled"/>) は
        /// サーバー自身の 1 人分を <see cref="LobbyInfo.MaxMembers"/> から除き、ボイスの無いロビーは最大人数をそのまま使う。
        /// </summary>
        public static int PlayerSlots(LobbyInfo lobby)
        {
            if (lobby == null)
            {
                throw new ArgumentNullException(nameof(lobby));
            }

            return lobby.VoiceEnabled ? lobby.MaxMembers - 1 : lobby.MaxMembers;
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

        /// <summary>
        /// クイックマッチで選んだサーバーのロビー lobby の公開アドレス (<see cref="ServerKey"/>) へ UDP でつなぐクライアントの起動設定。
        /// サーバーは UDP で待ち受けるときだけロビーに載る (<c>-register-lobby</c> は <c>-transport udp</c> だけ) ので、UDP でつなぐ。
        /// </summary>
        /// <exception cref="ArgumentException">アドレスが IPv4:port でない。</exception>
        public static NetLaunchOptions ConnectOptions(LobbyInfo lobby)
        {
            if (lobby == null)
            {
                throw new ArgumentNullException(nameof(lobby));
            }

            lobby.TryGetText(ServerKey, out var endpoint);
            if (!NetLaunchOptions.TryParseEndpoint(endpoint, out var address, out var port))
            {
                throw new ArgumentException("the server address '" + endpoint + "' is not an IPv4 address:port", nameof(lobby));
            }

            return new NetLaunchOptions(NetLaunchMode.Client, address, port, NetTransportKind.Udp, null);
        }
    }
}
