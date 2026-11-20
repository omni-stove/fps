using System;

namespace Shooter.Core
{
    public static class CampaignSaveLookup
    {
        // 壊れている・別のミッションのセーブを再開できないものとするのは、CampaignRunner がそのセーブでは最初から始めるため。
        public static bool TryReadResumable(ISaveStorage storage, string missionId, out CampaignSave save, out string problem)
        {
            if (storage == null)
            {
                throw new ArgumentNullException(nameof(storage));
            }

            var slot = CampaignSaveSlots.For(missionId);
            save = null;
            problem = null;
            if (!storage.TryRead(slot, out var text))
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

        public static void Delete(ISaveStorage storage, string missionId)
        {
            if (storage == null)
            {
                throw new ArgumentNullException(nameof(storage));
            }

            storage.Delete(CampaignSaveSlots.For(missionId));
        }
    }
}
