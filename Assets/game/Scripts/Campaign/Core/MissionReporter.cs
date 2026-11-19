using System;

namespace Shooter.Core
{
    public sealed class MissionReporter : IDisposable
    {
        public const string Tag = "[Mission]";
        public const string CompletedLine = Tag + " completed";
        public const string FailedLine = Tag + " failed";

        private readonly Mission _mission;
        private readonly Action<string> _sink;
        private bool _disposed;

        public MissionReporter(Mission mission, Action<string> sink)
        {
            _mission = mission ?? throw new ArgumentNullException(nameof(mission));
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));

            _mission.ObjectiveChanged += OnObjectiveChanged;
            _mission.MissionCompleted += OnCompleted;
            _mission.MissionFailed += OnFailed;
        }

        public static string FormatObjective(string id, ObjectiveState state)
        {
            if (id == null)
            {
                throw new ArgumentNullException(nameof(id));
            }

            return $"{Tag} objective={id} state={state}";
        }

        // 開始直後の状態は Mission が通知しないので、開始時に 1 度呼んで今の状態を流す。
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
