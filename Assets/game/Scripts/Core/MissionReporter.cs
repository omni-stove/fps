using System;

namespace Fps.Core
{
    /// <summary>
    /// <see cref="Mission"/> の状態の変化を、タグ付きの 1 行のテキストにして sink に渡す。
    /// 書式は <c>[Mission] objective=&lt;id&gt; state=&lt;state&gt;</c>、<c>[Mission] completed</c>、<c>[Mission] failed</c>。
    /// Dispose すると購読をやめる。
    /// </summary>
    public sealed class MissionReporter : IDisposable
    {
        public const string Tag = "[Mission]";
        public const string CompletedLine = Tag + " completed";
        public const string FailedLine = Tag + " failed";

        private readonly Mission _mission;
        private readonly Action<string> _sink;
        private bool _disposed;

        /// <param name="mission">見張るミッション。</param>
        /// <param name="sink">1 行ずつ受け取る先 (Unity では Debug.Log)。</param>
        public MissionReporter(Mission mission, Action<string> sink)
        {
            _mission = mission ?? throw new ArgumentNullException(nameof(mission));
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));

            _mission.ObjectiveChanged += OnObjectiveChanged;
            _mission.MissionCompleted += OnCompleted;
            _mission.MissionFailed += OnFailed;
        }

        /// <summary>目標 1 つの状態を表す行。</summary>
        public static string FormatObjective(string id, ObjectiveState state)
        {
            if (id == null)
            {
                throw new ArgumentNullException(nameof(id));
            }

            return $"{Tag} objective={id} state={state}";
        }

        /// <summary>
        /// すべての目標の今の状態を定義順に流し、終わっていればミッションの結果も流す。
        /// 開始直後の状態は <see cref="Mission"/> が通知しないので、開始時に 1 度呼ぶ。
        /// </summary>
        public void ReportAll()
        {
            ThrowIfDisposed();

            foreach (var objective in _mission.Objectives)
            {
                _sink(FormatObjective(objective.Id, _mission.GetState(objective.Id)));
            }

            if (_mission.State == MissionState.Completed)
            {
                _sink(CompletedLine);
            }
            else if (_mission.State == MissionState.Failed)
            {
                _sink(FailedLine);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _mission.ObjectiveChanged -= OnObjectiveChanged;
            _mission.MissionCompleted -= OnCompleted;
            _mission.MissionFailed -= OnFailed;
            GC.SuppressFinalize(this);
        }

        private void OnObjectiveChanged(Objective objective, ObjectiveState state)
        {
            _sink(FormatObjective(objective.Id, state));
        }

        private void OnCompleted()
        {
            _sink(CompletedLine);
        }

        private void OnFailed()
        {
            _sink(FailedLine);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(MissionReporter));
            }
        }
    }
}
