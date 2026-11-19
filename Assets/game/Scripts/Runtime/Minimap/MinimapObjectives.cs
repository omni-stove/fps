using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shooter.Runtime
{
    // 目標の側 (ObjectiveAreaBase など) と出す側 (MinimapPublisher) が互いを知らずに済むよう、間に置く static の置き場にする。
    public static class MinimapObjectives
    {
        // 載せる順を有効になった順に保つため、集合でなく並びで持つ。
        private static readonly List<IMinimapObjective> _entries = new List<IMinimapObjective>();

        // 破棄された目標も数えるのは、数えるたびに並びを書き換えないため。
        public static int Count => _entries.Count;

        public static void Add(IMinimapObjective objective)
        {
            if (objective == null)
            {
                throw new ArgumentNullException(nameof(objective));
            }

            if (!_entries.Contains(objective))
            {
                _entries.Add(objective);
            }
        }

        public static bool Remove(IMinimapObjective objective)
        {
            return objective != null && _entries.Remove(objective);
        }

        // 読むときに破棄済みを外すのは、OnDisable を通らずに消えた目標 (Edit Mode の DestroyImmediate など) を残さないため。
        public static void CopyShownTo(List<IMinimapObjective> into)
        {
            if (into == null)
            {
                throw new ArgumentNullException(nameof(into));
            }

            _entries.RemoveAll(IsDestroyed);
            foreach (var objective in _entries)
            {
                if (objective.ShowsOnMinimap)
                {
                    into.Add(objective);
                }
            }
        }

        public static void Clear()
        {
            _entries.Clear();
        }

        // ドメインの再読み込みを切った Play でも、前の Play の目標が残らないようにする。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetAll()
        {
            _entries.Clear();
        }

        private static bool IsDestroyed(IMinimapObjective objective)
        {
            // Unity の破棄は C# の参照を null にしないので、UnityEngine.Object の == で見る。
            return objective is UnityEngine.Object unityObject && unityObject == null;
        }
    }
}
