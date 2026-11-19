using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.Runtime
{
    // プロフィールを自分で読み書きしないのは、ProgressionRecorder と別々に保存すると、後に書いた方が先の分を消すため。
    public sealed class AchievementRecorder
    {
        private readonly ProgressionRecorder _progression;
        private readonly Func<double> _clock;
        private readonly List<AchievementDefinition> _unlocked = new List<AchievementDefinition>();

        // 試合の始まりの知らせは無いので、試合の終わりの後で初めて戦いが起きた時刻から数える。
        private double? _matchStartedAt;

        // 1 秒に満たない端数を次の試合へ持ち越し、試合ごとの切り捨てで遊んだ時間が目減りしないようにする。
        private double _carrySeconds;

        private IReadOnlyList<AchievementProgress> _published;

        public AchievementRecorder(ProgressionRecorder progression)
            : this(progression, LoadSpec(), () => Time.realtimeSinceStartupAsDouble)
        {
        }

        public AchievementRecorder(ProgressionRecorder progression, AchievementSpec spec, Func<double> clock)
        {
            _progression = progression ?? throw new ArgumentNullException(nameof(progression));
            Tracker = new AchievementTracker(spec ?? throw new ArgumentNullException(nameof(spec)));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public event Action<AchievementDefinition> Unlocked;

        public ProgressionRecorder Progression => _progression;

        public AchievementTracker Tracker { get; }

        public bool IsInstalled { get; private set; }

        public IReadOnlyList<AchievementProgress> Progress => Tracker.Progress(_progression.Profile);

        public static AchievementSpec LoadSpec()
        {
            return KitTuning.Load(c => c.AchievementsTuning, KitTuning.AchievementsResourcePath, AchievementJson.Parse, AchievementSpec.Default);
        }

        public void Install()
        {
            if (IsInstalled)
            {
                return;
            }

            if (!_progression.IsInstalled)
            {
                throw new InvalidOperationException("install the progression recorder before the achievement recorder");
            }

            Debug.Log(AchievementLog.Configured(Tracker.Spec));
            CombatEvents.Killed += OnKilled;
            CombatEvents.ShotFired += OnShotFired;
            CombatEvents.DamageTaken += OnDamageTaken;
            _progression.ProfileChanged += OnProfileChanged;
            _progression.MatchRecording += OnMatchRecording;
            SceneManager.sceneLoaded += OnSceneLoaded;
            _matchStartedAt = null;
            _carrySeconds = 0d;
            IsInstalled = true;

            // 版 3 までの保存や目標を下げた定義では、読んだ時点で条件を満たしているものがあるので、まず照らし直す。
            UnlockReached();
            Publish();
        }

        public void Uninstall()
        {
            if (!IsInstalled)
            {
                return;
            }

            CombatEvents.Killed -= OnKilled;
            CombatEvents.ShotFired -= OnShotFired;
            CombatEvents.DamageTaken -= OnDamageTaken;
            _progression.ProfileChanged -= OnProfileChanged;
            _progression.MatchRecording -= OnMatchRecording;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            ProgressionSignals.Achievements.ClearIfCurrent(_published);
            _published = null;
            IsInstalled = false;
        }

        // MatchFinished を出さずにメニューへ戻る経路があるため、シーンの読み込みで時計を止め、試合の外の時間を数えない。
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            StopMatchClock();
        }

        // SceneManager.sceneLoaded はテストから起こせないので、中身をここへ分けてテストが直接呼ぶ。
        internal void StopMatchClock()
        {
            _matchStartedAt = null;
        }

        private void OnKilled(KillFeedEntry entry)
        {
            StartMatchClock();

            // 進み具合の記録係だけが先に外れたときは、書き換えても保存されないので数えない。
            if (!_progression.IsInstalled || entry.IsSuicide || entry.Friendly || !IsLocal(entry.KillerId))
            {
                return;
            }

            _progression.Change(profile => Tracker.RecordKill(profile, entry.Headshot, entry.WeaponId));
        }

        private void OnShotFired(ShotFiredInfo shot)
        {
            StartMatchClock();
        }

        private void OnDamageTaken(DamageTakenInfo damage)
        {
            StartMatchClock();
        }

        private void StartMatchClock()
        {
            if (_matchStartedAt == null)
            {
                _matchStartedAt = _clock();
            }
        }

        private void OnMatchRecording(MatchResult result)
        {
            if (_matchStartedAt == null)
            {
                return;
            }

            // 時計が戻ることは無いはずだが、戻っても遊んだ時間を減らさないよう 0 で止める。
            var elapsed = Math.Max(0d, _clock() - _matchStartedAt.Value) + _carrySeconds;
            _matchStartedAt = null;
            var whole = Math.Floor(elapsed);
            _carrySeconds = elapsed - whole;
            if (whole < 1d)
            {
                return;
            }

            _progression.Change(profile => AchievementTracker.RecordPlayTime(profile, whole));
            Debug.Log(AchievementLog.PlayTime(whole, _progression.Profile.PlaySeconds));
        }

        private void OnProfileChanged(PlayerProfile profile)
        {
            UnlockReached();
            Publish();
        }

        private void UnlockReached()
        {
            _unlocked.Clear();
            var next = Tracker.Unlock(_progression.Profile, _unlocked);
            if (_unlocked.Count == 0)
            {
                return;
            }

            // 解けた分を先に写しておくのは、Change が知らせる ProfileChanged からここへ戻ってきて _unlocked を空にするため。
            var unlocked = _unlocked.ToArray();
            _progression.Change(_ => next);
            foreach (var achievement in unlocked)
            {
                Debug.Log(AchievementLog.Unlocked(achievement));
                RaiseUnlocked(achievement);
            }
        }

        private void RaiseUnlocked(AchievementDefinition achievement)
        {
            var handlers = Unlocked;
            if (handlers == null)
            {
                return;
            }

            foreach (var handler in handlers.GetInvocationList())
            {
                try
                {
                    ((Action<AchievementDefinition>)handler)(achievement);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        private void Publish()
        {
            var progress = Tracker.Progress(_progression.Profile);
            if (_published != null && SameProgress(_published, progress))
            {
                return;
            }

            _published = progress;
            ProgressionSignals.Achievements.Publish(progress);
        }

        private bool IsLocal(int id)
        {
            return _progression.Participants.TryGet(id, out var participant) && participant.IsLocal;
        }

        private static bool SameProgress(IReadOnlyList<AchievementProgress> a, IReadOnlyList<AchievementProgress> b)
        {
            if (a.Count != b.Count)
            {
                return false;
            }

            for (var i = 0; i < a.Count; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
