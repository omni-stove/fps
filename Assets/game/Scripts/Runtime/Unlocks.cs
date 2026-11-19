using System;
using UnityEngine;

namespace Shooter.Runtime
{
    // 本体は進行 (レベルや解放の表) の型を知らないので、解放を決める側がここへ門を入れ、本体は Id で問い合わせるだけにする。
    public static class Unlocks
    {
        // ドメインの再読み込みを切っているので、Play の開始時に ResetOnPlay で戻す。
        private static Func<string, bool> _gate;

        public static Func<string, bool> Gate => _gate;

        public static bool IsUnlocked(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("id must not be empty", nameof(id));
            }

            var gate = _gate;
            return gate == null || gate(id);
        }

        public static void Install(Func<string, bool> gate)
        {
            _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        }

        // 後から別の門が入っていたときに、先に入れた側が外すことでその門を消さないよう、自分の門のときだけ外す。
        public static void Uninstall(Func<string, bool> gate)
        {
            if (gate != null && _gate != null && _gate.Equals(gate))
            {
                _gate = null;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            _gate = null;
        }
    }
}
