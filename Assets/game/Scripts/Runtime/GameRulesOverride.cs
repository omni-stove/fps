using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public static class GameRulesOverride
    {
        public static GameRules Current { get; private set; }

        public static void Set(GameRules rules)
        {
            Current = rules ?? throw new ArgumentNullException(nameof(rules));
        }

        public static void Clear()
        {
            Current = null;
        }

        // Enter Play Mode でドメインを読み直さない設定なので、前の Play の上書きが残らないよう Play のたびに消す。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            Current = null;
        }
    }
}
