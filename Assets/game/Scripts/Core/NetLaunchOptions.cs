using System;
using System.Collections.Generic;
using System.Globalization;

namespace Fps.Core
{
    /// <summary>
    /// ネットワークの起動設定。コマンドライン引数 <c>-mode</c> / <c>-address</c> / <c>-port</c> から作る。
    /// 知らない引数 (Unity 自身の <c>-batchmode</c> など) は無視する。
    /// </summary>
    public sealed class NetLaunchOptions
    {
        public const string ModeFlag = "-mode";
        public const string AddressFlag = "-address";
        public const string PortFlag = "-port";

        public const string DefaultAddress = "127.0.0.1";
        public const ushort DefaultPort = 7777;

        /// <summary>全インターフェースで待ち受けるアドレス。サーバー側でだけ使える。</summary>
        public const string AnyAddress = "0.0.0.0";

        public NetLaunchMode Mode { get; }

        /// <summary>クライアントでは接続先、ホスト・サーバーでは待ち受けるアドレス (IPv4)。</summary>
        public string Address { get; }

        public ushort Port { get; }

        /// <param name="mode">起動モード。</param>
        /// <param name="address">IPv4 アドレス。クライアントでは <see cref="AnyAddress"/> を使えない。</param>
        /// <param name="port">1 から 65535 のポート番号。</param>
        public NetLaunchOptions(NetLaunchMode mode, string address, ushort port)
        {
            if (!Enum.IsDefined(typeof(NetLaunchMode), mode))
            {
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "unknown mode");
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

            Mode = mode;
            Address = address;
            Port = port;
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
        /// <c>-address</c> が無ければ <see cref="DefaultAddress"/>、<c>-port</c> が無ければ <see cref="DefaultPort"/> を使う。
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
            string modeText = null;
            string addressText = null;
            string portText = null;

            for (var i = 0; i < args.Count; i++)
            {
                string flag;
                if (IsFlag(args[i], ModeFlag))
                {
                    flag = ModeFlag;
                }
                else if (IsFlag(args[i], AddressFlag))
                {
                    flag = AddressFlag;
                }
                else if (IsFlag(args[i], PortFlag))
                {
                    flag = PortFlag;
                }
                else
                {
                    continue;
                }

                if (i + 1 >= args.Count || IsFlagLike(args[i + 1]))
                {
                    error = flag + " requires a value";
                    return false;
                }

                var value = args[++i];
                if (flag == ModeFlag)
                {
                    if (modeText != null)
                    {
                        error = ModeFlag + " is given more than once";
                        return false;
                    }

                    modeText = value;
                }
                else if (flag == AddressFlag)
                {
                    if (addressText != null)
                    {
                        error = AddressFlag + " is given more than once";
                        return false;
                    }

                    addressText = value;
                }
                else
                {
                    if (portText != null)
                    {
                        error = PortFlag + " is given more than once";
                        return false;
                    }

                    portText = value;
                }
            }

            var mode = defaultMode;
            if (modeText != null && !TryParseMode(modeText, out mode))
            {
                error = ModeFlag + " must be host, server or client (got '" + modeText + "')";
                return false;
            }

            if (!Enum.IsDefined(typeof(NetLaunchMode), mode))
            {
                throw new ArgumentOutOfRangeException(nameof(defaultMode), defaultMode, "unknown mode");
            }

            var address = addressText ?? DefaultAddress;
            var addressError = ValidateAddress(mode, address);
            if (addressError != null)
            {
                error = addressError;
                return false;
            }

            var port = DefaultPort;
            if (portText != null && !TryParsePort(portText, out port))
            {
                error = PortFlag + " must be an integer between 1 and 65535 (got '" + portText + "')";
                return false;
            }

            options = new NetLaunchOptions(mode, address, port);
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

        public override string ToString()
        {
            return ModeName(Mode) + " " + Address + ":" + Port.ToString(CultureInfo.InvariantCulture);
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
