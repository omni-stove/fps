using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class LoadoutPresetBookTests
    {
        private const string _rifle = "00000000-0000-4000-8000-000000000001";
        private const string _handgun = "00000000-0000-4000-8000-000000000002";

        [Test]
        public void TheDefaultsAreFivePresetsWith24CharacterNames()
        {
            Assert.That(LoadoutPresetSpec.DefaultMaxPresets, Is.EqualTo(5));
            Assert.That(LoadoutPresetSpec.DefaultMaxNameLength, Is.EqualTo(24));
            Assert.That(LoadoutPresetSpec.Default.MaxPresets, Is.EqualTo(5));
            Assert.That(LoadoutPresetSpec.Default.MaxNameLength, Is.EqualTo(24));
        }

        [Test]
        public void TheSpecNeedsAtLeastOnePresetAndOneCharacter()
        {
            Assert.That(new LoadoutPresetSpec(1, 1).MaxPresets, Is.EqualTo(1));
            Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => new LoadoutPresetSpec(0)).ParamName, Is.EqualTo("maxPresets"));
            Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => new LoadoutPresetSpec(3, 0)).ParamName, Is.EqualTo("maxNameLength"));
            Assert.Throws<ArgumentNullException>(() => new LoadoutPresetBook(null));
        }

        [Test]
        public void AnEmptyBookHasNoActivePreset()
        {
            var book = new LoadoutPresetBook(LoadoutPresetSpec.Default);

            Assert.That(book.Count, Is.EqualTo(0));
            Assert.That(book.ActiveIndex, Is.EqualTo(LoadoutPresetBook.NoPreset));
            Assert.That(book.Active, Is.Null);
            Assert.That(book.IsFull, Is.False);
        }

        [Test]
        public void TheFirstAddedPresetIsActiveAndLaterOnesAreNot()
        {
            var book = new LoadoutPresetBook(LoadoutPresetSpec.Default);

            Assert.That(book.Add("A", Choice(_rifle)), Is.EqualTo(0));
            Assert.That(book.Add("B", Choice(_handgun)), Is.EqualTo(1));

            Assert.That(book.ActiveIndex, Is.EqualTo(0));
            Assert.That(book.Active.Name, Is.EqualTo("A"));
        }

        [Test]
        public void AddingBeyondTheLimitIsRefusedAndKeepsThePresets()
        {
            var book = new LoadoutPresetBook(new LoadoutPresetSpec(3));
            book.Add("A", Choice(_rifle));
            book.Add("B", Choice(_rifle));
            book.Add("C", Choice(_rifle));

            Assert.That(book.IsFull, Is.True);
            Assert.Throws<InvalidOperationException>(() => book.Add("D", Choice(_handgun)));
            Assert.That(book.Count, Is.EqualTo(3));
            Assert.That(book[2].Name, Is.EqualTo("C"));
        }

        [Test]
        public void SelectRenameAndReplaceChangeOnlyTheirPreset()
        {
            var book = new LoadoutPresetBook(LoadoutPresetSpec.Default);
            book.Add("A", Choice(_rifle));
            book.Add("B", Choice(_rifle));

            book.Select(1);
            book.Rename(1, "  Close quarters  ");
            book.Replace(1, Choice(_handgun));

            Assert.That(book.ActiveIndex, Is.EqualTo(1));
            Assert.That(book[1].Name, Is.EqualTo("Close quarters"));
            Assert.That(book[1].Choice.WeaponIds, Is.EqualTo(new[] { _handgun }));
            Assert.That(book[0].Name, Is.EqualTo("A"));
            Assert.That(book[0].Choice.WeaponIds, Is.EqualTo(new[] { _rifle }));
        }

        [Test]
        public void RemoveAtKeepsTheSamePresetActiveWhenItCan()
        {
            var book = new LoadoutPresetBook(LoadoutPresetSpec.Default);
            book.Add("A", Choice(_rifle));
            book.Add("B", Choice(_rifle));
            book.Add("C", Choice(_rifle));
            book.Select(2);

            book.RemoveAt(0);
            Assert.That(book.Active.Name, Is.EqualTo("C"), "removing an earlier preset keeps the active one");
            Assert.That(book.ActiveIndex, Is.EqualTo(1));

            book.RemoveAt(1);
            Assert.That(book.Active.Name, Is.EqualTo("B"), "removing the last active preset selects the one before it");

            book.RemoveAt(0);
            Assert.That(book.ActiveIndex, Is.EqualTo(LoadoutPresetBook.NoPreset));
            Assert.Throws<ArgumentOutOfRangeException>(() => book.RemoveAt(0));
        }

        [Test]
        public void RemovingTheActivePresetInTheMiddleSelectsTheNextOne()
        {
            var book = new LoadoutPresetBook(LoadoutPresetSpec.Default);
            book.Add("A", Choice(_rifle));
            book.Add("B", Choice(_rifle));
            book.Add("C", Choice(_rifle));
            book.Select(1);

            book.RemoveAt(1);

            Assert.That(book.ActiveIndex, Is.EqualTo(1));
            Assert.That(book.Active.Name, Is.EqualTo("C"));
        }

        [Test]
        public void ABlankNameClearsTheName()
        {
            var book = new LoadoutPresetBook(LoadoutPresetSpec.Default);
            book.Add("A", Choice(_rifle));

            book.Rename(0, "   ");
            Assert.That(book[0].HasName, Is.False);

            book.Rename(0, null);
            Assert.That(book[0].Name, Is.Empty);
        }

        [Test]
        public void NamesLongerThanTheLimitOrWithControlCharactersAreRefused()
        {
            var book = new LoadoutPresetBook(new LoadoutPresetSpec(2, 4));
            book.Add("ABCD", Choice(_rifle));

            Assert.Throws<ArgumentException>(() => book.Rename(0, "ABCDE"));
            Assert.Throws<ArgumentException>(() => book.Rename(0, "A\tB"));
            Assert.Throws<ArgumentException>(() => book.Add("ABCDE", Choice(_rifle)));
            Assert.That(book[0].Name, Is.EqualTo("ABCD"));
            Assert.That(book.Count, Is.EqualTo(1));
        }

        [Test]
        public void UnsavableChoicesAndBadIndexesAreRefused()
        {
            var book = new LoadoutPresetBook(LoadoutPresetSpec.Default);
            book.Add("A", Choice(_rifle));

            Assert.Throws<ArgumentNullException>(() => book.Add("B", null));
            Assert.Throws<ArgumentException>(() => book.Add("B", new LoadoutChoice(Array.Empty<string>())));
            Assert.Throws<ArgumentException>(() => book.Replace(0, new LoadoutChoice(new[] { _rifle, string.Empty })));
            Assert.Throws<ArgumentOutOfRangeException>(() => book.Select(1));
            Assert.Throws<ArgumentOutOfRangeException>(() => book.Rename(-1, "B"));
            Assert.Throws<ArgumentOutOfRangeException>(() => book.Replace(1, Choice(_rifle)));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = book[1]);
            Assert.That(book.Count, Is.EqualTo(1));
            Assert.That(book[0].Choice.WeaponIds, Is.EqualTo(new[] { _rifle }));
        }

        private static LoadoutChoice Choice(string weaponId)
        {
            return new LoadoutChoice(new[] { weaponId });
        }
    }
}
