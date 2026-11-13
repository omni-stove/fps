using System;
using System.Collections.Generic;
using System.Globalization;

namespace Fps.Core
{
    /// <summary>
    /// ネットワークの起動設定。コマンドライン引数 <c>-mode</c> / <c>-address</c> / <c>-port</c> /
    /// <c>-transport</c> / <c>-eos-server</c> / <c>-register-lobby</c> / <c>-map</c> / <c>-bots</c> / <c>-net-latency</c> / <c>-net-loss</c> /
    /// <c>-net-jitter</c> / <c>-drain-timeout</c> から作る。知らない引数 (Unity 自身の <c>-batchmode</c> など) は無視する。
    /// </summary>
    public sealed class NetLaunchOptions
    {
        public const string ModeFlag = "-mode";
        public const string AddressFlag = "-address";
        public const string PortFlag = "-port";
        public const string TransportFlag = "-transport";
        public const string EosServerFlag = "-eos-server";
        public const string RegisterLobbyFlag = "-register-lobby";
        public const string MapFlag = "-map";
        public const string BotsFlag = "-bots";
        public const string NetLatencyFlag = "-net-latency";
        public const string NetLossFlag = "-net-loss";
        public const string NetJitterFlag = "-net-jitter";
        public const string DrainTimeoutFlag = "-drain-timeout";

        /// <summary><see cref="Map"/> の名前の最長の文字数。</summary>
        public const int MaxMapNameLength = 64;

        /// <summary><see cref="Bots"/> の上限。サーバーが Bot を際限なく足さないようにする。</summary>
        public const int MaxBots = 64;

        /// <summary><see cref="NetLatencyMs"/> と <see cref="NetJitterMs"/> の上限 (ミリ秒)。</summary>
        public const int MaxNetDelayMs = 5000;

        /// <summary><see cref="NetLossPercent"/> の上限 (%)。</summary>
        public const int MaxNetLossPercent = 100;

        /// <summary><see cref="DrainTimeoutSeconds"/> の上限 (秒)。</summary>
        public const int MaxDrainTimeoutSeconds = 3600;

        public const string DefaultAddress = "127.0.0.1";
        public const ushort DefaultPort = 7777;

        /// <summary>
        /// <c>-transport</c> が無いときの transport。認証情報の有無で挙動が変わらないよう、EOS は明示したときだけ使う。
        /// </summary>
        public const NetTransportKind DefaultTransport = NetTransportKind.Udp;

        /// <summary>全インターフェースで待ち受けるアドレス。サーバー側でだけ使える。</summary>
        public const string AnyAddress = "0.0.0.0";

        /// <summary>EOS の ProductUserId の文字列表現の長さ (16 進数の桁数)。</summary>
        public const int ProductUserIdLength = 32;

        private static readonly string[] _flags =
        {
            ModeFlag, AddressFlag, PortFlag, TransportFlag, EosServerFlag, RegisterLobbyFlag,
            MapFlag, BotsFlag, NetLatencyFlag, NetLossFlag, NetJitterFlag, DrainTimeoutFlag,
        };

        public NetLaunchMode Mode { get; }

        /// <summary>UDP のとき、クライアントでは接続先、ホスト・サーバーでは待ち受けるアドレス (IPv4)。</summary>
        public string Address { get; }

        public ushort Port { get; }

        public NetTransportKind Transport { get; }

        /// <summary>EOS のクライアントが接続するサーバーの ProductUserId。それ以外では null。</summary>
        public string EosServerId { get; }

        /// <summary>
        /// サーバーが EOS のロビーに載せる、クライアントがつなぐ公開アドレス (<c>address:port</c>)。登録しないなら null。
        /// </summary>
        public string RegisterLobbyEndpoint { get; }

        /// <summary>サーバーが自分を EOS のロビーに登録するか。</summary>
        public bool RegistersLobby => RegisterLobbyEndpoint != null;

        /// <summary>ホスト・サーバーが試合に使うマップ (シーン名)。指定が無ければ null で、今のシーンのまま。</summary>
        public string Map { get; }

        /// <summary>ホスト・サーバーが試合に足す Bot の数。0 なら足さない。</summary>
        public int Bots { get; }

