using System;
using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary>
    /// <c>-netstack</c> の読み方。新しい手順 (<see cref="NetStackKind.V2"/>) は置き換えが終わるまで、付けたときだけ使う。
    /// v2 は .NET のサーバーへつなぐクライアントだけなので、<c>-mode client</c> を求め、まだ移していない起動 (ロビー・EOS の通信路・回線のシミュレーション) は断る。
    /// </summary>
    public static class NetStackLaunch
    {
        public const string Flag = "-netstack";
        public const string NetcodeName = "v1";
        public const string V2Name = "v2";

        /// <summary>引数の誤りで終えるときの終了コード (Netcode のクライアントの NetBootstrap と同じ)。</summary>
        public const int ExitInvalidArguments = 2;

        /// <summary><c>-netstack</c> を読む。無ければ <see cref="NetStackKind.Netcode"/>。</summary>
        /// <returns>値が無い・知らない値・2 回指定なら false (error に理由)。</returns>
        public static bool TryParse(IReadOnlyList<string> args, out NetStackKind stack, out string error)
        {
            if (args == null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            stack = NetStackKind.Netcode;
            error = null;
            var seen = false;
            for (var i = 0; i < args.Count; i++)
            {
                if (!string.Equals(args[i], Flag, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (seen)
                {
                    error = Flag + " is given more than once";
                    return false;
                }

                seen = true;
                var value = i + 1 < args.Count ? args[i + 1] : null;
                if (string.Equals(value, NetcodeName, StringComparison.OrdinalIgnoreCase))
                {
                    stack = NetStackKind.Netcode;
                }
                else if (string.Equals(value, V2Name, StringComparison.OrdinalIgnoreCase))
                {
                    stack = NetStackKind.V2;
                }
                else
                {
                    stack = NetStackKind.Netcode;
                    error = Flag + " must be " + NetcodeName + " or " + V2Name + " (got '" + value + "')";
                    return false;
                }

                i++;
            }

            return true;
        }

        /// <summary><c>-netstack v2</c> が付いているか。値が正しくなければ false (<see cref="ResolveStart"/> が誤りにする)。</summary>
        public static bool RequestsV2(IReadOnlyList<string> args)
        {
            return TryParse(args, out var stack, out _) && stack == NetStackKind.V2;
        }

        /// <summary>
        /// <c>-netstack v2</c> のクライアントの起動設定を決める。<c>-address</c>・<c>-port</c> は Netcode のクライアントと同じ意味で、
        /// <c>-map</c> は今と同じくクライアントには付けられない (つないだサーバーのマップへ移る)。
        /// </summary>
        /// <returns>引数が正しく、v2 で起動できるなら true。</returns>
        public static bool TryResolveV2Client(IReadOnlyList<string> args, out NetLaunchOptions options, out string error)
        {
            return TryResolveV2(args, true, out options, out error);
        }

        /// <summary>
        /// 起動したプロセスの v2 の始め方を決める。専用サーバーと <c>-netstack</c> の無い起動は <see cref="NetV2Start.NotRequested"/>。
        /// <c>-mode client</c> 付きなら起動してすぐつなぎ (<see cref="NetV2Start.Connect"/>)、無ければメニューのオンラインのボタンから
        /// options の <c>-address</c>・<c>-port</c> へつなぐ (<see cref="NetV2Start.FromMenu"/>)。
        /// </summary>
        /// <returns>
        /// <c>-netstack</c> の値の誤り (付け忘れ・知らない値・2 回指定) と v2 で動かせない引数は <see cref="NetV2Start.Invalid"/> (error に理由)。
        /// 呼ぶ側は <see cref="ExitInvalidArguments"/> で終える。黙って Netcode のクライアントで起動すると、.NET のサーバーへつなぐつもりの人には
        /// つながらない理由が分からないため。
        /// </returns>
        public static NetV2Start ResolveStart(IReadOnlyList<string> args, bool isDedicatedServer, out NetLaunchOptions options, out string error)
        {
            if (args == null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            options = null;
            error = null;
            if (isDedicatedServer)
            {
                return NetV2Start.NotRequested;
            }

            if (!TryParse(args, out var stack, out error))
            {
                return NetV2Start.Invalid;
            }

            if (stack != NetStackKind.V2)
            {
                return NetV2Start.NotRequested;
            }

            var connect = NetLaunchOptions.HasModeFlag(args);
            if (!TryResolveV2(args, connect, out options, out error))
            {
                return NetV2Start.Invalid;
            }

            return connect ? NetV2Start.Connect : NetV2Start.FromMenu;
        }

        private static bool TryResolveV2(IReadOnlyList<string> args, bool requireMode, out NetLaunchOptions options, out string error)
        {
            if (args == null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            options = null;
            if (LobbyLaunchOptions.HasLobbyFlag(args))
            {
                error = LobbyLaunchOptions.LobbyFlag + " is not supported with " + Flag + " " + V2Name + " yet; connect with "
                    + NetLaunchOptions.AddressFlag + " instead";
                return false;
            }

            if (requireMode && !NetLaunchOptions.HasModeFlag(args))
            {
                error = Flag + " " + V2Name + " needs " + NetLaunchOptions.ModeFlag + " client: it only connects to a .NET server"
                    + " (make server-dotnet)";
                return false;
            }

            if (!NetLaunchOptions.TryParse(args, NetLaunchMode.Client, out var parsed, out error))
            {
                return false;
            }

            if (parsed.Mode != NetLaunchMode.Client)
            {
                error = Flag + " " + V2Name + " only runs " + NetLaunchOptions.ModeFlag + " client (got "
                    + NetLaunchOptions.ModeName(parsed.Mode) + "); run the server with make server-dotnet";
                return false;
            }

            if (parsed.Transport != NetTransportKind.Udp)
            {
                error = NetLaunchOptions.TransportFlag + " " + NetLaunchOptions.TransportName(parsed.Transport) + " is not supported with "
                    + Flag + " " + V2Name + " yet; use udp";
                return false;
            }

            if (parsed.SimulatesNetwork)
            {
                error = NetLaunchOptions.NetLatencyFlag + " / " + NetLaunchOptions.NetLossFlag + " / " + NetLaunchOptions.NetJitterFlag
                    + " are not supported with " + Flag + " " + V2Name + " yet";
                return false;
            }

            options = parsed;
            return true;
        }
    }
}
