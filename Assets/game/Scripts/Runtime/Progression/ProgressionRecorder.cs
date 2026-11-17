using System;
using System.IO;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    /// <summary>
    /// 自分の成長を記録する。<see cref="Install"/> で <see cref="ProfileStore"/> からプロフィールを読み、
    /// <see cref="CombatEvents.Killed"/> のうち自分 (<see cref="Participant.IsLocal"/> の者) が倒したものと
    /// <see cref="CombatEvents.MatchFinished"/> を <see cref="ProgressionLedger"/> に渡す。試合が終わるたびに保存する。
    /// 自滅と味方撃ちは数えない。<see cref="Uninstall"/> で購読を外す。
    /// </summary>
    public sealed class ProgressionRecorder
    {
        public const string ResourcePath = "Tuning/progression";

        private readonly ProfileStore _store;
        private readonly IParticipants _participants;

        /// <summary>
        /// storage に保存し、成長の調整値を <see cref="LoadSpec"/> で、倒した者を <see cref="OfflineParticipants"/>
        /// (<see cref="Damageable.IsLocalPlayer"/> の者に振った <see cref="CombatEvents.OfflineId"/>) で引く。
        /// </summary>
        /// <exception cref="ArgumentNullException">storage が null。</exception>
        public ProgressionRecorder(ISaveStorage storage)
            : this(storage, LoadSpec(), new OfflineParticipants())
        {
        }

        /// <exception cref="ArgumentNullException">どれかが null。</exception>
        public ProgressionRecorder(ISaveStorage storage, ProgressionSpec spec, IParticipants participants)
        {
            _store = new ProfileStore(storage);
            Ledger = new ProgressionLedger(spec);
            _participants = participants ?? throw new ArgumentNullException(nameof(participants));
        }

        public ProgressionLedger Ledger { get; }

        /// <summary>今のプロフィール。<see cref="Install"/> の前は <see cref="PlayerProfile.Empty"/>。</summary>
        public PlayerProfile Profile { get; private set; } = PlayerProfile.Empty;

        public int Level => Ledger.LevelOf(Profile);

        public bool IsInstalled { get; private set; }

        /// <summary>今のプロフィールのレベルで id (武器・アタッチメント) が使えるか (<see cref="ProgressionLedger.IsUnlocked(string, PlayerProfile)"/>)。</summary>
        /// <exception cref="ArgumentException">id が null か空。</exception>
        public bool IsUnlocked(string id)
        {
            return Ledger.IsUnlocked(id, Profile);
        }

        /// <summary>入口の <see cref="GameContent.ProgressionTuning"/> か <see cref="ResourcePath"/> の JSON を読む。無いか読めなければ <see cref="ProgressionSpec.Default"/>。</summary>
        public static ProgressionSpec LoadSpec()
        {
            return KitTuning.Load(c => c.ProgressionTuning, ResourcePath, ProgressionJson.Parse, ProgressionSpec.Default);
        }

        /// <summary>プロフィールを読み直して記録を始める。既に始めていれば何もしない。</summary>
        public void Install()
        {
            if (IsInstalled)
            {
                return;
            }

            Profile = _store.Load();
            Debug.Log(ProgressionLog.Configured(Ledger.Spec));
            Debug.Log(ProgressionLog.Loaded(Profile, Level));
            CombatEvents.Killed += OnKilled;
            CombatEvents.MatchFinished += OnMatchFinished;
            IsInstalled = true;
        }

        /// <summary>記録を止める。終わっていない試合の分は保存しない。始めていなければ何もしない。</summary>
        public void Uninstall()
        {
            if (!IsInstalled)
            {
                return;
            }

            CombatEvents.Killed -= OnKilled;
            CombatEvents.MatchFinished -= OnMatchFinished;
            IsInstalled = false;
        }

        private void OnKilled(KillFeedEntry entry)
        {
            if (entry.IsSuicide || entry.Friendly)
            {
                return;
            }

            if (!_participants.TryGet(entry.KillerId, out var killer) || !killer.IsLocal)
            {
                return;
            }

            var award = Ledger.RecordKill(Profile, entry.Headshot);
            Profile = award.Profile;
            Debug.Log(ProgressionLog.Killed(entry.Headshot, award));
            LogLevelUp(award);
        }

        private void OnMatchFinished(MatchResult result)
        {
            var award = Ledger.RecordMatch(Profile, result);
            Profile = award.Profile;
            Debug.Log(ProgressionLog.MatchRecorded(result, award));
            LogLevelUp(award);
            Save();
        }

        private void Save()
        {
            try
            {
                _store.Save(Profile);
                Debug.Log(ProgressionLog.Saved(Profile));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                // 手元の Profile は持ち続けるので、次の試合が終わったときにまとめて書ける。
                Debug.LogError(ProgressionLog.SaveFailed(e.Message));
            }
        }

        private static void LogLevelUp(ProgressionAward award)
        {
            if (award.LeveledUp)
            {
                Debug.Log(ProgressionLog.LeveledUp(award));
            }
        }
    }
}
