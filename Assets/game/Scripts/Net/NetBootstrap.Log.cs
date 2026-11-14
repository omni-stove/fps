using System;
using System.IO;
using Fps.Core;
using UnityEngine;

namespace Fps.Net
{
    /// <summary>
    /// <see cref="NetBootstrap"/> のうち、サーバーの運用のために起動・終了と試合のイベントを 1 行の JSON
    /// (<see cref="ServerEventLog"/>) で標準出力に出す処理。<c>-log-format json</c> (<see cref="NetLaunchOptions.LogFormat"/>) の
    /// サーバーでだけ出し、既定 (<c>text</c>) では何もしない。どちらでも今のログ (<c>[Net]</c> / <c>[Match]</c> 付き) はそのまま出る。
    /// </summary>
    public sealed partial class NetBootstrap
    {
        private bool _writesServerEvents;
        private bool _serverEventOutputBroken;
        private bool _wroteServerStopping;

        partial void LogBeforeStart(ref string error);

        partial void LogAfterStart();

        partial void LogQuitRequested(ref bool allowQuit);

        /// <summary>NetworkManager を組み立てた後、待ち受け・接続を始める前に呼ぶ。</summary>
        /// <param name="error">起動を止めるならその理由を入れる (<c>[Net]</c> 付きの文)。既に入っていれば触らない。</param>
        partial void LogBeforeStart(ref string error)
        {
            if (_options.LogFormat != ServerLogFormat.Json || _options.Mode != NetLaunchMode.Server)
            {
                return;
            }

            // 試合の進行役は StartServer の中 (HandleServerStarted) で Spawn されるので、その前に受け取る先を渡せるようにしておく
            _writesServerEvents = true;
            NetTeamDeathmatch.CurrentChanged += AttachServerEventWriter;
            AttachServerEventWriter(NetTeamDeathmatch.Current);
            Debug.Log("[Net] match events are also written to stdout as JSON lines (" + NetLaunchOptions.LogFormatFlag + " json)");
        }

        /// <summary>待ち受け・接続を始められた直後に呼ぶ。</summary>
        partial void LogAfterStart()
        {
            if (!_writesServerEvents)
            {
                return;
            }

            var version = string.IsNullOrEmpty(Application.version) ? "unknown" : Application.version;
            WriteServerEvent(ServerEventLog.ServerStarted(DateTime.UtcNow, version, _options.Transport, _options.Endpoint));
        }

        /// <summary>終了を求められたときに呼ぶ。</summary>
        /// <param name="allowQuit">終了を待たせるなら false にする。待たせたら、待ち終えたところで自分で終了する。</param>
        partial void LogQuitRequested(ref bool allowQuit)
        {
            // 他の差し込み口が終了を待たせると、待ち終えたところでもう一度求められるので、1 度だけ出す
            if (!_writesServerEvents || _wroteServerStopping)
            {
                return;
            }

            _wroteServerStopping = true;
            WriteServerEvent(ServerEventLog.ServerStopping(DateTime.UtcNow));
        }

        /// <summary>OnDestroy から呼び、試合の進行役に渡した受け取る先を外す。</summary>
        private void StopServerEventLog()
        {
            if (!_writesServerEvents)
            {
                return;
            }

            _writesServerEvents = false;
            NetTeamDeathmatch.CurrentChanged -= AttachServerEventWriter;
            NetTeamDeathmatch.Current?.SetServerEventWriter(null, false);
        }

        private void AttachServerEventWriter(NetTeamDeathmatch match)
        {
            if (match != null)
            {
                match.SetServerEventWriter(WriteServerEvent, _options.Transport == NetTransportKind.Eos);
            }
        }

        /// <summary>
        /// 1 行を標準出力に書く。Debug.Log を通すとプレイヤーのビルドではスタックトレースなどの行が付いて 1 行の JSON にならないので、
        /// Console に直接書く。書けなくなったら (受け手のパイプが閉じたなど) 一度だけ警告を出し、以後は書かない。
        /// </summary>
        private void WriteServerEvent(string line)
        {
            if (_serverEventOutputBroken)
            {
                return;
            }

            try
            {
                var output = Console.Out;
                output.WriteLine(line);
                output.Flush();
            }
            catch (Exception e) when (e is IOException || e is ObjectDisposedException)
            {
                // 試合の進行の途中で呼ばれるので、出力の失敗で試合を止めない
                _serverEventOutputBroken = true;
                Debug.LogWarning("[Net] cannot write match events to stdout (" + e.Message + "); JSON event lines are turned off");
            }
        }
    }
}
