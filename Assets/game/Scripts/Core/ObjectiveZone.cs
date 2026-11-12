using System;

namespace Fps.Core
{
    /// <summary><see cref="ObjectiveZone"/> が目標を進める条件。</summary>
    public enum ObjectiveZoneMode
    {
        /// <summary>プレイヤーが範囲に入ったら進める。</summary>
        Reach,

        /// <summary>プレイヤーが範囲の中で操作したら進める。</summary>
        Interact,
    }

    /// <summary>
    /// 範囲に結び付いた目標の判断。範囲の中にいるプレイヤーの数を数え、条件がそろったら <see cref="Mission.Progress"/> を呼ぶ。
    /// <see cref="ObjectiveZoneMode.Reach"/> では、目標がまだ Locked の間に入っていても、中にいるまま Active になった時点で進める
    /// (Locked の間の侵入を取りこぼすと、出入りし直さない限り進められなくなるため)。
    /// Dispose するとミッションの購読をやめ、以後の入力を無視する。
    /// </summary>
    public sealed class ObjectiveZone : IDisposable
    {
        private readonly Mission _mission;
        private bool _disposed;

        /// <summary>進める目標の Id。</summary>
        public string ObjectiveId { get; }

        public ObjectiveZoneMode Mode { get; }

        /// <summary>範囲の中にいるプレイヤーの数。</summary>
        public int Occupants { get; private set; }

        public bool IsOccupied => Occupants > 0;

        /// <param name="mission">進めるミッション。</param>
        /// <param name="objectiveId">mission にある目標の Id。</param>
        /// <param name="mode">進める条件。</param>
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

        /// <summary>プレイヤーが範囲に入った。Reach なら目標が Active のとき進める。</summary>
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

        /// <summary>プレイヤーが範囲から出た。入った数より多く呼ばれても 0 未満にはしない。</summary>
        public void Exit()
        {
            if (_disposed || Occupants == 0)
            {
                return;
            }

            Occupants--;
        }

        /// <summary>
        /// プレイヤーが操作した。Interact で範囲の中にいて、目標が Active なら進めて true を返す。
        /// </summary>
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
