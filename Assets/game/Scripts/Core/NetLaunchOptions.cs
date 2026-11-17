using System;
using System.Collections.Generic;
using System.Globalization;

namespace Shooter.Core
{
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
        public const string LogFormatFlag = "-log-format";

        public const int MaxMapNameLength = 64;

        // サーバーが Bot を際限なく足さないよう上限を持つ。
        public const int MaxBots = 64;

        public const int MaxNetDelayMs = 5000;

        public const int MaxNetLossPercent = 100;

        public const int MaxDrainTimeoutSeconds = 3600;

        public const string DefaultAddress = "127.0.0.1";
        public const ushort DefaultPort = 7777;

        // 認証情報の有無で挙動が変わらないよう、EOS は明示したときだけ使う。
        public const NetTransportKind DefaultTransport = NetTransportKind.Udp;

        public const string AnyAddress = "0.0.0.0";

        public const int ProductUserIdLength = 32;

        private static readonly string[] _flags =
        {
            ModeFlag, AddressFlag, PortFlag, TransportFlag, EosServerFlag, RegisterLobbyFlag,
            MapFlag, BotsFlag, NetLatencyFlag, NetLossFlag, NetJitterFlag, DrainTimeoutFlag, LogFormatFlag,
        };

        public NetLaunchMode Mode { get; }

        public string Address { get; }

        public ushort Port { get; }

        public NetTransportKind Transport { get; }

        public string EosServerId { get; }

        public string RegisterLobbyEndpoint { get; }

        public bool RegistersLobby => RegisterLobbyEndpoint != null;

        public string Map { get; }

        public int Bots { get; }

        public int NetLatencyMs { get; }

        public int NetLossPercent { get; }

        public int NetJitterMs { get; }

        public bool SimulatesNetwork => NetLatencyMs > 0 || NetLossPercent > 0 || NetJitterMs > 0;

        // -drain-timeout が無いときも 0 になるので、サーバーは ServerDrain.ResolveTimeoutSeconds で既定の秒数に読み替える。
        public int DrainTimeoutSeconds { get; }

        public ServerLogFormat LogFormat { get; }

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

        public NetLaunchOptions(NetLaunchMode mode, string address, ushort port)
            : this(mode, address, port, NetTransportKind.Udp, null)
        {
        }

        public NetLaunchOptions(NetLaunchMode mode, string address, ushort port, NetTransportKind transport, string eosServerId)
            : this(mode, address, port, transport, eosServerId, null)
        {
        }

