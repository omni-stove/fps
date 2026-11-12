using System;
using System.IO;
using System.Text.RegularExpressions;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Fps.Tests.EditMode
{
    public sealed class FileSaveStorageTests
    {
        private string _root;
        private FileSaveStorage _storage;

        [SetUp]
        public void SetUp()
        {
            // まだ無いディレクトリを渡し、最初の書き込みで作られることも確かめる。
            _root = Path.Combine(Path.GetTempPath(), "fps-save-tests-" + Guid.NewGuid().ToString("N"), "saves");
            _storage = new FileSaveStorage(_root);
        }

        [TearDown]
        public void TearDown()
        {
            var parent = Path.GetDirectoryName(_root);
            if (parent != null && Directory.Exists(parent))
            {
                Directory.Delete(parent, true);
            }
        }

        [Test]
        public void ReadsNothingBeforeTheFirstWrite()
        {
            Assert.That(_storage.TryRead("campaign", out var text), Is.False);
            Assert.That(text, Is.Null);
            Assert.That(Directory.Exists(_root), Is.False, "reading does not create the directory");
        }

        [Test]
        public void WritesToSlotJsonAndReadsItBack()
        {
            _storage.Write("campaign", "{\"a\":\"日本語\"}");

            var path = Path.Combine(_root, "campaign.json");
            Assert.That(_storage.PathFor("campaign"), Is.EqualTo(path));
            Assert.That(File.Exists(path), Is.True);
            Assert.That(File.Exists(path + FileSaveStorage.TempSuffix), Is.False, "the temp file is moved into place");
            Assert.That(_storage.TryRead("campaign", out var text), Is.True);
            Assert.That(text, Is.EqualTo("{\"a\":\"日本語\"}"));

            var bytes = File.ReadAllBytes(path);
            Assert.That(bytes[0], Is.EqualTo((byte)'{'), "written without a BOM");
        }

        [Test]
        public void WriteReplacesTheExistingContent()
        {
            _storage.Write("campaign", "first");
            _storage.Write("campaign", "second");

            Assert.That(_storage.TryRead("campaign", out var text), Is.True);
            Assert.That(text, Is.EqualTo("second"));
            Assert.That(File.Exists(_storage.PathFor("campaign") + FileSaveStorage.TempSuffix), Is.False);
        }

        [Test]
        public void SlotsAreIndependent()
        {
            _storage.Write("a", "1");
            _storage.Write("b", "2");

            _storage.Delete("a");

            Assert.That(_storage.TryRead("a", out _), Is.False);
            Assert.That(_storage.TryRead("b", out var text), Is.True);
            Assert.That(text, Is.EqualTo("2"));
        }

        [Test]
        public void DeleteRemovesTheSlotAndLeftoverTempFile()
        {
            _storage.Write("campaign", "x");
            var temp = _storage.PathFor("campaign") + FileSaveStorage.TempSuffix;
            File.WriteAllText(temp, "half-written");

            _storage.Delete("campaign");

            Assert.That(_storage.TryRead("campaign", out _), Is.False);
            Assert.That(File.Exists(temp), Is.False);
        }

        [Test]
        public void DeleteWithoutSaveDoesNothing()
        {
            Assert.DoesNotThrow(() => _storage.Delete("campaign"));
            Directory.CreateDirectory(_root);
            Assert.DoesNotThrow(() => _storage.Delete("campaign"));
        }

        [Test]
        public void LeftoverTempFileDoesNotBlockTheNextWrite()
        {
            Directory.CreateDirectory(_root);
            File.WriteAllText(_storage.PathFor("campaign") + FileSaveStorage.TempSuffix, "stale");

            _storage.Write("campaign", "fresh");

            Assert.That(_storage.TryRead("campaign", out var text), Is.True);
            Assert.That(text, Is.EqualTo("fresh"));
        }

        [TestCase("../campaign")]
        [TestCase("..")]
        [TestCase("a..b")]
        [TestCase(".")]
        [TestCase("dir/campaign")]
        [TestCase("dir\\campaign")]
        [TestCase("/abs")]
        [TestCase("C:campaign")]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("nul\0byte")]
        public void RejectsUnsafeSlotNames(string slot)
        {
            Assert.Throws<ArgumentException>(() => _storage.Write(slot, "x"));
            Assert.Throws<ArgumentException>(() => _storage.TryRead(slot, out _));
            Assert.Throws<ArgumentException>(() => _storage.Delete(slot));
            Assert.That(Directory.Exists(_root), Is.False, "nothing is written for a rejected slot");
        }

        [Test]
        public void RejectsNullSlotAndText()
        {
            Assert.Throws<ArgumentNullException>(() => _storage.Write(null, "x"));
            Assert.Throws<ArgumentNullException>(() => _storage.Write("campaign", null));
            Assert.Throws<ArgumentNullException>(() => _storage.TryRead(null, out _));
            Assert.Throws<ArgumentNullException>(() => _storage.Delete(null));
        }

        [Test]
        public void RejectsAnEmptyRootDirectory()
        {
            Assert.Throws<ArgumentNullException>(() => new FileSaveStorage(null));
            Assert.Throws<ArgumentException>(() => new FileSaveStorage(" "));
        }

        [Test]
        public void BrokenEncodingIsLoggedAndTreatedAsNoSave()
        {
            Directory.CreateDirectory(_root);
            File.WriteAllBytes(_storage.PathFor("campaign"), new byte[] { (byte)'{', 0xC3, 0x28, 0xFF, (byte)'}' });
            LogAssert.Expect(LogType.Error, new Regex(@"^\[Campaign\] failed to read save slot=campaign .*no save"));

            Assert.That(_storage.TryRead("campaign", out var text), Is.False);
            Assert.That(text, Is.Null);
        }

        [Test]
        public void BrokenJsonIsReturnedAsIsForTheCallerToReject()
        {
            // 中身の解釈は CampaignSave.FromJson の役目。ストレージは書かれたとおりに返す。
            Directory.CreateDirectory(_root);
            File.WriteAllText(_storage.PathFor("campaign"), "{\"version\":1,");

            Assert.That(_storage.TryRead("campaign", out var text), Is.True);
            Assert.That(text, Is.EqualTo("{\"version\":1,"));
        }

        [Test]
        public void ADirectoryInPlaceOfTheSaveIsNoSave()
        {
            Directory.CreateDirectory(_storage.PathFor("campaign"));

            Assert.That(_storage.TryRead("campaign", out var text), Is.False);
            Assert.That(text, Is.Null);
        }
    }
}