        /// <summary>送受信に足す遅れ (ミリ秒)。0 なら足さない。</summary>
        public int NetLatencyMs { get; }

        /// <summary>落とすパケットの割合 (%)。0 なら落とさない。</summary>
        public int NetLossPercent { get; }

        /// <summary>遅れに足すゆらぎ (ミリ秒)。0 ならゆらがない。</summary>
        public int NetJitterMs { get; }

        /// <summary>回線の劣化 (遅れ・ロス・ゆらぎ) をどれか 1 つでも求められているか。</summary>
        public bool SimulatesNetwork => NetLatencyMs > 0 || NetLossPercent > 0 || NetJitterMs > 0;

        /// <summary>サーバーが終了を求められてから、今の試合が終わるのを待つ最長の秒数。0 なら待たずに止まる。</summary>
        public int DrainTimeoutSeconds { get; }

        /// <summary>ログ向けの接続先・待ち受け先。UDP は <c>address:port</c>、EOS は <c>eos</c> か <c>eos:&lt;サーバーの ID&gt;</c>。</summary>
        public string Endpoint
        {
            get
            {
                if (Transport == NetTransportKind.Udp)
                {
                    return FormatEndpoint(Address, Port);
                }

                return EosServerId == null ? "eos" : "eos:" + EosServerId;
            }
        }

        /// <param name="mode">起動モード。</param>
        /// <param name="address">IPv4 アドレス。クライアントでは <see cref="AnyAddress"/> を使えない。</param>
        /// <param name="port">1 から 65535 のポート番号。</param>
        public NetLaunchOptions(NetLaunchMode mode, string address, ushort port)
            : this(mode, address, port, NetTransportKind.Udp, null)
        {
        }

        /// <param name="mode">起動モード。</param>
        /// <param name="address">IPv4 アドレス。クライアントでは <see cref="AnyAddress"/> を使えない。EOS では使わないが検証はする。</param>
        /// <param name="port">1 から 65535 のポート番号。</param>
        /// <param name="transport">transport。</param>
        /// <param name="eosServerId">EOS のクライアントでは必須の、サーバーの ProductUserId。EOS のクライアント以外では null。</param>
        public NetLaunchOptions(NetLaunchMode mode, string address, ushort port, NetTransportKind transport, string eosServerId)
            : this(mode, address, port, transport, eosServerId, null)
        {
        }

        /// <param name="mode">起動モード。</param>
        /// <param name="address">IPv4 アドレス。クライアントでは <see cref="AnyAddress"/> を使えない。EOS では使わないが検証はする。</param>
        /// <param name="port">1 から 65535 のポート番号。</param>
        /// <param name="transport">transport。</param>
        /// <param name="eosServerId">EOS のクライアントでは必須の、サーバーの ProductUserId。EOS のクライアント以外では null。</param>
        /// <param name="registerLobbyEndpoint">
        /// UDP のサーバーが EOS のロビーに載せる公開アドレス (<c>address:port</c>)。登録しないなら null。
        /// </param>
        public NetLaunchOptions(
            NetLaunchMode mode, string address, ushort port, NetTransportKind transport, string eosServerId, string registerLobbyEndpoint)
            : this(mode, address, port, transport, eosServerId, registerLobbyEndpoint, null, 0, 0, 0, 0, 0)
        {
        }

