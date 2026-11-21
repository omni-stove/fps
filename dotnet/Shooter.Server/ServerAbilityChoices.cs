using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using Shooter.Core;

namespace Shooter.Server
{
    // 人ごとに選んだ能力 (置ける物の種類の Id) を枠の順に持つ。枠を「なし」にするのは空の Id (AbilitySelection.None)。
    // 選んだ並び (Choose) は武器の選択と同じく次の試合でも残り、退出で消える。選んだ並びは次の出現 (Equip) から持たせ、
    // まだ選んでいない人には候補の先頭から枠の数だけを持たせる。持たせた並びは、マップやルールが替わると候補が替わりうるので捨て、
    // 次の出現か次に引かれたときに持たせ直す。
    // 置く物の他に選べるのはグラップル (grapple.json) だけ。置く物と同じ Id の枠を取り合わないよう、同じ Id は作るときに断る
    internal sealed class ServerAbilityChoices
    {
        private readonly DeployCatalog _catalog;
        private readonly List<string> _candidates = new List<string>();
        private readonly Dictionary<int, ReadOnlyCollection<string>> _chosen = new Dictionary<int, ReadOnlyCollection<string>>();
        private readonly Dictionary<int, ReadOnlyCollection<string>> _equipped = new Dictionary<int, ReadOnlyCollection<string>>();
        private ReadOnlyCollection<string> _default;

        public ServerAbilityChoices(DeployCatalog catalog = null, int slotCount = AbilitySelection.DefaultSlotCount, GrappleType grapple = null)
        {
            if (slotCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(slotCount), slotCount, "slotCount must not be negative");
            }

            _catalog = catalog ?? DeployCatalog.Empty;
            if (grapple != null && _catalog.TryGet(grapple.Id, out _))
            {
                throw new ArgumentException(
                    "the grapple id is also a deployable id; give the grapple in grapple.json an id deployables.json does not use", nameof(grapple));
            }

            Grapple = grapple;
            SlotCount = slotCount;
            _default = DefaultOf(_candidates, slotCount);
        }

        public int SlotCount { get; }

        // 選べるグラップル。grapple.json が無ければ null
        public GrappleType Grapple { get; }

        public ReadOnlyCollection<string> Candidates => _candidates.AsReadOnly();

        public ReadOnlyCollection<string> Default => _default;

        // ルールのファイルの loadout.abilities が null (項目が無い) なら一覧の全部 (置く物の後にグラップル) を候補にし、あれば書いた順の Id だけを候補にする。
        // 一覧に無い Id は skipped に返して候補から外し、置けない種類 (isUsable が false) は既に理由を出しているので黙って外す。
        // 選んだ並びが新しい候補に合わなくなった人は droppedPlayers に返して既定に戻し、持たせた並びは全員分捨てる
        public void Use(
            IReadOnlyList<string> listed, Func<string, bool> isUsable, List<string> skipped, List<int> droppedPlayers)
        {
            _candidates.Clear();
            if (listed == null)
            {
                foreach (var type in _catalog.All)
                {
                    AddCandidate(type.Id, isUsable);
                }

                if (Grapple != null)
                {
                    AddCandidate(Grapple.Id, isUsable);
                }
            }
            else
            {
                foreach (var id in listed)
                {
                    if (string.IsNullOrEmpty(id) || _candidates.Contains(id))
                    {
                        continue;
                    }

                    if (!_catalog.TryGet(id, out _) && !IsGrapple(id))
                    {
                        skipped?.Add(id);
                        continue;
                    }

                    AddCandidate(id, isUsable);
                }
            }

            _default = DefaultOf(_candidates, SlotCount);
            _equipped.Clear();

            var dropped = new List<int>();
            foreach (var pair in _chosen)
            {
                if (Check(pair.Value) != null)
                {
                    dropped.Add(pair.Key);
                }
            }

            dropped.Sort();
            foreach (var playerId in dropped)
            {
                _chosen.Remove(playerId);
            }

            droppedPlayers?.AddRange(dropped);
        }

