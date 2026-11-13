using System;

namespace Fps.Core
{
    /// <summary>
    /// メニューから見たミッションのセーブ。再開できるセーブがあるか (<see cref="TryReadResumable"/>) と、「最初から」で消す
    /// (<see cref="Delete"/>) を、CampaignRunner がセーブを読む・消すのと同じスロットの規則 (<see cref="CampaignSaveSlots"/>) で行う。
    /// </summary>
    public static class CampaignSaveLookup
    {
        /// <summary>
        /// missionId のミッションの再開できるセーブを読む。ミッションごとのスロットが無ければ、<see cref="CampaignSaveSlots.ReadsLegacy"/> のミッションは
        /// 古いスロットも読む。CampaignRunner が最初から始めるセーブ (壊れている・別のミッションのもの) は再開できないものとして false を返す。
        /// </summary>
        /// <param name="storage">セーブの置き場所。</param>
        /// <param name="missionId">ミッション Id (<see cref="CampaignLaunch.IsValidMissionId"/> を満たすこと)。</param>
        /// <param name="save">再開できるセーブ。無ければ null。</param>
        /// <param name="problem">セーブはあるが使えなかったときの理由。セーブが無い・使えるときは null。</param>
        public static bool TryReadResumable(ISaveStorage storage, string missionId, out CampaignSave save, out string problem)
        {
            if (storage == null)
            {
                throw new ArgumentNullException(nameof(storage));
            }

            var slot = CampaignSaveSlots.For(missionId);
            save = null;
            problem = null;
            if (!storage.TryRead(slot, out var text)
                && !(CampaignSaveSlots.ReadsLegacy(missionId) && storage.TryRead(CampaignSaveSlots.Legacy, out text)))
            {
                return false;
            }

            CampaignSave parsed;
            try
            {
                parsed = CampaignSave.FromJson(text);
            }
            catch (FormatException e)
            {
                problem = "the save is broken: " + e.Message;
                return false;
            }

            if (!string.Equals(parsed.MissionId, missionId, StringComparison.Ordinal))
            {
                problem = "the save belongs to mission '" + parsed.MissionId + "'";
                return false;
            }

            save = parsed;
            return true;
        }

        /// <summary>
        /// missionId のミッションのセーブを消す (古いスロットを読むミッションは古いスロットも)。無ければ何もしない。
        /// 置き場所が消せなかったときの例外 (IOException など) はそのまま投げる。
        /// </summary>
        public static void Delete(ISaveStorage storage, string missionId)
        {
            if (storage == null)
            {
                throw new ArgumentNullException(nameof(storage));
            }

            var slot = CampaignSaveSlots.For(missionId);
            storage.Delete(slot);

            // 古いスロットが残っていると、CampaignRunner はそこから再開してしまう。
            if (CampaignSaveSlots.ReadsLegacy(missionId))
            {
                storage.Delete(CampaignSaveSlots.Legacy);
            }
        }
    }
}
