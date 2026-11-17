using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public static class NetClientLaunch
    {
        public const int ExitInvalidArguments = 2;

        private static readonly string[] _serverFlags =
        {
            NetLaunchOptions.AddressFlag, NetLaunchOptions.PortFlag, NetLaunchOptions.EosServerFlag,
        };

        private static readonly string[] _unsupportedWhenSearching =
        {
            NetLaunchOptions.MapFlag, NetLaunchOptions.BotsFlag, NetLaunchOptions.RegisterLobbyFlag, NetLaunchOptions.DrainTimeoutFlag,
            NetLaunchOptions.LogFormatFlag, NetLaunchOptions.NetLatencyFlag, NetLaunchOptions.NetLossFlag, NetLaunchOptions.NetJitterFlag,
        };

        public static NetClientStart ResolveStart(IReadOnlyList<string> args, out NetLaunchOptions server, out string error)
        {
            if (args == null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            server = null;
            if (!LobbyLaunchOptions.TryParse(args, out var lobby, out error))
            {
                return NetClientStart.Invalid;
            }

            if (lobby.IsRequested)
            {
                return TryResolveSearch(args, out error) ? NetClientStart.QuickMatch : NetClientStart.Invalid;
            }

            var connect = NetLaunchOptions.HasModeFlag(args);
            if (!connect && !NamesServer(args))
            {
                return TryResolveSearch(args, out error) ? NetClientStart.FromMenu : NetClientStart.Invalid;
            }

            if (!TryResolveServer(args, out server, out error))
            {
                return NetClientStart.Invalid;
            }

            return connect ? NetClientStart.Connect : NetClientStart.FromMenu;
        }

        public static bool NamesServer(IReadOnlyList<string> args)
        {
            foreach (var flag in _serverFlags)
            {
                if (NetLaunchOptions.HasFlag(args, flag))
                {
                    return true;
                }
            }

            return false;
        }

        public static NetLaunchOptions DefaultServer()
        {
            return new NetLaunchOptions(NetLaunchMode.Client, NetLaunchOptions.DefaultAddress, NetLaunchOptions.DefaultPort);
        }

        private static bool TryResolveServer(IReadOnlyList<string> args, out NetLaunchOptions options, out string error)
        {
            options = null;
            if (!NetLaunchOptions.TryParse(args, NetLaunchMode.Client, out var parsed, out error))
            {
                return false;
            }

            if (parsed.Mode != NetLaunchMode.Client)
            {
                error = "this build only runs " + NetLaunchOptions.ModeFlag + " client (got " + NetLaunchOptions.ModeName(parsed.Mode)
                    + "); run the server with make server-dotnet or the server image";
                return false;
            }

            if (parsed.SimulatesNetwork)
            {
                error = NetLaunchOptions.NetLatencyFlag + " / " + NetLaunchOptions.NetLossFlag + " / " + NetLaunchOptions.NetJitterFlag
                    + " are not supported by the client";
                return false;
            }

            options = parsed;
            return true;
        }

        private static bool TryResolveSearch(IReadOnlyList<string> args, out string error)
        {
            foreach (var flag in _unsupportedWhenSearching)
            {
                if (NetLaunchOptions.HasFlag(args, flag))
                {
                    error = flag + " is not supported by the client";
                    return false;
                }
            }

            // ロビーで探したサーバーへは UDP でつなぐ (サーバーは UDP で待ち受けるときだけロビーに載る) ので、探すときに -transport eos は断る。
            var seen = false;
            for (var i = 0; i < args.Count; i++)
            {
                if (!string.Equals(args[i], NetLaunchOptions.TransportFlag, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (seen)
                {
                    error = NetLaunchOptions.TransportFlag + " is given more than once";
                    return false;
                }

                seen = true;
                if (i + 1 >= args.Count || !NetLaunchOptions.TryParseTransport(args[i + 1], out var transport))
                {
                    error = NetLaunchOptions.TransportFlag + " must be udp or eos (got '" + (i + 1 < args.Count ? args[i + 1] : "") + "')";
                    return false;
                }

                if (transport != NetTransportKind.Udp)
                {
                    error = "quick match connects to the server lobby's address over " + NetLaunchOptions.TransportFlag + " "
                        + NetLaunchOptions.TransportName(NetTransportKind.Udp) + " (servers register only on UDP); to use EOS, connect with "
                        + NetLaunchOptions.ModeFlag + " client " + NetLaunchOptions.TransportFlag + " "
                        + NetLaunchOptions.TransportName(NetTransportKind.Eos) + " " + NetLaunchOptions.EosServerFlag + " <server id>";
                    return false;
                }
            }

            error = null;
            return true;
        }
    }
}
