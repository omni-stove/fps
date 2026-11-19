using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // プロフィールと他のセーブ (キャンペーンなど) が同じ保存先を使うよう、手元の既定の保存先はここからだけ作る。
    public static class CloudSaveBootstrap
    {
        public const string ObjectName = "CloudSave";

        // 登録はコードの配線なので Backend と同じく Play をまたいで残す。登録し直しても同じ判定は 1 つにまとめる。
        private static readonly List<Registration> _registrations = new List<Registration>();

        // ドメインの再読み込みを切っているので、Play の開始時に ResetOnPlay で戻す。
        private static CloudSaveSync _current;

        // Shooter.NetEos はプラグインがあるビルドでだけコンパイルされるので、起動時にそちらから入れてもらう。
        // 無いビルドでは null のまま、手元だけで保存する。Play をまたいで残す (入れるのは SubsystemRegistration の 1 回だけのため)。
        public static ICloudSaveBackend Backend { get; set; }

        public static CloudSaveSync Current => _current;

        // 同期を入れる前に書いたスロットは時刻の記録に無いので、起動時の同期では名前を挙げて見に行く。
        public static IReadOnlyList<string> StartupSlots
        {
            get
            {
                var slots = new List<string> { ProfileStore.Slot };
                foreach (var registration in _registrations)
                {
                    foreach (var slot in registration.StartupSlots)
                    {
                        if (!slots.Contains(slot))
                        {
                            slots.Add(slot);
                        }
                    }
                }

                return slots;
            }
        }

        public static ISaveStorage Storage => Acquire();

        // 本体はプロフィールのスロットしか知らないので、他のセーブを同期する側 (キャンペーンなど) が、
        // どのスロットが自分のものか・起動時に見に行くスロット・クラウドの中身を読めるかを渡す。
        public static void RegisterSlots(Func<string, bool> owns, IEnumerable<string> startupSlots, Func<string, bool> canRead)
        {
            if (owns == null)
            {
                throw new ArgumentNullException(nameof(owns));
            }

            if (startupSlots == null)
            {
                throw new ArgumentNullException(nameof(startupSlots));
            }

            if (canRead == null)
            {
                throw new ArgumentNullException(nameof(canRead));
            }

            var slots = new List<string>();
            foreach (var slot in startupSlots)
            {
                if (string.IsNullOrEmpty(slot))
                {
                    throw new ArgumentException("startup slots must not be empty", nameof(startupSlots));
                }

                if (!owns(slot))
                {
                    throw new ArgumentException($"startup slot '{slot}' is not one of the registered slots", nameof(startupSlots));
                }

                slots.Add(slot);
            }

            var registration = new Registration(owns, slots.ToArray(), canRead);
            var index = _registrations.FindIndex(r => r.Owns.Equals(owns));
            if (index >= 0)
            {
                _registrations[index] = registration;
            }
            else
            {
                _registrations.Add(registration);
            }

            // 動いている同期は起動時のスロットを作ったときに受け取っているので、遅れて足したものはここで知らせる。
            if (_current != null && slots.Count > 0)
            {
                Debug.LogWarning(CloudSaveLog.RegisteredLate(slots.Count));
            }
        }

        public static bool Syncs(string slot)
        {
            return string.Equals(slot, ProfileStore.Slot, StringComparison.Ordinal) || OwnerOf(slot) != null;
        }

        public static long UnixMillisecondsNow()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        public static CloudSaveSync Create(ISaveStorage local, ICloudSaveBackend backend)
        {
            return new CloudSaveSync(local, backend, Syncs, StartupSlots, UnixMillisecondsNow) { CanRead = CanRead };
        }

        public static bool CanRead(string slot, string text)
        {
            try
            {
                if (string.Equals(slot, ProfileStore.Slot, StringComparison.Ordinal))
                {
                    PlayerProfile.FromJson(text);
                    return true;
                }

                // 持ち主のいないスロットは中身の形を誰も知らないので、手元を書き換えないよう読めないものとして扱う。
                var owner = OwnerOf(slot);
                return owner != null && owner.CanRead(text);
            }
            catch (FormatException)
            {
                return false;
            }
        }

        // テストが登録を空にしてから自分の判定を入れるために使う。
        internal static void ClearRegisteredSlots()
        {
            _registrations.Clear();
        }

        private static Registration OwnerOf(string slot)
        {
            if (slot == null)
            {
                return null;
            }

            foreach (var registration in _registrations)
            {
                if (registration.Owns(slot))
                {
                    return registration;
                }
            }

            return null;
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

            // クラウドの読み書きはサインインしたユーザーで行うので、起動時の同期に間に合うようここでサインインを始める。
            AccountBootstrap.Ensure();

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

        private sealed class Registration
        {
            public Registration(Func<string, bool> owns, string[] startupSlots, Func<string, bool> canRead)
            {
                Owns = owns;
                StartupSlots = startupSlots;
                CanRead = canRead;
            }

            public Func<string, bool> Owns { get; }

            public string[] StartupSlots { get; }

            public Func<string, bool> CanRead { get; }
        }
    }
}
