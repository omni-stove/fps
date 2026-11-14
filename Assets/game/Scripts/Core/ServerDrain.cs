using System;
using System.Collections.Generic;
using System.Globalization;

namespace Fps.Core
{
    /// <summary>
    /// 専用サーバーが終了を求められてから (SIGTERM など) 止まるまでの「止まる準備中」(drain) を決める。
    /// 最初の要求で準備を始め、つないでいる人がいなくなるか、今の試合が終わるか、<see cref="TimeoutSeconds"/> が過ぎたら止まる。
    /// 準備中にもう一度求められたらすぐ止まる。準備中は新しい参加を断る (<see cref="ConnectionApproval"/>)。
    /// </summary>
    /// <remarks>
    /// 時刻は呼ぶ側の時計 (秒) で受け取り、Unity に頼らない。止まると決めた後は状態を変えない。
    /// </remarks>
    public sealed class ServerDrain
    {
        /// <summary><c>-drain-timeout</c> が無いときに、試合の終わりを待つ最長の秒数。</summary>
        public const int DefaultTimeoutSeconds = 600;

        private ServerDrainState _state;
        private ServerDrainStop _stop;
        private float _deadline;

        /// <param name="timeoutSeconds">試合の終わりを待つ最長の秒数。0 なら待たずに止まる。</param>
        /// <exception cref="ArgumentOutOfRangeException">負、または <see cref="NetLaunchOptions.MaxDrainTimeoutSeconds"/> より大きい。</exception>
        public ServerDrain(int timeoutSeconds)
        {
            if (timeoutSeconds < 0 || timeoutSeconds > NetLaunchOptions.MaxDrainTimeoutSeconds)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(timeoutSeconds), timeoutSeconds, "must be between 0 and " + NetLaunchOptions.MaxDrainTimeoutSeconds);
            }

            TimeoutSeconds = timeoutSeconds;
        }

        public int TimeoutSeconds { get; }

        public ServerDrainState State => _state;

        /// <summary>止まる準備中 (求められたが、まだ止まると決めていない) なら true。新しい参加を断る。</summary>
        public bool IsDraining => _state == ServerDrainState.Draining;

        /// <summary>止まると決めた理由。まだ決めていなければ <see cref="ServerDrainStop.None"/>。</summary>
        public ServerDrainStop Stop => _stop;

        /// <summary>準備を始めてから、待つのをやめる時刻。準備を始める前は 0。</summary>
        public float Deadline => _deadline;

        /// <summary>
        /// 起動引数に <c>-drain-timeout</c> があれば解析済みのその値、無ければ <see cref="DefaultTimeoutSeconds"/>。
        /// </summary>
        /// <param name="args">コマンドライン引数。<see cref="NetLaunchOptions.TryParse"/> に渡したもの。</param>
        /// <param name="parsedSeconds"><see cref="NetLaunchOptions.DrainTimeoutSeconds"/>。</param>
        /// <remarks>
        /// <see cref="NetLaunchOptions"/> は書かれていないときも 0 にするので、書かれた 0 (待たない) と区別するためにここで引数を見る。
        /// </remarks>
        public static int ResolveTimeoutSeconds(IReadOnlyList<string> args, int parsedSeconds)
        {
            if (args == null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            foreach (var arg in args)
            {
                if (string.Equals(arg, NetLaunchOptions.DrainTimeoutFlag, StringComparison.OrdinalIgnoreCase))
                {
                    return parsedSeconds;
                }
            }

            return DefaultTimeoutSeconds;
        }

        /// <summary>
        /// 終了を求められた。最初の要求なら準備を始め、すぐ止まってよい (つないでいる人がいない・試合が無いか終わっている・
        /// 待つ秒数が 0) なら止まると決める。準備中の 2 回目の要求なら <see cref="ServerDrainStop.RequestedAgain"/> で止まると決める。
        /// </summary>
        /// <param name="now">今の時刻 (秒)。<see cref="Tick"/> と同じ時計で渡す。</param>
        /// <param name="matchOver">今の試合が無いか、終わっていれば true。</param>
        /// <param name="players">つないでいる人の数 (Bot を除く)。</param>
        /// <returns>終了を待たせるなら true。止まると決めた (前に決めていた) なら false。</returns>
        public bool Request(float now, bool matchOver, int players)
        {
            switch (_state)
            {
                case ServerDrainState.Idle:
                    _state = ServerDrainState.Draining;
                    _deadline = now + TimeoutSeconds;
                    return !TryFinish(now, matchOver, players);
                case ServerDrainState.Draining:
                    Finish(ServerDrainStop.RequestedAgain);
                    return false;
                default:
                    return false;
            }
        }

        /// <summary>準備中に毎フレーム呼ぶ。止まると決めたその呼び出しでだけ true を返す。</summary>
        /// <param name="now">今の時刻 (秒)。</param>
        /// <param name="matchOver">準備を始めてから試合が終わった (または今の試合が無い) なら true。</param>
        /// <param name="players">つないでいる人の数 (Bot を除く)。</param>
        public bool Tick(float now, bool matchOver, int players)
        {
            return _state == ServerDrainState.Draining && TryFinish(now, matchOver, players);
        }

        /// <summary>止まると決めた理由のログ向けの文。例: <c>the match ended</c>。</summary>
        public string DescribeStop()
        {
            switch (_stop)
            {
                case ServerDrainStop.NoPlayers:
                    return "no players are connected";
                case ServerDrainStop.MatchEnded:
                    return "the match ended";
                case ServerDrainStop.TimedOut:
                    return "the drain timeout (" + TimeoutSeconds.ToString(CultureInfo.InvariantCulture) + "s) passed";
                case ServerDrainStop.RequestedAgain:
                    return "a second stop request arrived";
                default:
                    return "not stopping";
            }
        }

        private bool TryFinish(float now, bool matchOver, int players)
        {
            if (players <= 0)
            {
                Finish(ServerDrainStop.NoPlayers);
            }
            else if (matchOver)
            {
                Finish(ServerDrainStop.MatchEnded);
            }
            else if (now >= _deadline)
            {
                Finish(ServerDrainStop.TimedOut);
            }
            else
            {
                return false;
            }

            return true;
        }

        private void Finish(ServerDrainStop stop)
        {
            _state = ServerDrainState.Finished;
            _stop = stop;
        }
    }
}
