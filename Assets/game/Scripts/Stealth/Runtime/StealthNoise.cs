using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 字幕 (SubtitleFeed) と分けるのは、あちらは素材があって実際に鳴った音だけを流すが、敵の耳には音の素材の有無に関わらず届けたいため。
    public static class StealthNoise
    {
        // SoundStimuli は SubsystemRegistration で購読を外すので、その後の AfterAssembliesLoaded でつなぐ。
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        internal static void Subscribe()
        {
            SoundStimuli.Emitted -= OnEmitted;
            SoundStimuli.Emitted += OnEmitted;
        }

        private static void OnEmitted(GameObject source, SoundStimulus sound)
        {
            Emit(source, sound);
        }

        public static int Emit(GameObject source, SoundStimulus sound)
        {
            return Emit(source, sound, EnemyStealth.Active);
        }

        // 聞いた敵が調べに行くと、その中で一覧が変わることがあるので写しを回す。1 人が投げても残りの敵には届ける。
        public static int Emit(GameObject source, SoundStimulus sound, IReadOnlyList<EnemyStealth> listeners)
        {
            if (listeners == null)
            {
                throw new ArgumentNullException(nameof(listeners));
            }

            var reacted = 0;
            var copy = new EnemyStealth[listeners.Count];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = listeners[i];
            }

            foreach (var listener in copy)
            {
                if (listener == null)
                {
                    continue;
                }

                try
                {
                    if (Reacted(listener.Hear(source, sound)))
                    {
                        reacted++;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogException(e, listener);
                }
            }

            return reacted;
        }

        public static bool Reacted(HearingOutcome outcome)
        {
            return outcome == HearingOutcome.Raised || outcome == HearingOutcome.Noticed || outcome == HearingOutcome.Investigating;
        }
    }
}
