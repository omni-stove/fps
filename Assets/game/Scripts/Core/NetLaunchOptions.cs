using System;
using System.Collections.Generic;
using System.Globalization;

namespace Fps.Core
{
    /// <summary>
    /// ネットワークの起動設定。コマンドライン引数 <c>-mode</c> / <c>-address</c> / <c>-port</c> /
    /// <c>-transport</c> / <c>-eos-server</c> / <c>-register-lobby</c> から作る。知らない引数 (Unity 自身の <c>-batchmode</c> など) は無視する。
    /// </summary>
    public sealed class NetLaunchOptions
    {
        public const string ModeFlag = "-mode";
        public const string AddressFlag = "-address";
        public const string PortFlag = "-port";
        public const string TransportFlag = "-transport";
        public const string EosServerFlag = "-eos-server";
        public const string RegisterLobbyFlag = "-register-lobby";

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

        private static readonly string[] _flags = { ModeFlag, AddressFlag, PortFlag, TransportFlag, EosServerFlag, RegisterLobbyFlag };

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

            Mode = mode;
            Address = address;
            Port = port;
            Transport = transport;
            EosServerId = eosServerId;
            RegisterLobbyEndpoint = normalizedEndpoint;
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

            options = new NetLaunchOptions(mode, address, port, transport, eosServerId, registerEndpoint);
            error = null;
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
        /// transport だけを差し替えた設定を返す。UDP にしたときは EOS のサーバー ID を捨てる。ロビーへの登録はそのまま引き継ぐ。
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
            return new NetLaunchOptions(Mode, Address, Port, transport, serverId, RegisterLobbyEndpoint);
        }

        public override string ToString()
        {
            var text = ModeName(Mode) + " " + Endpoint;
            return RegistersLobby ? text + " lobby=" + RegisterLobbyEndpoint : text;
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
