using System;
using Shooter.Core;

namespace Shooter.Runtime
{
    // モードごとの走らせ役に手を入れず、どのモードも出している GameSignals.ModeStatus の終わりで要約を書く。
    // 時間は Time.time ではなく渡された時計で数える。止めている間やスローの演出で、試合の長さがずれないようにするため。
    public sealed class MatchSummaryReporter : IDisposable
    {
        private readonly TelemetryRecorder _recorder;
        private readonly Func<float> _clock;
        private readonly Func<string> _matchId;
        private readonly Func<string> _mode;
        private readonly Func<string> _map;
        private float? _startedAt;
        private bool _reported;

        public MatchSummaryReporter(
            TelemetryRecorder recorder, Func<float> clock, Func<string> mode = null, Func<string> map = null, Func<string> matchId = null)
        {
            _recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _mode = mode;
            _map = map;
            _matchId = matchId;
        }

        public bool IsAttached { get; private set; }

        public int Reported { get; private set; }

        public void Attach()
        {
            if (IsAttached)
            {
                return;
            }

            GameSignals.ModeStatus.Changed += Observe;
            IsAttached = true;
            Observe(GameSignals.ModeStatus.Value);
        }

        public void Detach()
        {
            if (!IsAttached)
            {
                return;
            }

            GameSignals.ModeStatus.Changed -= Observe;
            IsAttached = false;
        }

        public void Dispose()
        {
            Detach();
        }

        public void Observe(ModeStatus status)
        {
            if (status == null)
            {
                _startedAt = null;
                _reported = false;
                return;
            }

            if (!status.IsEnded)
            {
                // 終わったあとにまた進み始めたら、同じ場面での次の試合 (再戦・次のラウンドの試合) として数え直す
                if (_startedAt == null || _reported)
                {
                    _startedAt = _clock();
                    _reported = false;
                }

                return;
            }

            // 始まりを見ていない試合は長さが分からないので、誤った長さで残さない
            if (_reported || _startedAt == null)
            {
                return;
            }

            _reported = true;
            var duration = Math.Max(0f, _clock() - _startedAt.Value);
            var summary = new TelemetryMatchSummary(
                _matchId?.Invoke(), _mode?.Invoke(), _map?.Invoke(), duration, status.SideScores, status.Winner, status.OwnSide);
            if (_recorder.RecordMatchSummary(summary))
            {
                Reported++;
            }
        }
    }
}
