using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public static class SoundStimuli
    {
        public static event Action<GameObject, SoundStimulus> Emitted;

        // 一つの購読者が投げても残りの購読者には届けたいので、event をそのまま呼ばずに一つずつ呼ぶ。
        public static void Emit(GameObject source, SoundStimulus sound)
        {
            var emitted = Emitted;
            if (emitted == null)
            {
                return;
            }

            foreach (var handler in emitted.GetInvocationList())
            {
                try
                {
                    ((Action<GameObject, SoundStimulus>)handler)(source, sound);
                }
                catch (Exception e)
                {
                    Debug.LogException(e, handler.Target as UnityEngine.Object);
                }
            }
        }

        // ドメインの読み直しを切った Play では静的な event が前回の購読を持ち越すので、Play の始めに外す。
        // 本体の購読者は AfterAssembliesLoaded 以降でつなぎ直す。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetOnPlay()
        {
            Emitted = null;
        }
    }
}
