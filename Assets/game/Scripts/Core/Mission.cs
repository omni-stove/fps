using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;

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

        /// <summary>全目標の現在の状態と進み具合を定義順で書き出す。<see cref="Restore"/> に渡すと今の状態に戻る。</summary>
        public IReadOnlyList<ObjectiveProgress> Snapshot()
        {
            var list = new List<ObjectiveProgress>(Objectives.Count);
            foreach (var objective in Objectives)
            {
                list.Add(new ObjectiveProgress(objective.Id, _states[objective.Id], _counts[objective.Id]));
            }

            return new ReadOnlyCollection<ObjectiveProgress>(list);
        }

        /// <summary>
        /// <see cref="Snapshot"/> で書き出した状態に戻す。終了後のミッションにも使え、ミッション全体の状態も計算し直す。
        /// Completed と Failed はそのまま戻し、Locked と Active は前提の状態から決め直す
        /// (前提がすべて完了なら Active、前提が失敗していれば Failed、それ以外は Locked)。
        /// ただし終了したミッションでは、操作で目標が切り替わらないのに合わせて、Locked はそのまま、Active は前提が未完了なら Locked にするだけにする。
        /// 戻すことは操作ではないので、イベントは呼ばない。呼び出し側は戻したあとで状態を読み直す。
        /// </summary>
        /// <param name="progress">
        /// 全目標ちょうど 1 つずつの状態。Completed は進み具合が RequiredCount で前提がすべて完了していること、
        /// それ以外は進み具合が RequiredCount 未満で、前提が未完了なら 0 であること。
        /// 満たさなければ <see cref="ArgumentException"/> を投げ、ミッションは変えない。
        /// </param>
        public void Restore(IEnumerable<ObjectiveProgress> progress)
        {
            if (progress == null)
            {
                throw new ArgumentNullException(nameof(progress));
            }

            var saved = new Dictionary<string, ObjectiveProgress>(StringComparer.Ordinal);
            foreach (var entry in progress)
            {
                if (entry == null)
                {
                    throw new ArgumentException("progress must not contain null", nameof(progress));
                }

                if (!_byId.TryGetValue(entry.Id, out var objective))
                {
                    throw new ArgumentException($"unknown objective id '{entry.Id}'", nameof(progress));
                }

                if (!saved.TryAdd(entry.Id, entry))
                {
                    throw new ArgumentException($"duplicate objective id '{entry.Id}'", nameof(progress));
                }

                if (entry.State == ObjectiveState.Completed ? entry.Count != objective.RequiredCount : entry.Count >= objective.RequiredCount)
                {
                    throw new ArgumentException(
                        $"objective '{entry.Id}' cannot be {entry.State} with count {entry.Count.ToString(CultureInfo.InvariantCulture)}",
                        nameof(progress));
                }
            }

            foreach (var objective in Objectives)
            {
                if (!saved.ContainsKey(objective.Id))
                {
                    throw new ArgumentException($"missing objective id '{objective.Id}'", nameof(progress));
                }
            }

            // 先に Completed と Failed だけでミッション全体の状態を決める。終了済みなら Locked と Active は操作で切り替わらない。
            var states = new Dictionary<string, ObjectiveState>(StringComparer.Ordinal);
            var finished = false;
            var allRequiredCompleted = true;
            foreach (var objective in Objectives)
            {
                var state = saved[objective.Id].State;
                finished |= !objective.Optional && state == ObjectiveState.Failed;
                allRequiredCompleted &= objective.Optional || state == ObjectiveState.Completed;
            }

            finished |= allRequiredCompleted;
            var requiredFailed = false;
            allRequiredCompleted = true;

            // 前提が先に並ぶ順に見るので、前提の状態は決まっている。
            foreach (var objective in _topologicalOrder)
            {
                var entry = saved[objective.Id];
                var prerequisitesCompleted = true;
                var prerequisiteFailed = false;
                foreach (var prerequisite in objective.Prerequisites)
                {
                    prerequisitesCompleted &= states[prerequisite] == ObjectiveState.Completed;
                    prerequisiteFailed |= states[prerequisite] == ObjectiveState.Failed;
                }

                var state = entry.State;
                if (state == ObjectiveState.Completed && !prerequisitesCompleted)
                {
                    throw new ArgumentException(
                        $"objective '{objective.Id}' is completed before its prerequisites", nameof(progress));
                }

                if (state == ObjectiveState.Locked || state == ObjectiveState.Active)
                {
                    if (!prerequisitesCompleted && entry.Count != 0)
                    {
                        throw new ArgumentException(
                            $"objective '{objective.Id}' has progress before its prerequisites are completed", nameof(progress));
                    }

                    if (finished)
                    {
                        state = state == ObjectiveState.Active && prerequisitesCompleted ? ObjectiveState.Active : ObjectiveState.Locked;
                    }
                    else if (prerequisiteFailed)
                    {
                        state = ObjectiveState.Failed;
                    }
                    else
                    {
                        state = prerequisitesCompleted ? ObjectiveState.Active : ObjectiveState.Locked;
                    }
                }

                states.Add(objective.Id, state);
                requiredFailed |= !objective.Optional && state == ObjectiveState.Failed;
                allRequiredCompleted &= objective.Optional || state == ObjectiveState.Completed;
            }

            foreach (var objective in Objectives)
            {
                _states[objective.Id] = states[objective.Id];
                _counts[objective.Id] = saved[objective.Id].Count;
            }

            if (requiredFailed)
            {
                State = MissionState.Failed;
            }
            else if (allRequiredCompleted)
            {
                State = MissionState.Completed;
            }
            else
            {
                State = MissionState.Active;
            }
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
