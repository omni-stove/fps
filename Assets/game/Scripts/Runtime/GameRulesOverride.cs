using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// シーンに配線された <see cref="GameRulesDefinition"/> の代わりに使う <see cref="GameRules"/> の置き場所。
    /// <see cref="GameBootstrap"/> はこれが入っていればシーンのルールより優先する。
    /// シーンを読み直しても <see cref="Clear"/> するまで残る。
    /// Play を始めるたびに空に戻すので、シーンを直接開いて Play したときは上書きされていない。
    /// </summary>
    public static class GameRulesOverride
    {
        /// <summary>上書きに使うルール。上書きしていなければ null。</summary>
        public static GameRules Current { get; private set; }

        /// <summary>rules で上書きする。前の上書きは置き換える。</summary>
        public static void Set(GameRules rules)
        {
            Current = rules ?? throw new ArgumentNullException(nameof(rules));
        }

        /// <summary>上書きをやめ、シーンのルールに戻す。</summary>
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
