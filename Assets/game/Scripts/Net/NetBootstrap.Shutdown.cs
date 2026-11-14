using System;
using System.Globalization;
using Fps.Core;
using UnityEngine;

namespace Fps.Net
{
    /// <summary>
    /// <see cref="NetBootstrap"/> のうち、専用サーバー (<c>-mode server</c>) が終了を求められたとき (Linux では SIGTERM。
    /// Unity のプレイヤーは SIGTERM を <see cref="Application.wantsToQuit"/> に通す) に、すぐには止まらず「止まる準備中」にする処理。
    /// 準備中はロビーの登録を消し、接続の承認で新しい参加を <see cref="DisconnectNotice.ServerDrainingDetail"/> を理由に断り、
    /// 今の試合が終わるか、つないでいる人がいなくなるか、<c>-drain-timeout</c> 秒 (無ければ <see cref="ServerDrain.DefaultTimeoutSeconds"/>)
    /// が過ぎたところで終了コード 0 で止まる。準備中にもう一度求められたらすぐ止まる。決め方は <see cref="ServerDrain"/>。
    /// </summary>
    /// <remarks>
    /// Host とクライアント、起動に失敗したサーバーは待たせない。待たせても試合を続ける相手がいないか、続けられない。
    /// </remarks>
    public sealed partial class NetBootstrap
    {
        /// <summary>止まる準備中に、残りの秒数と人数をログに出す間隔 (秒)。</summary>
        public const float DrainStatusLogIntervalSeconds = 30f;

        private ServerDrain _drain;
        private NetTeamDeathmatch _drainMatch;
        private bool _drainMatchEnded;
        private bool _watchingMatchForDrain;
        private float _nextDrainStatusLogTime;

        partial void ShutdownBeforeStart(ref string error);

        partial void ShutdownAfterStart();

        partial void ShutdownQuitRequested(ref bool allowQuit);

        /// <summary>NetworkManager を組み立てた後、待ち受け・接続を始める前に呼ぶ。</summary>
        /// <param name="error">起動を止めるならその理由を入れる (<c>[Net]</c> 付きの文)。既に入っていれば触らない。</param>
        partial void ShutdownBeforeStart(ref string error)
        {
            if (error != null || _options.Mode != NetLaunchMode.Server)
            {
                return;
            }

            var seconds = ServerDrain.ResolveTimeoutSeconds(Environment.GetCommandLineArgs(), _options.DrainTimeoutSeconds);
            _drain = new ServerDrain(seconds);
        }

        /// <summary>待ち受け・接続を始められた直後に呼ぶ。</summary>
        partial void ShutdownAfterStart()
        {
            if (_drain == null)
            {
                return;
            }

            Debug.Log(_drain.TimeoutSeconds > 0
                ? "[Net] on a stop request (SIGTERM) the server refuses new players and waits up to " + Seconds(_drain.TimeoutSeconds)
                    + " for the match to end; a second request stops it at once"
                : "[Net] on a stop request (SIGTERM) the server stops at once (" + NetLaunchOptions.DrainTimeoutFlag + " 0)");
        }

        /// <summary>終了を求められたときに呼ぶ。</summary>
        /// <param name="allowQuit">終了を待たせるなら false にする。待たせたら、待ち終えたところで自分で終了する。</param>
        partial void ShutdownQuitRequested(ref bool allowQuit)
        {
            // 起動に失敗して自分で終了するときと、ほかの差し込み口が既に待たせているときは触らない
            if (!allowQuit || _drain == null || _failed || _manager == null || !_manager.IsServer)
            {
                return;
            }

            var wasFinished = _drain.State == ServerDrainState.Finished;
            var wasDraining = _drain.IsDraining;
            if (_drain.Request(Time.unscaledTime, IsMatchOverNow(), _remoteClients.Count))
            {
                BeginDrain();
                allowQuit = false;
                return;
            }

            // 待ち終えた後に自分で呼んだ Application.Quit なので、理由は待ち終えたときに出している
            if (wasFinished)
            {
                return;
            }

            StopWatchingMatchForDrain();
            Debug.Log(wasDraining
                ? "[Net] drain cut short: " + _drain.DescribeStop() + "; stopping now (players=" + _remoteClients.Count + ")"
                : "[Net] stop requested; stopping now because " + _drain.DescribeStop());
        }

