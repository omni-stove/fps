using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // PlayerActionReader (DefaultExecutionOrder 100) がその LateUpdate で読んだ押下を、同じフレームのうちに使う。
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(110)]
    public sealed class StreakController : MonoBehaviour
    {
        public const string LogTag = StreakLog.LogTag;

        private StreakTracker _tracker;
        private Damageable _self;
        private PlayerActionReader _reader;
        private bool _subscribed;
        private StreakStatus? _published;

        public static StreakSpec Tuning => KitTuning.Load(c => c.StreaksTuning, StreakJson.ResourcePath, StreakJson.Parse, StreakSpec.Default);

        public StreakSpec Spec => _tracker?.Spec;

        public bool IsConfigured => _tracker != null;

        public StreakStatus? Status => _tracker?.Status;

        public IReadOnlyList<string> ReadyRewardIds => _tracker != null ? _tracker.ReadyRewardIds : (IReadOnlyList<string>)Array.Empty<string>();

        public System.Random Random { get; set; }

        // Play 中に付けると Awake で数え始めて自分の Damageable を探すので、Damageable を付けた後に呼ぶ。
        public static StreakController Apply(GameObject player, GameRules rules)
        {
            if (player == null)
            {
                throw new ArgumentNullException(nameof(player));
            }

            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            var streaks = player.GetComponent<StreakController>();
            if (rules.Streaks != Streaks.On)
            {
                if (streaks != null)
                {
                    streaks.enabled = false;

                    // Edit Mode では OnDisable が呼ばれないので、受けるのと出すのをここで止める。
                    if (!Application.isPlaying)
                    {
                        streaks.Unsubscribe();
                        streaks.Unpublish();
                    }
                }

                return null;
            }

            if (streaks == null)
            {
                return player.AddComponent<StreakController>();
            }

            var wasEnabled = streaks.enabled;
            streaks.enabled = true;

            // Edit Mode では OnEnable が呼ばれないので、数え始めていればここで受け直す。
            if (!wasEnabled && !Application.isPlaying && streaks._tracker != null)
            {
                streaks.Subscribe();
                streaks.Publish();
            }

            return streaks;
        }

        public void Configure(StreakSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            Unsubscribe();
            _tracker = new StreakTracker(spec);
            _self = GetComponent<Damageable>();
            _reader = GetComponent<PlayerActionReader>();
            Debug.Log(StreakLog.Configured(LogId, spec), this);

            // Edit Mode では OnEnable が呼ばれないので、ここで受け始める。
            if (!Application.isPlaying || (enabled && gameObject.activeInHierarchy))
            {
                Subscribe();
                Publish();
            }
        }

        public bool TryUse()
        {
            if (_tracker == null)
            {
                return false;
            }

            var ready = _tracker.ReadyRewardIds;
            if (ready.Count == 0)
            {
                Debug.Log(StreakLog.NothingToUse(LogId), this);
                return false;
            }

            if (_self != null && _self.IsDead)
            {
                Debug.Log(StreakLog.UseRefusedDead(LogId), this);
                return false;
            }

            // 動かせないまま報酬を取り去らないよう、使うと決める前に動かす部品を確かめる。
            var isUav = string.Equals(ready[0], StreakSpec.UavRewardId, StringComparison.Ordinal);
            var pings = isUav ? GetComponent<PingController>() : null;
            if (isUav && pings == null)
            {
                Debug.LogWarning(StreakLog.RewardUnavailable(LogId, ready[0], nameof(PingController)), this);
                return false;
            }

            var status = _tracker.Use(out var used);
            Debug.Log(StreakLog.Used(LogId, used, status), this);
            Publish();

            var spec = _tracker.Spec;
            if (isUav)
            {
                UavReward.Use(gameObject, pings, spec.Uav.DurationSeconds);
            }
            else if (string.Equals(used, StreakSpec.AirstrikeRewardId, StringComparison.Ordinal))
            {
                var airstrike = GetComponent<AirstrikeReward>();
                if (airstrike == null)
                {
                    airstrike = gameObject.AddComponent<AirstrikeReward>();
                }

                var strike = spec.Airstrike;
                airstrike.CallAtAim(
                    strike.Range, strike.ShellCount, strike.IntervalSeconds, strike.Spread, strike.Explosion, used, Random ?? new System.Random());
            }

            return true;
        }

        // 自分が倒れている間に入った分 (飛んでいる空爆など) は、倒れずに倒した数ではないので数えない。
        public bool HandleKilled(KillFeedEntry entry)
        {
            if (_tracker == null || !IsOwnKill(entry))
            {
                return false;
            }

            var status = _tracker.Killed(out var earned);
            Debug.Log(StreakLog.Killed(LogId, status), this);
            foreach (var rewardId in earned)
            {
                Debug.Log(StreakLog.Earned(LogId, rewardId, status), this);
            }

            Publish();
            return true;
        }

        private string LogId => _self != null ? _self.Id : name;

        private bool IsOwnKill(KillFeedEntry entry)
        {
            var selfId = CombatEvents.OfflineId(gameObject);
            if (entry.KillerId != selfId || entry.IsSuicide || entry.Friendly)
            {
                return false;
            }

            if (_self != null && _self.IsDead)
            {
                return false;
            }

            // オフラインの Damageable は Friendly を立てないので、陣営を見比べて味方殺しを外す。
            var victim = CombatEvents.OfflineObject(entry.VictimId);
            var mine = SideMember.Of(gameObject);
            var theirs = victim != null ? SideMember.Of(victim) : null;
            return mine == null || theirs == null || !mine.IsAllyOf(theirs);
        }

        private void OnSelfDied(Damageable self)
        {
            if (_tracker == null)
            {
                return;
            }

            var lost = _tracker.Kills;
            var status = _tracker.Died();
            Debug.Log(StreakLog.Died(LogId, lost, status), this);
            Publish();
        }

        private void Awake()
        {
            // 無効なまま置かれた本体は Awake が有効になるまで遅れるので、その間に Play 中のコードが Configure で渡した値を JSON で消さない。
            if (Application.isPlaying && _tracker == null)
            {
                Configure(Tuning);
            }
        }

        private void OnEnable()
        {
            if (_tracker == null)
            {
                return;
            }

            Subscribe();
            Publish();
        }

        private void OnDisable()
        {
            Unsubscribe();
            Unpublish();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            Unpublish();
        }

        private void LateUpdate()
        {
            if (_tracker == null)
            {
                return;
            }

            if (_reader == null)
            {
                _reader = GetComponent<PlayerActionReader>();
            }

            // ポーズ画面のボタンを押したつもりで報酬を使わないよう、開いている間は読まない。
            if (_reader != null
                && _reader.isActiveAndEnabled
                && (_reader.PressedCommands & PlayerCommands.UseStreak) != 0
                && !GameUi.IsPauseOpen)
            {
                TryUse();
            }
        }

        private void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            CombatEvents.Killed += OnKilled;
            if (_self != null)
            {
                _self.Died += OnSelfDied;
            }

            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            CombatEvents.Killed -= OnKilled;
            if (_self != null)
            {
                _self.Died -= OnSelfDied;
            }

            _subscribed = false;
        }

        private void OnKilled(KillFeedEntry entry)
        {
            HandleKilled(entry);
        }

        private void Publish()
        {
            _published = _tracker.Status;
            GameSignals.Streak.Publish(_published);
        }

        // 後から別のプレイヤーが出した状態までは消さない。
        private void Unpublish()
        {
            if (_published.HasValue)
            {
                GameSignals.Streak.ClearIfCurrent(_published);
                _published = null;
            }
        }
    }
}