        /// <param name="mode">起動モード。</param>
        /// <param name="address">IPv4 アドレス。クライアントでは <see cref="AnyAddress"/> を使えない。EOS では使わないが検証はする。</param>
        /// <param name="port">1 から 65535 のポート番号。</param>
        /// <param name="transport">transport。</param>
        /// <param name="eosServerId">EOS のクライアントでは必須の、サーバーの ProductUserId。EOS のクライアント以外では null。</param>
        /// <param name="registerLobbyEndpoint">
        /// UDP のサーバーが EOS のロビーに載せる公開アドレス (<c>address:port</c>)。登録しないなら null。
        /// </param>
        /// <param name="map">ホスト・サーバーが使うマップ (<see cref="IsMapName"/> に通るシーン名)。今のシーンのままなら null。</param>
        /// <param name="bots">ホスト・サーバーが足す Bot の数。0 以上 <see cref="MaxBots"/> 以下。クライアントでは 0。</param>
        /// <param name="netLatencyMs">足す遅れ (ミリ秒)。0 以上 <see cref="MaxNetDelayMs"/> 以下。</param>
        /// <param name="netLossPercent">落とすパケットの割合 (%)。0 以上 <see cref="MaxNetLossPercent"/> 以下。</param>
        /// <param name="netJitterMs">遅れのゆらぎ (ミリ秒)。0 以上 <see cref="MaxNetDelayMs"/> 以下。</param>
        /// <param name="drainTimeoutSeconds">
        /// 終了を求められてから試合の終わりを待つ最長の秒数。0 以上 <see cref="MaxDrainTimeoutSeconds"/> 以下。サーバー以外では 0。
        /// </param>
        public NetLaunchOptions(
            NetLaunchMode mode,
            string address,
            ushort port,
            NetTransportKind transport,
            string eosServerId,
            string registerLobbyEndpoint,
            string map,
            int bots,
            int netLatencyMs,
            int netLossPercent,
            int netJitterMs,
            int drainTimeoutSeconds)
        {
            if (!Enum.IsDefined(typeof(NetLaunchMode), mode))
            {
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "unknown mode");
            }

            if (!Enum.IsDefined(typeof(NetTransportKind), transport))
            {
                throw new ArgumentOutOfRangeException(nameof(transport), transport, "unknown transport");
            }

            var addressError = ValidateAddress(mode, address);
            if (addressError != null)
            {
                throw new ArgumentException(addressError, nameof(address));
            }

            if (port == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(port), port, "port must be between 1 and 65535");
            }

            var serverError = ValidateEosServer(mode, transport, eosServerId);
            if (serverError != null)
            {
                throw new ArgumentException(serverError, nameof(eosServerId));
            }

            var registerError = ValidateRegisterLobby(mode, transport, registerLobbyEndpoint, out var normalizedEndpoint);
            if (registerError != null)
            {
                throw new ArgumentException(registerError, nameof(registerLobbyEndpoint));
            }

            var mapError = ValidateMap(mode, map);
            if (mapError != null)
            {
                throw new ArgumentException(mapError, nameof(map));
            }

            ThrowIfOutOfRange(nameof(bots), BotsFlag, bots, MaxBots);
            ThrowIfOutOfRange(nameof(netLatencyMs), NetLatencyFlag, netLatencyMs, MaxNetDelayMs);
            ThrowIfOutOfRange(nameof(netLossPercent), NetLossFlag, netLossPercent, MaxNetLossPercent);
            ThrowIfOutOfRange(nameof(netJitterMs), NetJitterFlag, netJitterMs, MaxNetDelayMs);
            ThrowIfOutOfRange(nameof(drainTimeoutSeconds), DrainTimeoutFlag, drainTimeoutSeconds, MaxDrainTimeoutSeconds);

            var botsError = ValidateBots(mode, bots);
            if (botsError != null)
            {
                throw new ArgumentException(botsError, nameof(bots));
            }

            var drainError = ValidateDrainTimeout(mode, drainTimeoutSeconds);
            if (drainError != null)
            {
                throw new ArgumentException(drainError, nameof(drainTimeoutSeconds));
            }

