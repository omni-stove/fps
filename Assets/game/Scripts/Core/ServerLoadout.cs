using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    // クライアントは撃った武器の Id だけを送り、ダメージ・射程・減衰・連射間隔はすべてサーバーが持たせている武器の値を使う。
    public sealed class ServerLoadout
    {
        private readonly Dictionary<int, Entry> _entries = new Dictionary<int, Entry>();
        private readonly Dictionary<int, IReadOnlyList<WeaponSpec>> _choices = new Dictionary<int, IReadOnlyList<WeaponSpec>>();
        private readonly HashSet<string> _weaponIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly int _burst;

        public ServerLoadout(LoadoutRule rule, IReadOnlyList<WeaponSpec> weapons, float switchSeconds, int burst = ShotBudget.DefaultBurst)
            : this(rule, weapons, null, switchSeconds, burst)
        {
        }

        public ServerLoadout(
            LoadoutRule rule, IReadOnlyList<WeaponSpec> weapons, WeaponCatalog catalog, float switchSeconds, int burst = ShotBudget.DefaultBurst)
        {
            if (rule == null)
            {
                throw new ArgumentNullException(nameof(rule));
            }

            if (weapons == null)
            {
                throw new ArgumentNullException(nameof(weapons));
            }

            if (!(switchSeconds >= 0f) || float.IsInfinity(switchSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(switchSeconds), switchSeconds, "switchSeconds must be a non-negative finite number");
            }

            if (burst <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(burst), burst, "burst must be positive");
            }

            var copy = new List<WeaponSpec>(weapons);
            var reason = rule.Validate(copy);
            if (reason != null)
            {
                throw new ArgumentException("weapons do not fit the loadout rule: " + reason, nameof(weapons));
            }

            foreach (var weapon in copy)
            {
                _weaponIds.Add(weapon.Id);
            }

            if (catalog != null)
            {
                foreach (var weapon in catalog.All)
                {
                    _weaponIds.Add(weapon.Id);
                }
            }

            Catalog = catalog;
            Rule = rule;
            Weapons = copy.AsReadOnly();
            SwitchSeconds = switchSeconds;
            _burst = burst;
        }

        public LoadoutRule Rule { get; }

        public IReadOnlyList<WeaponSpec> Weapons { get; }

        public WeaponCatalog Catalog { get; }

        public int ChoiceCount => _choices.Count;

        public float SwitchSeconds { get; }

        public int PlayerCount => _entries.Count;

        public bool Contains(string weaponId)
        {
            return weaponId != null && _weaponIds.Contains(weaponId);
        }

        public ShotRejection CheckWeapon(WeaponSpec held, string weaponId)
        {
            if (!Contains(weaponId))
            {
                return ShotRejection.UnknownWeapon;
            }

            if (held == null)
            {
                return ShotRejection.NoWeapon;
            }

            if (!string.Equals(held.Id, weaponId, StringComparison.Ordinal))
            {
                return ShotRejection.WeaponMismatch;
            }

            return ShotRejection.None;
        }

        public ShotRejection TryFire(int playerId, WeaponSpec held, string weaponId, double nowSeconds)
        {
            var rejection = CheckWeapon(held, weaponId);
            if (rejection != ShotRejection.None)
            {
                return rejection;
            }

            if (!_entries.TryGetValue(playerId, out var entry) || !string.Equals(entry.WeaponId, held.Id, StringComparison.Ordinal))
            {
                // ビームと火炎の撃ち方は照射を自分で数えて最短の間隔を 0 にしているので、それで作ると制限が外れる。照射の間隔で制限する。
                var round = held.Round;
                var budget = round.Delivery == RoundDelivery.Beam ? new ShotBudget(round.Beam.TickSeconds, _burst)
                    : round.Delivery == RoundDelivery.Spray ? new ShotBudget(round.Spray.TickSeconds, _burst)
                    : new ShotBudget(TriggerModels.For(held), _burst);
                entry = new Entry(held.Id, budget);
                _entries[playerId] = entry;
            }

            return entry.Budget.TryConsume(nowSeconds) ? ShotRejection.None : ShotRejection.FireRateExceeded;
        }

        public bool TryGetBudget(int playerId, out ShotBudget budget)
        {
            if (_entries.TryGetValue(playerId, out var entry))
            {
                budget = entry.Budget;
                return true;
            }

            budget = null;
            return false;
        }

        // 送ってきた側の文字列をログに出さないため、理由には一覧に無い Id そのものは入れない。
        public string Resolve(IReadOnlyList<string> weaponIds, out IReadOnlyList<WeaponSpec> weapons)
        {
            weapons = null;
            if (Catalog == null)
            {
                return "this match has no weapon catalog to choose from";
            }

            if (weaponIds == null)
            {
                return "no weapons were sent";
            }

            if (weaponIds.Count != Rule.SlotCount)
            {
                return "expected " + Rule.SlotCount + " weapon(s) (one per slot) but got " + weaponIds.Count;
            }

            var specs = new List<WeaponSpec>(weaponIds.Count);
            for (var i = 0; i < weaponIds.Count; i++)
            {
                var id = weaponIds[i];
                if (string.IsNullOrEmpty(id))
                {
                    return "slot " + i + " has no weapon id";
                }

                if (!Catalog.TryGet(id, out var spec))
                {
                    return "slot " + i + " has a weapon id that is not in the catalog (length " + id.Length + ")";
                }

                specs.Add(spec);
            }

            var reason = Rule.Validate(specs);
            if (reason != null)
            {
                return reason;
            }

            weapons = specs.AsReadOnly();
            return null;
        }

        public bool TryChoose(int playerId, IReadOnlyList<string> weaponIds, out string reason)
        {
            reason = Resolve(weaponIds, out var weapons);
            if (reason != null)
            {
                return false;
            }

            _choices[playerId] = weapons;
            return true;
        }

        public bool HasChoice(int playerId)
        {
            return _choices.ContainsKey(playerId);
        }

        public IReadOnlyList<WeaponSpec> WeaponsFor(int playerId)
        {
            return _choices.TryGetValue(playerId, out var chosen) ? chosen : Weapons;
        }

        public bool Remove(int playerId)
        {
            var hadBudget = _entries.Remove(playerId);
            var hadChoice = _choices.Remove(playerId);
            return hadBudget || hadChoice;
        }

        public void ResetBudgets()
        {
            _entries.Clear();
        }

        private readonly struct Entry
        {
            public Entry(string weaponId, ShotBudget budget)
            {
                WeaponId = weaponId;
                Budget = budget;
            }

            public string WeaponId { get; }

            public ShotBudget Budget { get; }
        }
    }
}
