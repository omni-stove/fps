using System;

namespace Shooter.Core
{
    public enum ObjectiveZoneMode
    {
        Reach,

        Interact,
    }

    public sealed class ObjectiveZone : IDisposable
    {
        private readonly Mission _mission;
        private bool _disposed;

        public string ObjectiveId { get; }

        public ObjectiveZoneMode Mode { get; }

        public int Occupants { get; private set; }

        public bool IsOccupied => Occupants > 0;

        public bool IsActive => !_disposed && !_mission.IsFinished && _mission.GetState(ObjectiveId) == ObjectiveState.Active;

        public ObjectiveZone(Mission mission, string objectiveId, ObjectiveZoneMode mode)
        {
            _mission = mission ?? throw new ArgumentNullException(nameof(mission));
            if (objectiveId == null)
            {
                throw new ArgumentNullException(nameof(objectiveId));
            }

            if (!mission.HasObjective(objectiveId))
            {
                throw new ArgumentException($"mission has no objective '{objectiveId}'", nameof(objectiveId));
            }

            if (!Enum.IsDefined(typeof(ObjectiveZoneMode), mode))
            {
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "unknown zone mode");
            }

            ObjectiveId = objectiveId;
            Mode = mode;
            _mission.ObjectiveChanged += OnObjectiveChanged;
        }

        public void Enter()
        {
            if (_disposed)
            {
                return;
            }

            Occupants++;
            if (Mode == ObjectiveZoneMode.Reach)
            {
                TryProgress();
            }
        }

        public void Exit()
        {
            if (_disposed || Occupants == 0)
            {
                return;
            }

            Occupants--;
        }

        public bool Interact()
        {
            if (_disposed || Mode != ObjectiveZoneMode.Interact || !IsOccupied)
            {
                return false;
            }

            return TryProgress();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Occupants = 0;
            _mission.ObjectiveChanged -= OnObjectiveChanged;
            GC.SuppressFinalize(this);
        }

        private bool TryProgress()
        {
            if (_mission.IsFinished || _mission.GetState(ObjectiveId) != ObjectiveState.Active)
            {
                return false;
            }

            _mission.Progress(ObjectiveId);
            return true;
        }

        // 中にいるまま Active になった時点でも進めるのは、Locked の間の侵入を取りこぼすと、出入りし直さない限り進められなくなるため。
        private void OnObjectiveChanged(Objective objective, ObjectiveState state)
        {
            if (_disposed || Mode != ObjectiveZoneMode.Reach || !IsOccupied || state != ObjectiveState.Active)
            {
                return;
            }

            if (string.Equals(objective.Id, ObjectiveId, StringComparison.Ordinal))
            {
                TryProgress();
            }
        }
    }
}