        public NetLaunchOptions(
            NetLaunchMode mode, string address, ushort port, NetTransportKind transport, string eosServerId, string registerLobbyEndpoint)
            : this(mode, address, port, transport, eosServerId, registerLobbyEndpoint, null, 0, 0, 0, 0, 0)
        {
        }

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
            : this(
                mode, address, port, transport, eosServerId, registerLobbyEndpoint,
                map, bots, netLatencyMs, netLossPercent, netJitterMs, drainTimeoutSeconds, ServerLogFormat.Text)
        {
        }

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
            int drainTimeoutSeconds,
            ServerLogFormat logFormat)
        {
            if (!Enum.IsDefined(typeof(NetLaunchMode), mode))
            {
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "unknown mode");
            }

            if (!Enum.IsDefined(typeof(NetTransportKind), transport))
            {
                throw new ArgumentOutOfRangeException(nameof(transport), transport, "unknown transport");
            }

            if (!Enum.IsDefined(typeof(ServerLogFormat), logFormat))
            {
                throw new ArgumentOutOfRangeException(nameof(logFormat), logFormat, "unknown log format");
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

            var logFormatError = ValidateLogFormat(mode, logFormat);
            if (logFormatError != null)
            {
                throw new ArgumentException(logFormatError, nameof(logFormat));
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
            LogFormat = logFormat;
        }

        public static bool HasModeFlag(IReadOnlyList<string> args)
        {
            return HasFlag(args, ModeFlag);
        }

        public static bool HasFlag(IReadOnlyList<string> args, string flag)
        {
            if (args == null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            for (var i = 0; i < args.Count; i++)
            {
                if (IsFlag(args[i], flag))
                {
                    return true;
                }
            }

            return false;
        }

        // 知らない引数を断らないのは、Unity 自身の -batchmode などが混ざって渡るため。
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
                error = ModeFlag + " must be server or client (got '" + modeText + "')";
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

            var logFormat = ServerLogFormat.Text;
            if (values.TryGetValue(LogFormatFlag, out var logFormatText) && !TryParseLogFormat(logFormatText, out logFormat))
            {
                error = LogFormatFlag + " must be text or json (got '" + logFormatText + "')";
                return false;
            }

            error = ValidateBots(mode, bots) ?? ValidateDrainTimeout(mode, drainTimeoutSeconds) ?? ValidateLogFormat(mode, logFormat);
            if (error != null)
            {
                return false;
            }

            options = new NetLaunchOptions(
                mode, address, port, transport, eosServerId, registerEndpoint,
                map, bots, netLatencyMs, netLossPercent, netJitterMs, drainTimeoutSeconds, logFormat);
            return true;
        }

        public static string ModeName(NetLaunchMode mode)
        {
            switch (mode)
            {
                case NetLaunchMode.Server:
                    return "server";
                case NetLaunchMode.Client:
                    return "client";
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, "unknown mode");
            }
        }

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

        public static string LogFormatName(ServerLogFormat format)
        {
            switch (format)
            {
                case ServerLogFormat.Text:
                    return "text";
                case ServerLogFormat.Json:
                    return "json";
                default:
                    throw new ArgumentOutOfRangeException(nameof(format), format, "unknown log format");
            }
        }

        // 形だけを見るのは、実在するかは EOS に問い合わせないと分からないため。
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

        // AnyAddress を受け付けないのは、つなぐ先にならないため。
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

        public static string FormatEndpoint(string address, ushort port)
        {
            return address + ":" + port.ToString(CultureInfo.InvariantCulture);
        }

        public NetLaunchOptions WithTransport(NetTransportKind transport)
        {
            if (transport == Transport)
            {
                return this;
            }

            var serverId = transport == NetTransportKind.Udp ? null : EosServerId;
            return new NetLaunchOptions(
                Mode, Address, Port, transport, serverId, RegisterLobbyEndpoint,
                Map, Bots, NetLatencyMs, NetLossPercent, NetJitterMs, DrainTimeoutSeconds, LogFormat);
        }

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

            if (LogFormat != ServerLogFormat.Text)
            {
                text += " log=" + LogFormatName(LogFormat);
            }

            return text;
        }

        // パスや拡張子を認めないのは、ビルドに入っているシーンは名前でしか引けないため。
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

            return mode == NetLaunchMode.Client ? ServerOnly(MapFlag, mode) : null;
        }

        private static string ValidateBots(NetLaunchMode mode, int bots)
        {
            return bots > 0 && mode == NetLaunchMode.Client ? ServerOnly(BotsFlag, mode) : null;
        }

        private static string ValidateDrainTimeout(NetLaunchMode mode, int seconds)
        {
            if (seconds == 0 || mode == NetLaunchMode.Server)
            {
                return null;
            }

            return DrainTimeoutFlag + " is only used by " + ModeFlag + " " + ModeName(NetLaunchMode.Server)
                + " (got " + ModeName(mode) + ")";
        }

        private static string ValidateLogFormat(NetLaunchMode mode, ServerLogFormat format)
        {
            // JSON の行は専用サーバーの標準出力を集める運用のためのもので、クライアントには読む先が無い
            if (format == ServerLogFormat.Text || mode == NetLaunchMode.Server)
            {
                return null;
            }

            return LogFormatFlag + " " + LogFormatName(format) + " is only used by " + ModeFlag + " " + ModeName(NetLaunchMode.Server)
                + " (got " + ModeName(mode) + ")";
        }

        private static string ServerOnly(string flag, NetLaunchMode mode)
        {
            return flag + " is only used by " + ModeFlag + " " + ModeName(NetLaunchMode.Server) + " (got " + ModeName(mode) + ")";
        }

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

        private static bool TryParseLogFormat(string text, out ServerLogFormat format)
        {
            foreach (ServerLogFormat candidate in Enum.GetValues(typeof(ServerLogFormat)))
            {
                if (string.Equals(text, LogFormatName(candidate), StringComparison.OrdinalIgnoreCase))
                {
                    format = candidate;
                    return true;
                }
            }

            format = default;
            return false;
        }

        public static bool TryParseTransport(string text, out NetTransportKind transport)
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

        // 次の引数が別のフラグに見えたら値として読まないのは、値の書き忘れを見つけるため。
        private static bool IsFlagLike(string arg)
        {
            return string.IsNullOrEmpty(arg) || arg[0] == '-';
        }
    }
}
