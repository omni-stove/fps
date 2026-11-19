using System;
using System.Diagnostics;
using System.Threading;
using Shooter.Core;

namespace Shooter.Server
{
    internal sealed class ServerLoop : IDisposable
    {
        private readonly MatchServer _server;
        private readonly TickClock _clock;
        private readonly AutoResetEvent _wake = new AutoResetEvent(false);
        private int _stopRequests;

        public ServerLoop(MatchServer server, int tickRate = TickClock.DefaultTickRate)
        {
            _server = server ?? throw new ArgumentNullException(nameof(server));
            _clock = new TickClock(tickRate);
        }

        // どのスレッドから呼ばれてもシグナルのスレッドから試合を触らないよう、数えるだけにする。
        public void RequestStop()
        {
            Interlocked.Increment(ref _stopRequests);
            try
            {
                _wake.Set();
            }
            catch (ObjectDisposedException)
            {
                // 止める合図は、ループが終わって破棄した後にも届きうる (合図の購読を外す前に読んだ呼び出し)。そのときはもう起こすループが無いので捨てる。
            }
        }

        public void Run()
        {
            var watch = Stopwatch.StartNew();
            var handled = 0;
            var last = 0d;
            while (!_server.ExitRequested)
            {
                var elapsed = watch.Elapsed.TotalSeconds;
                var now = (float)elapsed;
                var requests = Volatile.Read(ref _stopRequests);
                while (handled < requests && !_server.ExitRequested)
                {
                    handled++;
                    _server.RequestStop(now);
                }

                if (_server.ExitRequested)
                {
                    break;
                }

                var ticks = _clock.Advance((float)(elapsed - last));
                last = elapsed;
                for (var i = 0; i < ticks && !_server.ExitRequested; i++)
                {
                    _server.Tick(now, _clock.TickSeconds);
                }

                var untilNextTick = (1f - _clock.Alpha) * _clock.TickSeconds;
                _wake.WaitOne(TimeSpan.FromSeconds(untilNextTick));
            }
        }

        public void Dispose()
        {
            _wake.Dispose();
        }
    }
}