        // 選んだ並びを持たせてよいかを見る。よければ null、だめなら断る理由。理由に Id を載せないのは、クライアントが送った文字でログの行を崩さないため
        public string Check(IReadOnlyList<string> abilityIds)
        {
            ArgumentNullException.ThrowIfNull(abilityIds);

            if (abilityIds.Count != SlotCount)
            {
                return "the request has " + Text(abilityIds.Count) + " ability slots but the loadout has " + Text(SlotCount);
            }

            for (var slot = 0; slot < abilityIds.Count; slot++)
            {
                var id = abilityIds[slot];
                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }

                if (!_candidates.Contains(id))
                {
                    return "ability slot " + Text(slot) + " names an ability the rules do not offer";
                }

                for (var earlier = 0; earlier < slot; earlier++)
                {
                    if (string.Equals(abilityIds[earlier], id, StringComparison.Ordinal))
                    {
                        return "ability slot " + Text(slot) + " names the same ability as slot " + Text(earlier);
                    }
                }
            }

            return null;
        }

        // 今持っている能力は変えない。次の Equip から持たせる
        public void Choose(int playerId, IReadOnlyList<string> abilityIds)
        {
            var reason = Check(abilityIds);
            if (reason != null)
            {
                throw new ArgumentException(reason, nameof(abilityIds));
            }

            var ids = new string[abilityIds.Count];
            for (var slot = 0; slot < ids.Length; slot++)
            {
                ids[slot] = abilityIds[slot] ?? AbilitySelection.None;
            }

            _chosen[playerId] = Array.AsReadOnly(ids);
        }

        public bool HasChoice(int playerId)
        {
            return _chosen.ContainsKey(playerId);
        }

        // 次の出現で持たせる並び
        public ReadOnlyCollection<string> AbilitiesFor(int playerId)
        {
            return _chosen.TryGetValue(playerId, out var chosen) ? chosen : _default;
        }

        // 出現したときに呼ぶ。持たせた並びが前と変わったら true
        public bool Equip(int playerId)
        {
            var next = _chosen.TryGetValue(playerId, out var chosen) ? chosen : _default;
            var changed = !_equipped.TryGetValue(playerId, out var before) || !SameIds(before, next);
            _equipped[playerId] = next;
            return changed;
        }

        // 持たせた並びを捨てた後 (マップやルールを替えた後) に出現を待たずに引かれたら、そのときの選択を持たせる
        public ReadOnlyCollection<string> EquippedFor(int playerId)
        {
            if (!_equipped.TryGetValue(playerId, out var equipped))
            {
                Equip(playerId);
                equipped = _equipped[playerId];
            }

            return equipped;
        }

        public bool IsEquipped(int playerId, string typeId)
        {
            if (string.IsNullOrEmpty(typeId))
            {
                return false;
            }

            foreach (var id in EquippedFor(playerId))
            {
                if (string.Equals(id, typeId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public bool IsGrapple(string abilityId)
        {
            return Grapple != null && string.Equals(Grapple.Id, abilityId, StringComparison.Ordinal);
        }

        // グラップルを候補にしていて、その人が今持っている並びにあるか
        public bool HasGrapple(int playerId)
        {
            return Grapple != null && _candidates.Contains(Grapple.Id) && IsEquipped(playerId, Grapple.Id);
        }

        public void ForgetEquipped()
        {
            _equipped.Clear();
        }

        public bool Remove(int playerId)
        {
            var hadChoice = _chosen.Remove(playerId);
            var hadEquipped = _equipped.Remove(playerId);
            return hadChoice || hadEquipped;
        }

        private void AddCandidate(string id, Func<string, bool> isUsable)
        {
            if (isUsable == null || isUsable(id))
            {
                _candidates.Add(id);
            }
        }

        // 候補が枠より少なければ、残りの枠は「なし」にする (AbilitySelection が既定の足りない枠を埋めるのと同じ)
        private static ReadOnlyCollection<string> DefaultOf(List<string> candidates, int slotCount)
        {
            var ids = new string[slotCount];
            for (var slot = 0; slot < slotCount; slot++)
            {
                ids[slot] = slot < candidates.Count ? candidates[slot] : AbilitySelection.None;
            }

            return Array.AsReadOnly(ids);
        }

        private static bool SameIds(ReadOnlyCollection<string> a, ReadOnlyCollection<string> b)
        {
            if (a.Count != b.Count)
            {
                return false;
            }

            for (var i = 0; i < a.Count; i++)
            {
                if (!string.Equals(a[i], b[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