        /// <summary>止まる準備を始める。ロビーから外し、新しい参加を断り、試合の終わりを見張る。</summary>
        private void BeginDrain()
        {
            Debug.Log("[Net] stop requested; draining: refusing new players and waiting up to " + Seconds(_drain.TimeoutSeconds)
                + " for the match to end (players=" + _remoteClients.Count + "). Send the stop signal again to stop now");

            var keys = _manager.GetComponent<NetConnectionKeys>();
            if (keys != null)
            {
                keys.Draining = true;
            }
            else
            {
                // NetworkSetup が必ず付けるので、無ければ組み立ての誤り。試合を待つのはやめない
                Debug.LogError("[Net] NetConnectionKeys is missing on " + _manager.name + "; new players cannot be refused while draining");
            }

            // 待っている間にクイックマッチで選ばれないよう、終了の前の片付けを待たずにロビーを閉じ始める
            CloseServerLobby();

            _watchingMatchForDrain = true;
            NetTeamDeathmatch.CurrentChanged += HandleDrainMatchChanged;
            WatchMatchStatusForDrain(NetTeamDeathmatch.Current);
            _nextDrainStatusLogTime = Time.unscaledTime + DrainStatusLogIntervalSeconds;
        }

        /// <summary>Update から毎フレーム呼ぶ。止まる準備を終えてよくなったら終了コード 0 で終了する。</summary>
        private void ShutdownUpdate()
        {
            if (_drain == null || !_drain.IsDraining || _failed)
            {
                return;
            }

            var now = Time.unscaledTime;
            if (!_drain.Tick(now, _drainMatchEnded || IsMatchOverNow(), _remoteClients.Count))
            {
                if (now >= _nextDrainStatusLogTime)
                {
                    _nextDrainStatusLogTime = now + DrainStatusLogIntervalSeconds;
                    var left = Mathf.Max(0, Mathf.CeilToInt(_drain.Deadline - now));
                    Debug.Log("[Net] draining: waiting for the match to end (players=" + _remoteClients.Count + ", " + Seconds(left) + " left)");
                }

                return;
            }

            StopWatchingMatchForDrain();
            Debug.Log("[Net] drain finished: " + _drain.DescribeStop() + "; stopping with exit code " + ExitDisconnected);
            _requestedExitCode = ExitDisconnected;
            Application.Quit(ExitDisconnected);
        }

        /// <summary>今の試合が無いか、始まらなかったか、終わって次の試合を待っているなら true。</summary>
        private static bool IsMatchOverNow()
        {
            var match = NetTeamDeathmatch.Current;
            return match == null || !match.IsRunningOnServer || match.State == GameModeState.Ended;
        }

        // 次の試合までの待ちを 0 秒にしていると、終わった同じフレームに次の試合が始まり、毎フレーム見るだけでは終わりを見逃す
        private void WatchMatchStatusForDrain(NetTeamDeathmatch match)
        {
            if (_drainMatch != null)
            {
                _drainMatch.StatusVariable.OnValueChanged -= HandleDrainMatchStatusChanged;
            }

            _drainMatch = match;
            if (match != null)
            {
                match.StatusVariable.OnValueChanged += HandleDrainMatchStatusChanged;
            }
        }

        private void HandleDrainMatchStatusChanged(NetMatchStatus previous, NetMatchStatus current)
        {
            if (current.State == GameModeState.Ended)
            {
                _drainMatchEnded = true;
            }
        }

        private void HandleDrainMatchChanged(NetTeamDeathmatch match)
        {
            // 待っていた試合が消えた・入れ替わったなら、その試合はもう続かない
            _drainMatchEnded = true;
            WatchMatchStatusForDrain(match);
        }

        private void StopWatchingMatchForDrain()
        {
            if (!_watchingMatchForDrain)
            {
                return;
            }

            _watchingMatchForDrain = false;
            NetTeamDeathmatch.CurrentChanged -= HandleDrainMatchChanged;
            WatchMatchStatusForDrain(null);
        }

        private static string Seconds(int seconds)
        {
            return seconds.ToString(CultureInfo.InvariantCulture) + "s";
        }
    }
}
