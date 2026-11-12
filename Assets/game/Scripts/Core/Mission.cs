using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Fps.Core
{
    /// <summary>ミッション全体の状態。</summary>
    public enum MissionState
    {
        /// <summary>進行中。</summary>
        Active,

        /// <summary>必須の目標がすべて完了した。</summary>
        Completed,

        /// <summary>必須の目標が失敗した。</summary>
        Failed,
    }

    /// <summary>
    /// キャンペーンの 1 ミッション。目標の状態と進み具合を管理する。
    /// 前提の無い目標は Active、ある目標は Locked で始まり、前提がすべて完了すると Active になる。
    /// 必須の目標がすべて完了したら Completed、どれかが失敗したら Failed になり、以後の操作は無視する。
    /// </summary>
    /// <remarks>
    /// 通知は 1 回の操作で変わった状態をすべて反映してから、変わった順に呼ぶ。
    /// ミッションが終わる操作では、目標の変化の通知のあとに <see cref="MissionCompleted"/> か <see cref="MissionFailed"/> を呼ぶ。
    /// </remarks>
    public sealed class Mission
    {
        private readonly Dictionary<string, Objective> _byId;
        private readonly Dictionary<string, ObjectiveState> _states;
        private readonly Dictionary<string, int> _counts;
        private readonly List<Objective> _topologicalOrder;

        /// <summary>目標の状態が変わるたびに、目標と新しい状態を渡して呼ばれる。</summary>
        public event Action<Objective, ObjectiveState> ObjectiveChanged;

        /// <summary>ミッションが完了した瞬間に 1 度だけ呼ばれる。</summary>
        public event Action MissionCompleted;

        /// <summary>ミッションが失敗した瞬間に 1 度だけ呼ばれる。</summary>
        public event Action MissionFailed;

        /// <summary>定義順の目標。</summary>
        public IReadOnlyList<Objective> Objectives { get; }

        public MissionState State { get; private set; }
        public bool IsFinished => State != MissionState.Active;

        /// <param name="objectives">
        /// 目標の定義。1 つ以上の必須の目標を含み、Id が重複せず、前提はすべて同じミッションの目標を指し、依存が循環しないこと。
        /// </param>
        public Mission(IEnumerable<Objective> objectives)
        {
            if (objectives == null)
            {
                throw new ArgumentNullException(nameof(objectives));
            }

            var list = new List<Objective>();
            _byId = new Dictionary<string, Objective>(StringComparer.Ordinal);
            var hasRequired = false;
            foreach (var objective in objectives)
            {
                if (objective == null)
                {
                    throw new ArgumentException("objectives must not contain null", nameof(objectives));
                }

                if (_byId.ContainsKey(objective.Id))
                {
                    throw new ArgumentException($"duplicate objective id '{objective.Id}'", nameof(objectives));
                }

                _byId.Add(objective.Id, objective);
                list.Add(objective);
                hasRequired |= !objective.Optional;
            }

            if (!hasRequired)
            {
                throw new ArgumentException("mission must have at least one required objective", nameof(objectives));
            }

            foreach (var objective in list)
            {
                foreach (var prerequisite in objective.Prerequisites)
                {
                    if (!_byId.ContainsKey(prerequisite))
                    {
                        throw new ArgumentException(
                            $"objective '{objective.Id}' depends on unknown objective '{prerequisite}'", nameof(objectives));
                    }
                }
            }

            _topologicalOrder = SortTopologically(list);
            Objectives = new ReadOnlyCollection<Objective>(list);

            _states = new Dictionary<string, ObjectiveState>(StringComparer.Ordinal);
            _counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var objective in list)
            {
                _states.Add(objective.Id, objective.Prerequisites.Count == 0 ? ObjectiveState.Active : ObjectiveState.Locked);
                _counts.Add(objective.Id, 0);
            }

            State = MissionState.Active;
        }

        /// <summary>id の目標がこのミッションにあれば true。null は false。</summary>
        public bool HasObjective(string id)
        {
            return id != null && _byId.ContainsKey(id);
        }

        /// <summary>目標の現在の状態。</summary>
        public ObjectiveState GetState(string id)
        {
            return _states[Find(id).Id];
        }

        /// <summary>目標の現在の進み具合。0 以上 RequiredCount 以下。</summary>
        public int GetCount(string id)
        {
            return _counts[Find(id).Id];
        }

        /// <summary>
        /// Active な目標を amount 進める。RequiredCount に届いたら Completed にし、前提がそろった目標を Active にする。
        /// Active でない目標や、終了後のミッションでは何もしない。
        /// </summary>
        public void Progress(string id, int amount = 1)
        {
            var objective = Find(id);
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "amount must not be negative");
            }

            if (IsFinished || _states[objective.Id] != ObjectiveState.Active || amount == 0)
            {
                return;
            }

            var count = (int)Math.Min((long)_counts[objective.Id] + amount, objective.RequiredCount);
            _counts[objective.Id] = count;
            if (count < objective.RequiredCount)
            {
                return;
            }

            var changes = new List<Objective> { objective };
            _states[objective.Id] = ObjectiveState.Completed;

            if (AllRequiredCompleted())
            {
                State = MissionState.Completed;
            }
            else
            {
                foreach (var candidate in Objectives)
                {
                    if (_states[candidate.Id] == ObjectiveState.Locked && PrerequisitesCompleted(candidate))
                    {
                        _states[candidate.Id] = ObjectiveState.Active;
                        changes.Add(candidate);
                    }
                }
            }

            Notify(changes);
        }

        /// <summary>
        /// Locked か Active の目標を失敗にする。それを前提にしていて Locked のままの目標は、達成できなくなるので一緒に失敗にする。
        /// 必須の目標が失敗したらミッション全体が Failed になる。完了・失敗済みの目標や、終了後のミッションでは何もしない。
        /// </summary>
        public void Fail(string id)
        {
            var objective = Find(id);
            if (IsFinished)
            {
                return;
            }

            var state = _states[objective.Id];
            if (state != ObjectiveState.Locked && state != ObjectiveState.Active)
            {
                return;
            }

            var changes = new List<Objective> { objective };
            _states[objective.Id] = ObjectiveState.Failed;
            var requiredFailed = !objective.Optional;

            if (!requiredFailed)
            {
                // 前提が先に並ぶ順に見るので、1 周で失敗が依存の先まで伝わる。
                foreach (var candidate in _topologicalOrder)
                {
                    if (_states[candidate.Id] == ObjectiveState.Locked && AnyPrerequisiteFailed(candidate))
                    {
                        _states[candidate.Id] = ObjectiveState.Failed;
                        changes.Add(candidate);
                        requiredFailed |= !candidate.Optional;
                    }
                }
            }

            if (requiredFailed)
            {
                State = MissionState.Failed;
            }

            Notify(changes);
        }

        private Objective Find(string id)
        {
            if (id == null)
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (!_byId.TryGetValue(id, out var objective))
            {
                throw new ArgumentException($"unknown objective id '{id}'", nameof(id));
            }

            return objective;
        }

        private bool AllRequiredCompleted()
        {
            foreach (var objective in Objectives)
            {
                if (!objective.Optional && _states[objective.Id] != ObjectiveState.Completed)
                {
                    return false;
                }
            }

            return true;
        }

        private bool PrerequisitesCompleted(Objective objective)
        {
            foreach (var prerequisite in objective.Prerequisites)
            {
                if (_states[prerequisite] != ObjectiveState.Completed)
                {
                    return false;
                }
            }

            return true;
        }

        private bool AnyPrerequisiteFailed(Objective objective)
        {
            foreach (var prerequisite in objective.Prerequisites)
            {
                if (_states[prerequisite] == ObjectiveState.Failed)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>状態の変化をすべて反映したあとで呼ぶ。通知の中から操作されても、この操作の結果は崩れない。</summary>
        private void Notify(List<Objective> changes)
        {
            var finalState = State;
            var states = new ObjectiveState[changes.Count];
            for (var i = 0; i < changes.Count; i++)
            {
                states[i] = _states[changes[i].Id];
            }

            for (var i = 0; i < changes.Count; i++)
            {
                ObjectiveChanged?.Invoke(changes[i], states[i]);
            }

            if (finalState == MissionState.Completed)
            {
                MissionCompleted?.Invoke();
            }
            else if (finalState == MissionState.Failed)
            {
                MissionFailed?.Invoke();
            }
        }

        /// <summary>前提が先に並ぶ順に並べる (Kahn 法、同順位は定義順)。循環していたら例外にする。</summary>
        private static List<Objective> SortTopologically(List<Objective> objectives)
        {
            var remaining = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var objective in objectives)
            {
                remaining.Add(objective.Id, objective.Prerequisites.Count);
            }

            var sorted = new List<Objective>(objectives.Count);
            var placed = new HashSet<string>(StringComparer.Ordinal);
            var progressed = true;
            while (progressed)
            {
                progressed = false;
                foreach (var objective in objectives)
                {
                    if (placed.Contains(objective.Id) || remaining[objective.Id] > 0)
                    {
                        continue;
                    }

                    placed.Add(objective.Id);
                    sorted.Add(objective);
                    progressed = true;
                    foreach (var dependent in objectives)
                    {
                        foreach (var prerequisite in dependent.Prerequisites)
                        {
                            if (prerequisite == objective.Id)
                            {
                                remaining[dependent.Id]--;
                            }
                        }
                    }
                }
            }

            if (sorted.Count != objectives.Count)
            {
                var cyclic = new List<string>();
                foreach (var objective in objectives)
                {
                    if (!placed.Contains(objective.Id))
                    {
                        cyclic.Add(objective.Id);
                    }
                }

                throw new ArgumentException(
                    $"objective prerequisites form a cycle among: {string.Join(", ", cyclic)}", nameof(objectives));
            }

            return sorted;
        }
    }
}
