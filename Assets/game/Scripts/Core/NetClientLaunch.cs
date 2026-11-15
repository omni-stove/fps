using System;
using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary>
    /// ゲームのクライアントの起動引数の読み方。クライアントは .NET の専用サーバーへつなぐだけなので、<c>-mode client</c> か
    /// <c>-lobby quickmatch</c> で起動してすぐつなぐか、どちらも無ければメニューのクイックマッチからつなぐ。
    /// サーバーとして動く起動 (<c>-mode server</c>) と、クライアントでは使えない引数は断る。
    /// </summary>
    public static class NetClientLaunch
    {
        /// <summary>引数の誤りで終えるときの終了コード。</summary>
        public const int ExitInvalidArguments = 2;

        /// <summary>つなぐサーバーを決める引数。どれかがあれば、メニューのクイックマッチもロビーを探さずにそのサーバーへつなぐ。</summary>
        private static readonly string[] _serverFlags =
        {
            NetLaunchOptions.AddressFlag, NetLaunchOptions.PortFlag, NetLaunchOptions.EosServerFlag,
        };

        /// <summary>サーバーだけが使う引数と、クライアントでは動かせない回線のシミュレーション。ロビーを探すときに断る。</summary>
        private static readonly string[] _unsupportedWhenSearching =
        {
            NetLaunchOptions.MapFlag, NetLaunchOptions.BotsFlag, NetLaunchOptions.RegisterLobbyFlag, NetLaunchOptions.DrainTimeoutFlag,
            NetLaunchOptions.LogFormatFlag, NetLaunchOptions.NetLatencyFlag, NetLaunchOptions.NetLossFlag, NetLaunchOptions.NetJitterFlag,
        };

        /// <summary>
        /// 起動したプロセスのクライアントの始め方を決める。
        /// <list type="bullet">
        /// <item><c>-lobby quickmatch</c>: <see cref="NetClientStart.QuickMatch"/>。server は null。</item>
        /// <item><c>-mode client</c>: <see cref="NetClientStart.Connect"/>。server につなぐ。</item>
        /// <item>どちらも無い: <see cref="NetClientStart.FromMenu"/>。<c>-address</c>・<c>-port</c>・<c>-eos-server</c> のどれかがあれば
        /// server にそのサーバー、無ければ server は null で、メニューのクイックマッチはロビーを探す。</item>
        /// </list>
        /// ロビーで探したサーバーへは UDP でつなぐ (サーバーは UDP で待ち受けるときだけロビーに載る) ので、探すときに <c>-transport eos</c> は断る。
        /// </summary>
        /// <returns>
        /// 引数が誤っているか、クライアントで動かせない引数があれば <see cref="NetClientStart.Invalid"/> (error に理由)。
        /// 呼ぶ側は <see cref="ExitInvalidArguments"/> で終える。
        /// </returns>
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

        /// <summary>つなぐサーバーを決める引数 (<c>-address</c>・<c>-port</c>・<c>-eos-server</c>) があるか。</summary>
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

        /// <summary>
        /// ロビーで探せないときにメニューのクイックマッチがつなぐ既定のサーバー
        /// (<see cref="NetLaunchOptions.DefaultAddress"/>:<see cref="NetLaunchOptions.DefaultPort"/> に UDP)。
        /// </summary>
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
