using System;
using System.Globalization;

namespace Fps.Core
{
    /// <summary>
    /// オンラインの HUD の右上に出す回線の表示 (Ping・パケットロス・サーバーの tick) の文字列と色の段階。
    /// </summary>
    public static class NetStatsFormat
    {
        /// <summary>この Ping (ms) 以上を黄色で出す。</summary>
        public const int WarningPingMs = 150;

        /// <summary>この Ping (ms) 以上を赤で出す。</summary>
        public const int BadPingMs = 300;

        /// <summary>まだ分からない値の代わりに出す文字列。</summary>
        public const string Unknown = "--";

        /// <summary>pingMs の色の段階。null (まだ分からない) は <see cref="NetPingLevel.Good"/>。</summary>
        public static NetPingLevel PingLevel(int? pingMs)
        {
            if (!pingMs.HasValue)
            {
                return NetPingLevel.Good;
            }

            if (pingMs.Value >= BadPingMs)
            {
                return NetPingLevel.Bad;
            }

            return pingMs.Value >= WarningPingMs ? NetPingLevel.Warning : NetPingLevel.Good;
        }

        /// <summary>
        /// 「Ping 123 ms  ロス 4%  tick 5678」の形の 1 行。分からない値は <see cref="Unknown"/> にする。
        /// 負の Ping とロスは 0 に、100 を超えるロスは 100 に収める。
        /// </summary>
        public static string Text(int? pingMs, int? lossPercent, int? serverTick)
        {
            var ping = pingMs.HasValue ? Number(Math.Max(0, pingMs.Value)) : Unknown;
            var loss = lossPercent.HasValue ? Number(Math.Max(0, Math.Min(100, lossPercent.Value))) : Unknown;
            var tick = serverTick.HasValue ? Number(serverTick.Value) : Unknown;
            return GameText.Format("netStats.line", ping, loss, tick);
        }

        /// <summary>トランスポートが返す往復時間 (ms) を表示用の int にする。int に収まらなければ <see cref="int.MaxValue"/>。</summary>
        public static int PingFromRtt(ulong rttMs)
        {
            return rttMs > int.MaxValue ? int.MaxValue : (int)rttMs;
        }

        private static string Number(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
