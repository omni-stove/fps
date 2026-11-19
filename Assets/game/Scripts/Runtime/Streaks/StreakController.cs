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
        private StreakTargeting _targeting;
        private int _targetingEndedFrame = -1;

        public static StreakSpec Tuning => KitTuning.Load(c => c.StreaksTuning, StreakJson.ResourcePath, StreakJson.Parse, StreakSpec.Default);

        public StreakSpec Spec => _tracker?.Spec;

        public bool IsConfigured => _tracker != null;

        public StreakStatus? Status => _tracker?.Status;

        public IReadOnlyList<string> ReadyRewardIds => _tracker != null ? _tracker.ReadyRewardIds : (IReadOnlyList<string>)Array.Empty<string>();

        public string SelectedRewardId => _tracker?.SelectedRewardId;

        public System.Random Random { get; set; }

        public bool IsTargeting => _targeting != null && _targeting.IsActive;

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

            var rewardId = _tracker.SelectedRewardId;
            if (rewardId == null)
            {
                Debug.Log(StreakLog.NothingToUse(LogId), this);
                return false;
            }

            if (_self != null && _self.IsDead)
            {
                Debug.Log(StreakLog.UseRefusedDead(LogId), this);
                return false;
            }

            // 地点を選ばせる報酬は、選び終えるまで枠を空けない。選ぶのをやめても報酬は残る。
            if (StreakSpec.NeedsTarget(rewardId))
            {
                return BeginTargeting(rewardId);
            }

            // 動かせないまま報酬を取り去らないよう、使うと決める前に動かす部品を確かめる。
            var isUav = string.Equals(rewardId, StreakSpec.UavRewardId, StringComparison.Ordinal);
            var pings = isUav ? GetComponent<PingController>() : null;
            if (isUav && pings == null)
            {
                Debug.LogWarning(StreakLog.RewardUnavailable(LogId, rewardId, nameof(PingController)), this);
                return false;
            }

            var status = _tracker.Use(rewardId, out _);
            Debug.Log(StreakLog.Used(LogId, rewardId, status), this);
            Publish();

            var spec = _tracker.Spec;
            if (isUav)
            {
                UavReward.Use(gameObject, pings, spec.Uav.DurationSeconds);
            }

            return true;
        }

        // 選んでいる間に倒れたり、その報酬がもう無くなったりしたら、選んだ地点では使わない。
        public bool UseAt(StreakTargetingStatus target)
        {
            if (_tracker == null)
            {
                return false;
            }

            if (!_tracker.IsReady(target.RewardId))
            {
                Debug.Log(StreakLog.NothingToUse(LogId), this);
                return false;
            }

            if (_self != null && _self.IsDead)
            {
                Debug.Log(StreakLog.UseRefusedDead(LogId), this);
                return false;
            }

            var used = target.RewardId;
            var status = _tracker.Use(used, out _);
            Debug.Log(StreakLog.Used(LogId, used, status), this);
            Publish();

            if (string.Equals(used, StreakSpec.AirstrikeRewardId, StringComparison.Ordinal))
            {
                var airstrike = GetComponent<AirstrikeReward>();
                if (airstrike == null)
                {
                    airstrike = gameObject.AddComponent<AirstrikeReward>();
                }

                airstrike.Call(target.Target.ToUnity(), target.HeadingDegrees, _tracker.Spec.Airstrike, used, Random ?? new System.Random(), _tracker.Spec.Targeting);
            }

            return true;
        }

        public bool CancelTargeting(string reason)
        {
            return _targeting != null && _targeting.Cancel(reason);
        }

        // 地点を選んでいる間は、選んでいる報酬を替えると狙いの案内と食い違うので替えない。
        public bool SelectNext()
        {
            if (_tracker == null || IsTargeting || !_tracker.SelectNext())
            {
                return false;
            }

            Debug.Log(StreakLog.Selected(LogId, _tracker.SelectedRewardId, _tracker.Status), this);
            Publish();
            return true;
        }

        private bool BeginTargeting(string rewardId)
        {
            if (IsTargeting)
            {
                return false;
            }

            if (_targeting == null)
            {
                _targeting = GetComponent<StreakTargeting>();
                if (_targeting == null)
                {
                    _targeting = gameObject.AddComponent<StreakTargeting>();
                }

                _targeting.Confirmed += OnTargetConfirmed;
                _targeting.Cancelled += OnTargetCancelled;
            }

            var spec = _tracker.Spec;
            var reward = spec.FindReward(rewardId);
            var mover = GetComponent<PlayerController>();
            var weapon = GetComponentInChildren<WeaponController>(true);
            var look = mover != null ? mover.Look : GetComponent<FirstPersonLook>();
            var heading = look != null && look.IsInitialized ? look.Yaw : transform.eulerAngles.y;
            var handoffs = new List<ControlHandoff>(2);
            if (mover != null)
            {
                handoffs.Add(mover.Handoff);
            }

            // オフラインでは移動と武器が同じ口を共有している (GameBootstrap) ので、別のときだけ武器の口も握る。
            if (weapon != null && weapon.Handoff != null && (mover == null || !ReferenceEquals(weapon.Handoff, mover.Handoff)))
            {
                handoffs.Add(weapon.Handoff);
            }

            var aim = new StreakAim(
                rewardId,
                reward != null ? reward.DisplayNameKey : string.Empty,
                transform.position.ToNumerics(),
                heading,
                spec.Airstrike.Range,
                spec.Airstrike.LineLength);
            _targeting.LogId = LogId;
            return _targeting.Begin(aim, spec.Targeting, handoffs, look);
        }

        private void OnTargetConfirmed(StreakTargetingStatus target)
        {
            _targetingEndedFrame = Time.frameCount;
            UseAt(target);
        }

        private void OnTargetCancelled(string rewardId, string reason)
        {
            _targetingEndedFrame = Time.frameCount;
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

            CancelTargeting(StreakTargeting.CancelledByDeath);
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
            CancelTargeting(StreakTargeting.CancelledByRules);
            Unsubscribe();
            Unpublish();
        }

        private void OnDestroy()
        {
            CancelTargeting(StreakTargeting.CancelledByRules);
            if (_targeting != null)
            {
                _targeting.Confirmed -= OnTargetConfirmed;
                _targeting.Cancelled -= OnTargetCancelled;
            }

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

            // 選んでいる間と選び終えたフレームの UseStreak は StreakTargeting が取消に使うので、ここでは読まない。
            if (IsTargeting || Time.frameCount == _targetingEndedFrame)
            {
                return;
            }

            // ポーズ画面のボタンを押したつもりで報酬を使わないよう、開いている間は読まない。
            if (_reader == null || !_reader.isActiveAndEnabled || GameUi.IsPauseOpen)
            {
                return;
            }

            var pressed = _reader.PressedCommands;
            if ((pressed & PlayerCommands.SwitchStreak) != 0)
            {
                SelectNext();
            }

            if ((pressed & PlayerCommands.UseStreak) != 0)
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
