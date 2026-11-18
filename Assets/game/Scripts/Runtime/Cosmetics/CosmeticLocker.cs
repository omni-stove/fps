using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 着けた見た目はプロフィールに残す。プロフィールを自分で読み書きしないのは、ProgressionRecorder と別々に保存すると後に書いた方が先の分を消すため (AchievementRecorder と同じ)。
    public sealed class CosmeticLocker : ICosmeticLocker
    {
        private readonly ProgressionRecorder _progression;

        public CosmeticLocker(ProgressionRecorder progression)
            : this(progression, LoadCatalog())
        {
        }

        public CosmeticLocker(ProgressionRecorder progression, CosmeticCatalog catalog)
        {
            _progression = progression ?? throw new ArgumentNullException(nameof(progression));
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        public event Action Changed;

        public CosmeticCatalog Catalog { get; }

        public ProgressionRecorder Progression => _progression;

        public bool IsInstalled { get; private set; }

        public static CosmeticCatalog LoadCatalog()
        {
            return KitTuning.Load(c => c.CosmeticsTuning, CosmeticJson.ResourcePath, CosmeticJson.Parse, CosmeticCatalog.Default);
        }

        public void Install()
        {
            if (IsInstalled)
            {
                return;
            }

            if (!_progression.IsInstalled)
            {
                throw new InvalidOperationException("install the progression recorder before the cosmetic locker");
            }

            Debug.Log(CosmeticLog.Configured(Catalog));
            _progression.ProfileChanged += OnProfileChanged;
            IsInstalled = true;
            GameSignals.Cosmetics.Publish(this);
        }

        public void Uninstall()
        {
            if (!IsInstalled)
            {
                return;
            }

            _progression.ProfileChanged -= OnProfileChanged;
            GameSignals.Cosmetics.ClearIfCurrent(this);
            IsInstalled = false;
        }

        // プロフィールに残っていても、カタログから外れたもの・種類が変わったもの・レベルが足りなくなったものは着けていないことにする。
        public string EquippedId(CosmeticKind kind)
        {
            var item = Catalog.Find(_progression.Profile.EquippedCosmetic(kind));
            return item != null && item.Kind == kind && item.IsUnlockedAt(_progression.Level) ? item.Id : null;
        }

        public CosmeticItem Equipped(CosmeticKind kind)
        {
            var id = EquippedId(kind);
            return id != null ? Catalog.Find(id) : null;
        }

        public bool IsUnlocked(string cosmeticId)
        {
            var item = Catalog.Find(cosmeticId);
            return item != null && item.IsUnlockedAt(_progression.Level);
        }

        public bool Equip(CosmeticKind kind, string cosmeticId)
        {
            if (!Enum.IsDefined(typeof(CosmeticKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "unknown cosmetic kind");
            }

            var reason = Check(kind, cosmeticId, out var id);
            if (reason != null)
            {
                Debug.LogWarning(CosmeticLog.Refused(kind, cosmeticId, reason));
                return false;
            }

            _progression.Change(profile => profile.WithCosmetic(kind, id));
            Debug.Log(CosmeticLog.Equipped(kind, id));
            _progression.Save();
            return true;
        }

        private string Check(CosmeticKind kind, string cosmeticId, out string id)
        {
            id = null;
            if (!IsInstalled)
            {
                return "the cosmetic locker is not installed";
            }

            if (string.IsNullOrWhiteSpace(cosmeticId))
            {
                return null;
            }

            var item = Catalog.Find(cosmeticId.Trim());
            if (item == null)
            {
                return "not in the cosmetic catalog";
            }

            if (item.Kind != kind)
            {
                return "it is a " + item.Kind;
            }

            if (!item.IsUnlockedAt(_progression.Level))
            {
                return "it unlocks at level " + item.UnlockLevel.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            id = item.Id;
            return null;
        }

        private void OnProfileChanged(PlayerProfile profile)
        {
            Changed?.Invoke();
        }
    }
}
