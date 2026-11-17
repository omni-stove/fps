using System;
using System.Collections.Generic;
using System.Globalization;

namespace Shooter.Core
{
    // Unity に頼らないよう、時刻は呼ぶ側の時計 (秒) で受け取る。
    public sealed class ServerDrain
    {
        public const int DefaultTimeoutSeconds = 600;

        private ServerDrainState _state;
        private ServerDrainStop _stop;
        private float _deadline;

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

        public bool IsDraining => _state == ServerDrainState.Draining;

        public ServerDrainStop Stop => _stop;

        public float Deadline => _deadline;

        // NetLaunchOptions は書かれていないときも 0 にするので、書かれた 0 (待たない) と区別するためにここで引数を見る。
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

        public bool Tick(float now, bool matchOver, int players)
        {
            return _state == ServerDrainState.Draining && TryFinish(now, matchOver, players);
        }

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
