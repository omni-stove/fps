using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.TestTools;

namespace Shooter.Tests.EditMode
{
    // 期待値は調整値 (レベル 2 が 500 XP から) と、下のカタログ (夜のスキンと虎の迷彩がレベル 2 から) から出す。
    public sealed class CosmeticLockerTests
    {
        private const string Desert = "skin-desert";
        private const string Night = "skin-night";
        private const string Tiger = "camo-tiger";

        private static readonly ProgressionSpec _spec = new ProgressionSpec(100, 0, 0, 250, 0, new[] { 0, 500 }, Array.Empty<ProgressionUnlock>());

        private static readonly CosmeticCatalog _catalog = new CosmeticCatalog(new[]
        {
            new CosmeticItem(Desert, CosmeticKind.Skin, "cosmetic.skin-desert", "body-desert"),
            new CosmeticItem(Night, CosmeticKind.Skin, "cosmetic.skin-night", "body-night", 2),
            new CosmeticItem(Tiger, CosmeticKind.Camo, "cosmetic.camo-tiger", "paint-tiger", 2),
        });

        private MemorySaveStorage _storage;
        private ProgressionRecorder _recorder;
        private CosmeticLocker _locker;

        [SetUp]
        public void SetUp()
        {
            GameSignals.ResetAll();
            CombatEvents.ClearSubscribers();
            _storage = new MemorySaveStorage();
        }

        [TearDown]
        public void TearDown()
        {
            _locker?.Uninstall();
            _recorder?.Uninstall();
            GameSignals.ResetAll();
            CombatEvents.ClearSubscribers();
        }

        private void Install(PlayerProfile saved = null)
        {
            if (saved != null)
            {
                new ProfileStore(_storage).Save(saved);
            }

            _recorder = new ProgressionRecorder(_storage, _spec, new OfflineParticipants());
            _recorder.Install();
            _locker = new CosmeticLocker(_recorder, _catalog);
            _locker.Install();
        }

        [Test]
        public void InstallingPublishesTheLockerAndUninstallingClearsIt()
        {
            Install();

            Assert.That(GameSignals.Cosmetics.Value, Is.SameAs(_locker));
            Assert.That(_locker.Catalog, Is.SameAs(_catalog));

            _locker.Uninstall();

            Assert.That(GameSignals.Cosmetics.Value, Is.Null);
            Assert.That(_locker.IsInstalled, Is.False);
        }

        [Test]
        public void TheLockerNeedsAnInstalledRecorder()
        {
            _recorder = new ProgressionRecorder(_storage, _spec, new OfflineParticipants());
            _locker = new CosmeticLocker(_recorder, _catalog);

            Assert.Throws<InvalidOperationException>(() => _locker.Install());
            Assert.Throws<ArgumentNullException>(() => new CosmeticLocker(null, _catalog));
            Assert.Throws<ArgumentNullException>(() => new CosmeticLocker(_recorder, null));
        }

        [Test]
        public void EquippingSavesTheIdInTheProfileAndTellsTheListeners()
        {
            Install();
            var changes = 0;
            _locker.Changed += () => changes++;
            LogAssert.Expect(LogType.Log, "[Cosmetic] equipped kind=Skin id=skin-desert");

            Assert.That(_locker.Equip(CosmeticKind.Skin, Desert), Is.True);

            Assert.That(_locker.EquippedId(CosmeticKind.Skin), Is.EqualTo(Desert));
            Assert.That(_locker.EquippedId(CosmeticKind.Camo), Is.Null);
            Assert.That(_recorder.Profile.EquippedCosmetic(CosmeticKind.Skin), Is.EqualTo(Desert));
            Assert.That(new ProfileStore(_storage).Load().EquippedCosmetic(CosmeticKind.Skin), Is.EqualTo(Desert), "saved at once, not at the end of the next match");
            Assert.That(changes, Is.EqualTo(1));
        }

        [Test]
        public void ANullIdTakesTheLookOff()
        {
            Install(PlayerProfile.Empty.WithCosmetic(CosmeticKind.Skin, Desert));
            Assert.That(_locker.EquippedId(CosmeticKind.Skin), Is.EqualTo(Desert));

            Assert.That(_locker.Equip(CosmeticKind.Skin, null), Is.True);

            Assert.That(_locker.EquippedId(CosmeticKind.Skin), Is.Null);
            Assert.That(new ProfileStore(_storage).Load().Cosmetics, Is.Empty);
        }

        [Test]
        public void ALockedUnknownOrWrongKindLookIsRefused()
        {
            Install();
            LogAssert.Expect(LogType.Warning, "[Cosmetic] refused kind=Skin id=skin-night reason=it unlocks at level 2");
            LogAssert.Expect(LogType.Warning, "[Cosmetic] refused kind=Skin id=missing reason=not in the cosmetic catalog");
            LogAssert.Expect(LogType.Warning, "[Cosmetic] refused kind=Skin id=camo-tiger reason=it is a Camo");

            Assert.That(_locker.IsUnlocked(Night), Is.False);
            Assert.That(_locker.Equip(CosmeticKind.Skin, Night), Is.False);
            Assert.That(_locker.Equip(CosmeticKind.Skin, "missing"), Is.False);
            Assert.That(_locker.Equip(CosmeticKind.Skin, Tiger), Is.False);
            Assert.That(_locker.EquippedId(CosmeticKind.Skin), Is.Null);
            Assert.That(_storage.Has(ProfileStore.Slot), Is.False, "nothing is saved for a refused look");
            Assert.Throws<ArgumentOutOfRangeException>(() => _locker.Equip((CosmeticKind)9, Desert));
        }

        [Test]
        public void ALookUnlocksWithTheLevel()
        {
            Install(new PlayerProfile(600, 0, 0, 0, 0));

            Assert.That(_locker.IsUnlocked(Night), Is.True);
            Assert.That(_locker.IsUnlocked(Tiger), Is.True);
            Assert.That(_locker.IsUnlocked("missing"), Is.False);
            Assert.That(_locker.Equip(CosmeticKind.Camo, Tiger), Is.True);
            Assert.That(_locker.Equipped(CosmeticKind.Camo).VisualId, Is.EqualTo("paint-tiger"));
        }

        [Test]
        public void AnIdTheCatalogNoLongerOffersIsNotWornButStaysSaved()
        {
            Install(PlayerProfile.Empty.WithCosmetic(CosmeticKind.Skin, "removed").WithCosmetic(CosmeticKind.Camo, Tiger));

            Assert.That(_locker.EquippedId(CosmeticKind.Skin), Is.Null, "the catalog no longer has it");
            Assert.That(_locker.EquippedId(CosmeticKind.Camo), Is.Null, "level 1 has not unlocked it");
            Assert.That(_recorder.Profile.EquippedCosmetic(CosmeticKind.Skin), Is.EqualTo("removed"));
        }

        [Test]
        public void AnUninstalledLockerRefusesToEquip()
        {
            Install();
            _locker.Uninstall();
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Cosmetic\] refused kind=Skin id=skin-desert reason=the cosmetic locker is not installed$"));

            Assert.That(_locker.Equip(CosmeticKind.Skin, Desert), Is.False);
        }
    }
}
