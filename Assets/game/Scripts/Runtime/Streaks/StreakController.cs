using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    /// <summary>
    /// プレイヤーに付けるストリークの部品。倒れずに倒した数を <see cref="StreakTracker"/> で数え、得た報酬を UseStreak の入力で使う。
    /// <list type="bullet">
    /// <item><see cref="CombatEvents.Killed"/> のうち、倒した者がこの GameObject (<see cref="CombatEvents.OfflineId"/>) で、自滅でも味方殺しでもないものを数える。
    /// 自分が倒れている間に入った分 (飛んでいる空爆など) は、倒れずに倒した数ではないので数えない。</item>
    /// <item>同じ GameObject の <see cref="Damageable"/> が倒れたら数を 0 に戻す。得て使っていない報酬は残る。</item>
    /// <item>UseStreak の入力 (<see cref="PlayerActionReader.PressedCommands"/> の <see cref="PlayerCommands.UseStreak"/>) で、一番先に得た報酬を
    /// <see cref="UavReward"/> か <see cref="AirstrikeReward"/> で使う (<see cref="TryUse"/>)。</item>
    /// <item>状態が変わるたびに <see cref="GameSignals.Streak"/> に出し、無効になるか破棄されたら自分の出した値だけを消す。</item>
    /// </list>
    /// 値は Play 中に生まれたとき、先に <see cref="Configure"/> で渡されていなければ調整値の JSON (<see cref="Tuning"/>) から読む。
    /// </summary>
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

        /// <summary>
        /// 調整値の JSON (入口の <see cref="GameContent.StreaksTuning"/>、空なら <c>Resources/Tuning/streaks</c>) の値。
        /// 無いか読めなければ <see cref="StreakSpec.Default"/>。
        /// </summary>
        public static StreakSpec Tuning => KitTuning.Load(c => c.StreaksTuning, StreakJson.ResourcePath, StreakJson.Parse, StreakSpec.Default);

        /// <summary>今の値。<see cref="Configure"/> の前は null。</summary>
        public StreakSpec Spec => _tracker?.Spec;

        public bool IsConfigured => _tracker != null;

        /// <summary>今の状態。<see cref="Configure"/> の前は null。</summary>
        public StreakStatus? Status => _tracker?.Status;

        /// <summary>今使える報酬の Id。得た順。</summary>
        public IReadOnlyList<string> ReadyRewardIds => _tracker != null ? _tracker.ReadyRewardIds : (IReadOnlyList<string>)Array.Empty<string>();

        /// <summary>空爆の着弾点を選ぶ乱数。null なら使うたびに新しく作る。テストが種を決めて差し込む。</summary>
        public System.Random Random { get; set; }

        /// <summary>
        /// spec で数え直す。倒した数も得た報酬もすべて捨て、<c>[Streak] id=... rewards=...</c> をログに出して <see cref="GameSignals.Streak"/> に出す。
        /// 有効な間 (Edit Mode では呼んだとき) から <see cref="CombatEvents.Killed"/> と自分の <see cref="Damageable.Died"/> を受ける。
        /// </summary>
        /// <exception cref="ArgumentNullException">spec が null。</exception>
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

        /// <summary>
        /// 一番先に得た報酬を使う。使ったら true を返し、<c>[Streak] id=... used</c> をログに出して <see cref="GameSignals.Streak"/> を出し直す。
        /// 何も得ていない・倒れている・報酬を動かす部品 (UAV なら <see cref="PingController"/>) が無いときは、報酬を残したまま
        /// 理由をログに出して false を返す。空爆の部品 (<see cref="AirstrikeReward"/>) が無ければ足す。
        /// </summary>
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

        /// <summary>
        /// 倒れたことの知らせ 1 件を数える。倒した者が自分で、自滅でも味方殺しでもなく、自分が生きていれば数え、
        /// <c>[Streak] id=... kill</c> と、報酬を得たら <c>[Streak] id=... earned</c> をログに出す。数えたら true。
        /// 有効な間は <see cref="CombatEvents.Killed"/> から呼ばれる。
        /// </summary>
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
