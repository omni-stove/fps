using System;
using System.IO;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed class ProgressionRecorder
    {
        public const string ResourcePath = "Tuning/progression";

        private readonly ProfileStore _store;
        private readonly IParticipants _participants;

        public ProgressionRecorder(ISaveStorage storage)
            : this(storage, LoadSpec(), new OfflineParticipants())
        {
        }

        public ProgressionRecorder(ISaveStorage storage, ProgressionSpec spec, IParticipants participants)
        {
            _store = new ProfileStore(storage);
            Ledger = new ProgressionLedger(spec);
            _participants = participants ?? throw new ArgumentNullException(nameof(participants));
        }

        public ProgressionLedger Ledger { get; }

        public PlayerProfile Profile { get; private set; } = PlayerProfile.Empty;

        public int Level => Ledger.LevelOf(Profile);

        public bool IsInstalled { get; private set; }

        public bool IsUnlocked(string id)
        {
            return Ledger.IsUnlocked(id, Profile);
        }

        public static ProgressionSpec LoadSpec()
        {
            return KitTuning.Load(c => c.ProgressionTuning, ResourcePath, ProgressionJson.Parse, ProgressionSpec.Default);
        }

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