            Mode = mode;
            Address = address;
            Port = port;
            Transport = transport;
            EosServerId = eosServerId;
            RegisterLobbyEndpoint = normalizedEndpoint;
            Map = map;
            Bots = bots;
            NetLatencyMs = netLatencyMs;
            NetLossPercent = netLossPercent;
            NetJitterMs = netJitterMs;
            DrainTimeoutSeconds = drainTimeoutSeconds;
        }

        /// <summary>引数に <c>-mode</c> が含まれるか。値の妥当性は見ない。</summary>
        public static bool HasModeFlag(IReadOnlyList<string> args)
        {
            if (args == null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            for (var i = 0; i < args.Count; i++)
            {
                if (IsFlag(args[i], ModeFlag))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// コマンドライン引数を解析する。<c>-mode</c> が無ければ <paramref name="defaultMode"/>、
        /// <c>-address</c> が無ければ <see cref="DefaultAddress"/>、<c>-port</c> が無ければ <see cref="DefaultPort"/>、
        /// <c>-transport</c> が無ければ <see cref="DefaultTransport"/> を使う。
        /// <c>-eos-server</c> は <c>-transport eos</c> のクライアントでだけ使い、そのときは必須。
        /// <c>-register-lobby</c> は UDP のサーバーでだけ使える。
        /// <c>-map</c> と <c>-bots</c> はホスト・サーバーで、<c>-drain-timeout</c> はサーバーでだけ使える。
        /// <c>-bots</c> / <c>-net-latency</c> / <c>-net-loss</c> / <c>-net-jitter</c> / <c>-drain-timeout</c> は 0 以上の整数で、無ければ 0。
        /// </summary>
        /// <param name="args">コマンドライン引数。先頭が実行ファイルのパスでもよい。</param>
        /// <param name="defaultMode">-mode が無いときのモード。</param>
        /// <param name="options">成功したときの設定。失敗したときは null。</param>
        /// <param name="error">失敗したときの理由。成功したときは null。</param>
        /// <returns>引数が正しければ true。</returns>
        public static bool TryParse(
            IReadOnlyList<string> args,
            NetLaunchMode defaultMode,
            out NetLaunchOptions options,
            out string error)
        {
            if (args == null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            options = null;
            var values = new Dictionary<string, string>(StringComparer.Ordinal);

            for (var i = 0; i < args.Count; i++)
            {
                var flag = MatchFlag(args[i]);
                if (flag == null)
                {
                    continue;
                }

                if (i + 1 >= args.Count || IsFlagLike(args[i + 1]))
                {
                    error = flag + " requires a value";
                    return false;
                }

                if (values.ContainsKey(flag))
                {
                    error = flag + " is given more than once";
                    return false;
                }

                values.Add(flag, args[++i]);
            }

            var mode = defaultMode;
            if (values.TryGetValue(ModeFlag, out var modeText) && !TryParseMode(modeText, out mode))
            {
                error = ModeFlag + " must be host, server or client (got '" + modeText + "')";
                return false;
            }

            if (!Enum.IsDefined(typeof(NetLaunchMode), mode))
            {
                throw new ArgumentOutOfRangeException(nameof(defaultMode), defaultMode, "unknown mode");
            }

            if (!values.TryGetValue(AddressFlag, out var address))
            {
                address = DefaultAddress;
            }

            var addressError = ValidateAddress(mode, address);
            if (addressError != null)
            {
                error = addressError;
                return false;
            }

            var port = DefaultPort;
            if (values.TryGetValue(PortFlag, out var portText) && !TryParsePort(portText, out port))
            {
                error = PortFlag + " must be an integer between 1 and 65535 (got '" + portText + "')";
                return false;
            }

            var transport = DefaultTransport;
            if (values.TryGetValue(TransportFlag, out var transportText) && !TryParseTransport(transportText, out transport))
            {
                error = TransportFlag + " must be udp or eos (got '" + transportText + "')";
                return false;
            }

            values.TryGetValue(EosServerFlag, out var eosServerId);
            var serverError = ValidateEosServer(mode, transport, eosServerId);
            if (serverError != null)
            {
                error = serverError;
                return false;
            }

            values.TryGetValue(RegisterLobbyFlag, out var registerText);
            var registerError = ValidateRegisterLobby(mode, transport, registerText, out var registerEndpoint);
            if (registerError != null)
            {
                error = registerError;
                return false;
            }

            values.TryGetValue(MapFlag, out var map);
            var mapError = ValidateMap(mode, map);
            if (mapError != null)
            {
                error = mapError;
                return false;
            }

            if (!TryReadCount(values, BotsFlag, MaxBots, out var bots, out error)
                || !TryReadCount(values, NetLatencyFlag, MaxNetDelayMs, out var netLatencyMs, out error)
                || !TryReadCount(values, NetLossFlag, MaxNetLossPercent, out var netLossPercent, out error)
                || !TryReadCount(values, NetJitterFlag, MaxNetDelayMs, out var netJitterMs, out error)
                || !TryReadCount(values, DrainTimeoutFlag, MaxDrainTimeoutSeconds, out var drainTimeoutSeconds, out error))
            {
                return false;
            }

            error = ValidateBots(mode, bots) ?? ValidateDrainTimeout(mode, drainTimeoutSeconds);
            if (error != null)
            {
                return false;
            }

            options = new NetLaunchOptions(
                mode, address, port, transport, eosServerId, registerEndpoint,
                map, bots, netLatencyMs, netLossPercent, netJitterMs, drainTimeoutSeconds);
            return true;
        }

        /// <summary>ログ向けの小文字のモード名 (host / server / client)。</summary>
        public static string ModeName(NetLaunchMode mode)
        {
            switch (mode)
            {
                case NetLaunchMode.Host:
                    return "host";
                case NetLaunchMode.Server:
                    return "server";
                case NetLaunchMode.Client:
                    return "client";
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, "unknown mode");
            }
        }

        /// <summary>ログ・引数向けの小文字の transport 名 (udp / eos)。</summary>
        public static string TransportName(NetTransportKind transport)
        {
            switch (transport)
            {
                case NetTransportKind.Udp:
                    return "udp";
                case NetTransportKind.Eos:
                    return "eos";
                default:
                    throw new ArgumentOutOfRangeException(nameof(transport), transport, "unknown transport");
            }
        }

        /// <summary>
        /// EOS の ProductUserId の文字列表現 (<see cref="ProductUserIdLength"/> 桁の 16 進数) に見えるか。
        /// 実在するかは EOS に問い合わせないと分からない。
        /// </summary>
        public static bool IsProductUserId(string text)
        {
            if (text == null || text.Length != ProductUserIdLength)
            {
                return false;
            }

            foreach (var c in text)
            {
                var isHex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                if (!isHex)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// <c>address:port</c> (IPv4 と 1〜65535 のポート) を分ける。<see cref="AnyAddress"/> はつなぐ先にならないので受け付けない。
        /// </summary>
        /// <param name="text">分ける文字列。</param>
        /// <param name="address">成功したときのアドレス。失敗したときは null。</param>
        /// <param name="port">成功したときのポート。失敗したときは 0。</param>
        public static bool TryParseEndpoint(string text, out string address, out ushort port)
        {
            address = null;
            port = 0;
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            var colon = text.LastIndexOf(':');
            if (colon <= 0 || colon == text.Length - 1)
            {
                return false;
            }

            var host = text.Substring(0, colon);
            if (!IsIPv4(host) || host == AnyAddress || !TryParsePort(text.Substring(colon + 1), out var parsedPort))
            {
                return false;
            }

            address = host;
            port = parsedPort;
            return true;
        }

        /// <summary>ログ・ロビーの属性向けの <c>address:port</c>。</summary>
        public static string FormatEndpoint(string address, ushort port)
        {
            return address + ":" + port.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// transport だけを差し替えた設定を返す。UDP にしたときは EOS のサーバー ID を捨てる。
        /// ロビーへの登録・マップ・Bot・回線の劣化・終了の待ち時間はそのまま引き継ぐ。
        /// </summary>
        /// <exception cref="ArgumentException">
        /// EOS のクライアントにするのにサーバー ID が無い、または EOS にするのにロビーへの登録が指定されている。
        /// </exception>
        public NetLaunchOptions WithTransport(NetTransportKind transport)
        {
            if (transport == Transport)
            {
                return this;
            }

            var serverId = transport == NetTransportKind.Udp ? null : EosServerId;
            return new NetLaunchOptions(
                Mode, Address, Port, transport, serverId, RegisterLobbyEndpoint,
                Map, Bots, NetLatencyMs, NetLossPercent, NetJitterMs, DrainTimeoutSeconds);
        }

        /// <summary>
        /// <c>&lt;モード&gt; &lt;接続先&gt;</c> に、指定されたものだけ <c>lobby=</c> / <c>map=</c> / <c>bots=</c> /
        /// <c>latency=</c> <c>loss=</c> <c>jitter=</c> / <c>drain=</c> を続けたログ向けの文字列。
        /// </summary>
        public override string ToString()
        {
            var text = ModeName(Mode) + " " + Endpoint;
            if (RegistersLobby)
            {
                text += " lobby=" + RegisterLobbyEndpoint;
            }

            if (Map != null)
            {
                text += " map=" + Map;
            }

            if (Bots > 0)
            {
                text += " bots=" + Bots.ToString(CultureInfo.InvariantCulture);
            }

            if (SimulatesNetwork)
            {
                text += " latency=" + NetLatencyMs.ToString(CultureInfo.InvariantCulture) + "ms"
                    + " loss=" + NetLossPercent.ToString(CultureInfo.InvariantCulture) + "%"
                    + " jitter=" + NetJitterMs.ToString(CultureInfo.InvariantCulture) + "ms";
            }

            if (DrainTimeoutSeconds > 0)
            {
                text += " drain=" + DrainTimeoutSeconds.ToString(CultureInfo.InvariantCulture) + "s";
            }

            return text;
        }

        /// <summary>
        /// <c>-map</c> に使えるシーン名か。1 文字以上 <see cref="MaxMapNameLength"/> 文字以下の英数字・<c>_</c>・<c>-</c> だけを認める
        /// (パスや拡張子を書いても、ビルドに入っているシーンは名前でしか引けないため)。シーンがビルドにあるかは見ない。
        /// </summary>
        public static bool IsMapName(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Length > MaxMapNameLength)
            {
                return false;
            }

            foreach (var c in text)
            {
                var allowed = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '_' || c == '-';
                if (!allowed)
                {
                    return false;
                }
            }

            return true;
        }

        /// <returns>正しければ null、誤りならその理由。</returns>
        private static string ValidateMap(NetLaunchMode mode, string map)
        {
            if (map == null)
            {
                return null;
            }

            if (!IsMapName(map))
            {
                return MapFlag + " must be a scene name of letters, digits, '_' or '-' (at most "
                    + MaxMapNameLength.ToString(CultureInfo.InvariantCulture) + " characters, got '" + map + "')";
            }

            return mode == NetLaunchMode.Client ? HostOrServerOnly(MapFlag, mode) : null;
        }

        /// <returns>正しければ null、誤りならその理由。</returns>
        private static string ValidateBots(NetLaunchMode mode, int bots)
        {
            return bots > 0 && mode == NetLaunchMode.Client ? HostOrServerOnly(BotsFlag, mode) : null;
        }

        /// <returns>正しければ null、誤りならその理由。</returns>
        private static string ValidateDrainTimeout(NetLaunchMode mode, int seconds)
        {
            if (seconds == 0 || mode == NetLaunchMode.Server)
            {
                return null;
            }

            return DrainTimeoutFlag + " is only used by " + ModeFlag + " " + ModeName(NetLaunchMode.Server)
                + " (got " + ModeName(mode) + ")";
        }

        private static string HostOrServerOnly(string flag, NetLaunchMode mode)
        {
            return flag + " is only used by " + ModeFlag + " " + ModeName(NetLaunchMode.Host) + " or " + ModeName(NetLaunchMode.Server)
                + " (got " + ModeName(mode) + ")";
        }

        /// <summary>
        /// values の flag を 0 以上 max 以下の整数として読む。無ければ 0。整数でないか範囲外なら error に理由を入れて false。
        /// </summary>
        private static bool TryReadCount(
            Dictionary<string, string> values, string flag, int max, out int value, out string error)
        {
            value = 0;
            error = null;
            if (!values.TryGetValue(flag, out var text))
            {
                return true;
            }

            if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) || value > max)
            {
                value = 0;
                error = CountRangeMessage(flag, max) + " (got '" + text + "')";
                return false;
            }

            return true;
        }

        private static void ThrowIfOutOfRange(string paramName, string flag, int value, int max)
        {
            if (value < 0 || value > max)
            {
                throw new ArgumentOutOfRangeException(paramName, value, CountRangeMessage(flag, max));
            }
        }

        private static string CountRangeMessage(string flag, int max)
        {
            return flag + " must be an integer between 0 and " + max.ToString(CultureInfo.InvariantCulture);
        }

        private static string MatchFlag(string arg)
        {
            foreach (var flag in _flags)
            {
                if (IsFlag(arg, flag))
                {
                    return flag;
                }
            }

            return null;
        }

        private static bool TryParseMode(string text, out NetLaunchMode mode)
        {
            foreach (NetLaunchMode candidate in Enum.GetValues(typeof(NetLaunchMode)))
            {
                if (string.Equals(text, ModeName(candidate), StringComparison.OrdinalIgnoreCase))
                {
                    mode = candidate;
                    return true;
                }
            }

            mode = default;
            return false;
        }

        private static bool TryParseTransport(string text, out NetTransportKind transport)
        {
            foreach (NetTransportKind candidate in Enum.GetValues(typeof(NetTransportKind)))
            {
                if (string.Equals(text, TransportName(candidate), StringComparison.OrdinalIgnoreCase))
                {
                    transport = candidate;
                    return true;
                }
            }

            transport = default;
            return false;
        }

        private static bool TryParsePort(string text, out ushort port)
        {
            return ushort.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out port) && port != 0;
        }

        /// <returns>正しければ null、誤りならその理由。</returns>
        private static string ValidateAddress(NetLaunchMode mode, string address)
        {
            if (!IsIPv4(address))
            {
                return AddressFlag + " must be an IPv4 address such as " + DefaultAddress + " (got '" + address + "')";
            }

            if (mode == NetLaunchMode.Client && address == AnyAddress)
            {
                return AddressFlag + " " + AnyAddress + " can only be listened on; a client needs the server's address";
            }

            return null;
        }

        /// <returns>正しければ null、誤りならその理由。</returns>
        private static string ValidateEosServer(NetLaunchMode mode, NetTransportKind transport, string eosServerId)
        {
            var needsServer = transport == NetTransportKind.Eos && mode == NetLaunchMode.Client;
            if (eosServerId == null)
            {
                return needsServer
                    ? "a client with " + TransportFlag + " eos needs " + EosServerFlag + " <server product user id>"
                    : null;
            }

            if (!needsServer)
            {
                return EosServerFlag + " is only used by a client with " + TransportFlag + " eos";
            }

            if (!IsProductUserId(eosServerId))
            {
                return EosServerFlag + " must be a " + ProductUserIdLength.ToString(CultureInfo.InvariantCulture)
                    + "-digit hexadecimal product user id (got '" + eosServerId + "')";
            }

            return null;
        }

        /// <param name="mode">起動モード。</param>
        /// <param name="transport">transport。</param>
        /// <param name="endpoint">登録しないなら null。</param>
        /// <param name="normalized">正しいときの <c>address:port</c>。登録しないか誤りなら null。</param>
        /// <returns>正しければ null、誤りならその理由。</returns>
        private static string ValidateRegisterLobby(
            NetLaunchMode mode, NetTransportKind transport, string endpoint, out string normalized)
        {
            normalized = null;
            if (endpoint == null)
            {
                return null;
            }

            if (mode != NetLaunchMode.Server)
            {
                return RegisterLobbyFlag + " is only used by " + ModeFlag + " " + ModeName(NetLaunchMode.Server)
                    + " (got " + ModeName(mode) + ")";
            }

            if (transport != NetTransportKind.Udp)
            {
                return RegisterLobbyFlag + " needs " + TransportFlag + " " + TransportName(NetTransportKind.Udp)
                    + "; clients connect to the registered address over UDP";
            }

            if (!TryParseEndpoint(endpoint, out var address, out var port))
            {
                return RegisterLobbyFlag + " must be the public IPv4 address and port clients connect to, such as 203.0.113.10:"
                    + DefaultPort.ToString(CultureInfo.InvariantCulture) + " (got '" + endpoint + "')";
            }

            normalized = FormatEndpoint(address, port);
            return null;
        }

        /// <summary>ドット区切り 4 つの 0〜255 の 10 進数か。"1" や "01.2.3.4" のような省略・先頭 0 は認めない。</summary>
        private static bool IsIPv4(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            var parts = text.Split('.');
            if (parts.Length != 4)
            {
                return false;
            }

            foreach (var part in parts)
            {
                if (part.Length == 0 || part.Length > 3 || (part.Length > 1 && part[0] == '0'))
                {
                    return false;
                }

                if (!int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var octet) || octet > 255)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsFlag(string arg, string flag)
        {
            return string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>次の引数が値ではなく別のフラグに見えるか (値の書き忘れを検出する)。</summary>
        private static bool IsFlagLike(string arg)
        {
            return string.IsNullOrEmpty(arg) || arg[0] == '-';
        }
    }
}
