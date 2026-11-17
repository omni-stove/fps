using System;
using System.Text.RegularExpressions;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Shooter.Tests.EditMode
{
    public sealed class ProfileStoreTests
    {
        private const string Broken = "{\"version\":1,\"xp\":";

        [Test]
        public void ANewStorageGivesAnEmptyProfile()
        {
            var storage = new MemorySaveStorage();

            Assert.That(new ProfileStore(storage).Load(), Is.EqualTo(PlayerProfile.Empty));
            Assert.That(storage.WriteCount, Is.Zero, "nothing is written just by reading");
        }

        [Test]
        public void ANewStorageStartsAtTheGivenRating()
        {
            var storage = new MemorySaveStorage();

            Assert.That(new ProfileStore(storage, 1500).Load(), Is.EqualTo(PlayerProfile.Started(1500)));
            Assert.That(new ProfileStore(storage).InitialRating, Is.EqualTo(1000));
        }

        [Test]
        public void ASavedProfileIsReadBack()
        {
            var storage = new MemorySaveStorage();
            var store = new ProfileStore(storage);
            var profile = new PlayerProfile(900, 3, 2, 2, 1, 4, 1234);

            store.Save(profile);

            Assert.That(ProfileStore.Slot, Is.EqualTo("profile"));
            Assert.That(
                storage.Text("profile"),
                Is.EqualTo("{\"version\":3,\"xp\":900,\"kills\":3,\"assists\":4,\"deaths\":2,\"matches\":2,\"wins\":1,\"rating\":1234}"));
            Assert.That(new ProfileStore(storage).Load(), Is.EqualTo(profile));
            Assert.That(new ProfileStore(storage, 1500).Load(), Is.EqualTo(profile), "a saved rating wins over the initial rating");
        }

        [Test]
        public void AVersion2ProfileKeepsItsRecordAndStartsAtTheGivenRating()
        {
            var storage = new MemorySaveStorage();
            const string version2 = "{\"version\":2,\"xp\":900,\"kills\":3,\"assists\":4,\"deaths\":2,\"matches\":2,\"wins\":1}";
            storage.Write(ProfileStore.Slot, version2);
            var writes = storage.WriteCount;

            var profile = new ProfileStore(storage, 1500).Load();

            Assert.That(profile, Is.EqualTo(new PlayerProfile(900, 3, 2, 2, 1, 4, 1500)));
            Assert.That(storage.Has(ProfileStore.RejectedSlot), Is.False);
            Assert.That(storage.WriteCount, Is.EqualTo(writes), "the old save is rewritten by the next save, not by reading");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void AVersion1ProfileKeepsItsXpAndIsNotSetAside()
        {
            var storage = new MemorySaveStorage();
            const string version1 = "{\"version\":1,\"xp\":1600,\"kills\":9,\"deaths\":2,\"matches\":3,\"wins\":1}";
            storage.Write(ProfileStore.Slot, version1);
            var writes = storage.WriteCount;

            var profile = new ProfileStore(storage).Load();

            Assert.That(profile, Is.EqualTo(new PlayerProfile(1600, 9, 2, 3, 1, 0)));
            Assert.That(storage.Has(ProfileStore.RejectedSlot), Is.False);
            Assert.That(storage.WriteCount, Is.EqualTo(writes), "the old save is rewritten by the next save, not by reading");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void SavingAgainReplacesTheProfile()
        {
            var storage = new MemorySaveStorage();
            var store = new ProfileStore(storage);

            store.Save(new PlayerProfile(100, 1, 0, 0, 0));
            store.Save(new PlayerProfile(350, 1, 0, 1, 0));

            Assert.That(store.Load(), Is.EqualTo(new PlayerProfile(350, 1, 0, 1, 0)));
        }

        [Test]
        public void ABrokenProfileIsLoggedKeptAsideAndReplacedByANewOne()
        {
            var storage = new MemorySaveStorage();
            storage.Write(ProfileStore.Slot, Broken);

            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Progression\] profile rejected: "));
            LogAssert.Expect(LogType.Warning, "[Progression] kept the rejected profile in slot=profile-rejected");
            var profile = new ProfileStore(storage, 1500).Load();

            Assert.That(profile, Is.EqualTo(PlayerProfile.Started(1500)));
            Assert.That(storage.Text(ProfileStore.RejectedSlot), Is.EqualTo(Broken));
            Assert.That(storage.Text(ProfileStore.Slot), Is.EqualTo(Broken), "the slot is left until the next save");
        }

        [Test]
        public void AProfileFromANewerVersionIsRejected()
        {
            var storage = new MemorySaveStorage();
            const string newer = "{\"version\":4,\"xp\":900,\"kills\":3,\"assists\":0,\"deaths\":2,\"matches\":2,\"wins\":1,\"rating\":1000,\"prestige\":1}";
            storage.Write(ProfileStore.Slot, newer);

            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Progression\] profile rejected: unsupported profile data version 4"));
            LogAssert.Expect(LogType.Warning, "[Progression] kept the rejected profile in slot=profile-rejected");
            var profile = new ProfileStore(storage).Load();

            Assert.That(profile, Is.EqualTo(PlayerProfile.Empty));
            Assert.That(storage.Text(ProfileStore.RejectedSlot), Is.EqualTo(newer));
        }

        [Test]
        public void AProfileWithValuesOutOfRangeIsRejected()
        {
            var storage = new MemorySaveStorage();
            storage.Write(ProfileStore.Slot, "{\"version\":1,\"xp\":-5,\"kills\":0,\"deaths\":0,\"matches\":0,\"wins\":0}");

            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Progression\] profile rejected: invalid profile data: "));
            LogAssert.Expect(LogType.Warning, "[Progression] kept the rejected profile in slot=profile-rejected");

            Assert.That(new ProfileStore(storage).Load(), Is.EqualTo(PlayerProfile.Empty));
        }

        [Test]
        public void ABrokenProfileStillGivesANewOneWhenItCannotBeKeptAside()
        {
            var storage = new MemorySaveStorage();
            storage.Write(ProfileStore.Slot, Broken);
            storage.FailWrites = true;

            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Progression\] profile rejected: "));
            LogAssert.Expect(LogType.Error, "[Progression] could not save the profile: disk full");
            var profile = new ProfileStore(storage).Load();

            Assert.That(profile, Is.EqualTo(PlayerProfile.Empty));
            Assert.That(storage.Has(ProfileStore.RejectedSlot), Is.False);
        }

        [Test]
        public void SaveLetsAWriteFailureThrough()
        {
            var storage = new MemorySaveStorage { FailWrites = true };

            Assert.Throws<System.IO.IOException>(() => new ProfileStore(storage).Save(PlayerProfile.Empty));
        }

        [Test]
        public void RejectsMissingArguments()
        {
            Assert.Throws<ArgumentNullException>(() => new ProfileStore(null));
            Assert.Throws<ArgumentNullException>(() => new ProfileStore(null, 1000));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ProfileStore(new MemorySaveStorage(), -1));
            Assert.Throws<ArgumentNullException>(() => new ProfileStore(new MemorySaveStorage()).Save(null));
        }
    }
}
