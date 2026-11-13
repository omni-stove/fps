using System;
using System.Globalization;
using Fps.Core;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Fps.Net
{
    /// <summary>
    /// <see cref="NetBootstrap"/> のうち、<c>-net-latency</c> / <c>-net-loss</c> / <c>-net-jitter</c>
    /// (<see cref="NetLaunchOptions.SimulatesNetwork"/>) で回線の劣化を真似る処理。
    /// 開発ビルドとエディターでは、待ち受け・接続を始める前に <see cref="UnityTransport.SetDebugSimulatorParameters"/> へ値を入れ、
    /// 効かせた値を <see cref="NetSimulationLogTag"/> 付きで出す。製品ビルドと UDP 以外の transport では警告を出して無視する。
    /// </summary>
    public sealed partial class NetBootstrap
    {
        /// <summary>回線の劣化を真似る処理のログの頭。</summary>
        public const string NetSimulationLogTag = "[NetSim]";

        /// <summary>
        /// このビルドで transport のネットワークシミュレーターを使えるか (エディターと開発ビルド)。
        /// 製品ビルドで遅れやロスを足せると、配った実行ファイルの引数一つで回線の悪い客を装えてしまうので使わせない。
        /// </summary>
        public static bool IsNetSimulationBuild
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return true;
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// 回線の劣化を transport に効かせるかを決める。
        /// </summary>
        /// <param name="options">起動設定。</param>
        /// <param name="simulationBuild">シミュレーターを使えるビルドか (<see cref="IsNetSimulationBuild"/>)。</param>
        /// <param name="transport">組み立てた transport。</param>
        public static NetSimulationOutcome DecideNetSimulation(NetLaunchOptions options, bool simulationBuild, NetworkTransport transport)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (!options.SimulatesNetwork)
            {
                return NetSimulationOutcome.NotRequested;
            }

            if (!simulationBuild)
            {
                return NetSimulationOutcome.IgnoredInReleaseBuild;
            }

            return transport is UnityTransport ? NetSimulationOutcome.Applied : NetSimulationOutcome.IgnoredForTransport;
        }

        /// <summary>
        /// 効かせた値のログ。例: <c>[NetSim] latency=200 loss=5 jitter=10 (ms, %, ms)</c>。
        /// </summary>
        public static string FormatNetSimulationLog(NetLaunchOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            return NetSimulationLogTag + " " + FormatNetSimulationValues(options) + " (ms, %, ms)";
        }

        /// <summary>
        /// 劣化を効かせなかったときの警告。効かせなかった理由が無い (<see cref="NetSimulationOutcome.NotRequested"/> か
        /// <see cref="NetSimulationOutcome.Applied"/>) なら null。
        /// </summary>
        public static string FormatNetSimulationWarning(NetSimulationOutcome outcome, NetLaunchOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var flags = NetLaunchOptions.NetLatencyFlag + " / " + NetLaunchOptions.NetLossFlag + " / " + NetLaunchOptions.NetJitterFlag;
            switch (outcome)
            {
                case NetSimulationOutcome.IgnoredInReleaseBuild:
                    return NetSimulationLogTag + " " + flags + " only work in development builds and the editor; ignoring "
                        + FormatNetSimulationValues(options);
                case NetSimulationOutcome.IgnoredForTransport:
                    return NetSimulationLogTag + " " + flags + " only work with transport="
                        + NetLaunchOptions.TransportName(NetTransportKind.Udp) + "; ignoring "
                        + FormatNetSimulationValues(options) + " for transport=" + NetLaunchOptions.TransportName(options.Transport);
                default:
                    return null;
            }
        }

        private static string FormatNetSimulationValues(NetLaunchOptions options)
        {
            return "latency=" + options.NetLatencyMs.ToString(CultureInfo.InvariantCulture)
                + " loss=" + options.NetLossPercent.ToString(CultureInfo.InvariantCulture)
                + " jitter=" + options.NetJitterMs.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>NetworkManager を組み立てた後、待ち受け・接続を始める前に呼ぶ。</summary>
        /// <param name="error">起動を止めるならその理由を入れる (<c>[Net]</c> 付きの文)。既に入っていれば触らない。</param>
        partial void NetSimulationBeforeStart(ref string error);

        partial void NetSimulationBeforeStart(ref string error)
        {
            if (error != null)
            {
                return;
            }

            var transport = _manager.NetworkConfig.NetworkTransport;
            var outcome = DecideNetSimulation(_options, IsNetSimulationBuild, transport);
            switch (outcome)
            {
                case NetSimulationOutcome.NotRequested:
                    return;
                case NetSimulationOutcome.Applied:
                    if (TryApplyDebugSimulator((UnityTransport)transport, _options, out var applyError))
                    {
                        Debug.Log(FormatNetSimulationLog(_options));
                    }
                    else
                    {
                        // 引数で求められた劣化が効かないまま確かめを進めると結果を読み違えるので、起動を止める
                        error = "[Net] cannot apply " + FormatNetSimulationValues(_options) + ": " + applyError;
                    }

                    return;
                default:
                    Debug.LogWarning(FormatNetSimulationWarning(outcome, _options));
                    return;
            }
        }

        private static bool TryApplyDebugSimulator(UnityTransport transport, NetLaunchOptions options, out string error)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            try
            {
                // Multiplayer Tools の Network Simulator を入れずに引数だけで劣化させたいので、非推奨の印が付いていてもこの入口を使う
#pragma warning disable CS0618
                transport.SetDebugSimulatorParameters(options.NetLatencyMs, options.NetJitterMs, options.NetLossPercent);
#pragma warning restore CS0618
            }
            catch (Exception e)
            {
                // 起動処理の外側の境界なので、何が飛んできても起動を止める理由に変える
                Debug.LogException(e);
                error = e.GetType().Name + ": " + e.Message;
                return false;
            }

            error = null;
            return true;
#else
            error = "the network simulator is not in release builds";
            return false;
#endif
        }

        /// <summary>待ち受け・接続を始められた直後に呼ぶ。</summary>
        partial void NetSimulationAfterStart();

        /// <summary>終了を求められたときに呼ぶ。</summary>
        /// <param name="allowQuit">終了を待たせるなら false にする。待たせたら、待ち終えたところで自分で終了する。</param>
        partial void NetSimulationQuitRequested(ref bool allowQuit);
    }
}
