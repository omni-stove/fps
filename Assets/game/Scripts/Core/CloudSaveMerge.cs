namespace Shooter.Core
{
    public static class CloudSaveMerge
    {
        // 時刻の無い手元のセーブ (同期を入れる前に書いたもの・同期を通さずに書かれたもの) はいつ書いたか分からないので、
        // 時刻 0 として扱い、クラウドに何かあればそちらを採る。手元の分は同期の側が別のスロットに残す。
        public const long UnstampedSavedAt = 0;

        public static CloudSaveStamp? Local(CloudSaveStamp? recorded, bool exists)
        {
            if (exists)
            {
                return recorded.HasValue && !recorded.Value.Deleted
                    ? recorded.Value
                    : new CloudSaveStamp(UnstampedSavedAt, false);
            }

            return recorded.HasValue && recorded.Value.Deleted ? recorded : null;
        }

        public static CloudSaveAction Decide(CloudSaveStamp? local, CloudSaveEnvelope cloud)
        {
            if (!local.HasValue)
            {
                return cloud == null || cloud.Deleted ? CloudSaveAction.None : CloudSaveAction.Download;
            }

            if (cloud == null)
            {
                return local.Value.Deleted ? CloudSaveAction.None : CloudSaveAction.Upload;
            }

            if (cloud.SavedAt > local.Value.SavedAt)
            {
                return CloudSaveAction.Download;
            }

            if (local.Value.SavedAt > cloud.SavedAt)
            {
                return CloudSaveAction.Upload;
            }

            return CloudSaveAction.None;
        }
    }
}
