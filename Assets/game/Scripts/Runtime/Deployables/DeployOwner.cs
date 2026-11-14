using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    // 能力の枠を入れ替えても置いた物を数え続けられるよう、能力 (DeployAbility) ではなく持ち主に持たせる。
    [DisallowMultipleComponent]
    public sealed class DeployOwner : MonoBehaviour
    {
        private readonly Dictionary<string, DeployRoster<Deployable>> _rosters = new Dictionary<string, DeployRoster<Deployable>>(StringComparer.Ordinal);
        private int _lastSerial;

        public static DeployOwner GetOrAdd(GameObject owner)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            var found = owner.GetComponent<DeployOwner>();
            return found != null ? found : owner.AddComponent<DeployOwner>();
        }

        public string OwnerId
        {
            get
            {
                var damageable = GetComponent<Damageable>();
                return damageable != null ? damageable.Id : name;
            }
        }

        public int NextSerial()
        {
            return ++_lastSerial;
        }

        public int ActiveCount(string kindId)
        {
            return _rosters.TryGetValue(Key(kindId), out var roster) ? roster.Count : 0;
        }

        public IReadOnlyList<Deployable> Active(string kindId)
        {
            return _rosters.TryGetValue(Key(kindId), out var roster) ? roster.Items : Array.Empty<Deployable>();
        }

        public bool CanPlace(string kindId, DeploySpec spec)
        {
            return Roster(kindId, spec).CanPlace;
        }

        public void Add(string kindId, DeploySpec spec, Deployable deployable)
        {
            if (deployable == null)
            {
                throw new ArgumentNullException(nameof(deployable));
            }

            var roster = Roster(kindId, spec);
            var evicted = roster.Add(deployable);
            deployable.Removed += OnRemoved;

            // 置いたログは呼ぶ側がこの後に出すので、入れ替えで消したログが先に並ぶ。
            RemoveAll(evicted, DeployRemoval.Replaced);
        }

        public void Clear(DeployRemoval reason)
        {
            var rosters = new List<DeployRoster<Deployable>>(_rosters.Values);
            foreach (var roster in rosters)
            {
                RemoveAll(roster.Clear(), reason);
            }
        }

        // 同じ種類を別の設定の能力で置き直したときに、新しい上限に合わせて古い物を消す。
        private DeployRoster<Deployable> Roster(string kindId, DeploySpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            var key = Key(kindId);
            if (!_rosters.TryGetValue(key, out var roster))
            {
                roster = new DeployRoster<Deployable>(spec.MaxActive, spec.Overflow);
                _rosters.Add(key, roster);
                return roster;
            }

            if (roster.MaxActive != spec.MaxActive || roster.Overflow != spec.Overflow)
            {
                RemoveAll(roster.Configure(spec.MaxActive, spec.Overflow), DeployRemoval.Replaced);
            }

            return roster;
        }

        private void RemoveAll(IReadOnlyList<Deployable> deployables, DeployRemoval reason)
        {
            foreach (var deployable in deployables)
            {
                if (deployable != null)
                {
                    deployable.Removed -= OnRemoved;
                    deployable.Remove(reason);
                }
            }
        }

        private void OnRemoved(Deployable deployable, DeployRemoval reason)
        {
            deployable.Removed -= OnRemoved;
            if (_rosters.TryGetValue(Key(deployable.KindId), out var roster))
            {
                roster.Remove(deployable);
            }
        }

        private static string Key(string kindId)
        {
            return kindId ?? string.Empty;
        }
    }
}
