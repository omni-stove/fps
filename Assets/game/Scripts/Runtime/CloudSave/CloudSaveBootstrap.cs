using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // プロフィールとキャンペーンが同じ保存先を使うよう、手元の既定の保存先はここからだけ作る。
    public static class CloudSaveBootstrap
    {
        public const string ObjectName = "CloudSave";

        private static readonly string[] _startupSlots = { ProfileStore.Slot, CampaignSaveSlots.Legacy };

        // ドメインの再読み込みを切っているので、Play の開始時に ResetOnPlay で戻す。
        private static CloudSaveSync _current;

        // Shooter.NetEos はプラグインがあるビルドでだけコンパイルされるので、起動時にそちらから入れてもらう。
        // 無いビルドでは null のまま、手元だけで保存する。Play をまたいで残す (入れるのは SubsystemRegistration の 1 回だけのため)。
        public static ICloudSaveBackend Backend { get; set; }

        public static CloudSaveSync Current => _current;

        // 同期を入れる前に書いたスロットは時刻の記録に無いので、起動時の同期では名前を挙げて見に行く。
        public static IReadOnlyList<string> StartupSlots => _startupSlots;

        public static ISaveStorage Storage => Acquire();

        public static bool Syncs(string slot)
        {
            return string.Equals(slot, ProfileStore.Slot, StringComparison.Ordinal) || CampaignSaveSlots.IsSlot(slot);
        }

        public static long UnixMillisecondsNow()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        public static CloudSaveSync Create(ISaveStorage local, ICloudSaveBackend backend)
        {
            return new CloudSaveSync(local, backend, Syncs, _startupSlots, UnixMillisecondsNow);
        }

        private static CloudSaveSync Acquire()
        {
            if (_current != null)
            {
                return _current;
            }

            var backend = Backend;
            var sync = Create(FileSaveStorage.CreateDefault(), backend);

            // Edit Mode では回す部品を置けないので、そのたびに作って手元だけで使う。
            if (!Application.isPlaying)
            {
                return sync;
            }

            _current = sync;
            if (backend == null)
            {
                Debug.Log(CloudSaveLog.Off("no cloud save backend in this build"));
                return sync;
            }

            var host = new GameObject(ObjectName);
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<CloudSaveDriver>().Bind(sync);
            return sync;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            _current = null;
        }
    }
}
