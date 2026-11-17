using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shooter.Runtime
{
    // 見つけた側 (UavReward) と出す側 (MinimapPublisher) が互いを知らずに済むよう、間に置く static の置き場にする。
    public static class MinimapReveals
    {
        // 載せる順を見つけた順に保つため、辞書でなく並びで持つ。
        private static readonly List<Entry> _entries = new List<Entry>();

        // null なら Time.time を使い、ポーズで時間が止まると残りの秒数も止まるようにする。テストは差し込んで進める。
        public static Func<float> Clock { get; set; }

        // 消えた・倒れた敵も数えるのは、数えるたびに並びを書き換えないため。正しく数えるには先に Prune を呼ぶ。
        public static int Count => _entries.Count;

        private static float Now => Clock != null ? Clock() : Time.time;

        public static void Reveal(Damageable target, float seconds)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "seconds must be positive and finite");
            }

            var expiresAt = Now + seconds;
            var index = IndexOf(target);
            if (index < 0)
            {
                _entries.Add(new Entry(target, expiresAt));
                return;
            }

            // 短い報酬を後から重ねても、先の長い報酬を縮めない。
            if (_entries[index].ExpiresAt < expiresAt)
            {
                _entries[index] = new Entry(target, expiresAt);
            }
        }

        public static bool IsRevealed(Damageable target)
        {
            return RemainingSeconds(target) > 0f;
        }

        public static float RemainingSeconds(Damageable target)
        {
            if (target == null || target.IsDead)
            {
                return 0f;
            }

            var index = IndexOf(target);
            return index < 0 ? 0f : Math.Max(0f, _entries[index].ExpiresAt - Now);
        }

        // 読む前に Prune するのは、秒数の切れた敵を消すための別の Tick を持たないため。
        public static void CopyRevealedTo(List<Damageable> into)
        {
            if (into == null)
            {
                throw new ArgumentNullException(nameof(into));
            }

            Prune();
            foreach (var entry in _entries)
            {
                into.Add(entry.Target);
            }
        }

        public static int Prune()
        {
            var now = Now;
            return _entries.RemoveAll(e => e.Target == null || e.Target.IsDead || now >= e.ExpiresAt);
        }

        public static void Clear()
        {
            _entries.Clear();
        }

        // ドメインの再読み込みを切った Play でも、前の Play で見つけた敵が残らないようにする。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetAll()
        {
            _entries.Clear();
            Clock = null;
        }

        private static int IndexOf(Damageable target)
        {
            for (var i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Target == target)
                {
                    return i;
                }
            }

            return -1;
        }

        private readonly struct Entry
        {
            public Entry(Damageable target, float expiresAt)
            {
                Target = target;
                ExpiresAt = expiresAt;
            }

            public Damageable Target { get; }

            public float ExpiresAt { get; }
        }
    }
}
