using System;
using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary>
    /// <c>-netstack</c> の読み方。新しい手順 (<see cref="NetStackKind.V2"/>) は置き換えが終わるまで、付けたときだけ使う。
    /// v2 は .NET のサーバーへつなぐクライアントだけなので、<c>-mode client</c> を求め、まだ移していない起動 (ロビー・回線のシミュレーション) は断る。
    /// </summary>
    public static class NetStackLaunch
    {
        public const string Flag = "-netstack";
        public const string NetcodeName = "v1";
        public const string V2Name = "v2";

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

        /// <summary><c>-netstack v2</c> が付いているか。値が正しくなければ false (<c>NetBootstrap</c> が <see cref="TryParse"/> で誤りを出して止める)。</summary>
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

            if (!NetLaunchOptions.HasModeFlag(args))
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
