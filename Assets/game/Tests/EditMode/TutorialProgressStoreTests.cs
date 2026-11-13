using System;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Fps.Tests.EditMode
{
    public sealed class TutorialProgressStoreTests
    {
        private TutorialProgressStore _store;

        [SetUp]
        public void SetUp()
        {
            // 本物の記録 (DefaultKey) を壊さないよう、テストごとに別のキーを使う。
            _store = new TutorialProgressStore("Fps.Tests.TutorialCompleted." + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(_store.Key);
            PlayerPrefs.Save();
        }

        [Test]
        public void IsNotCompletedBeforeTheFirstSave()
        {
            Assert.That(_store.IsCompleted, Is.False);
        }

        [Test]
        public void MarkCompletedIsReadBack()
        {
            _store.MarkCompleted();

            Assert.That(PlayerPrefs.GetInt(_store.Key), Is.EqualTo(1));
            Assert.That(new TutorialProgressStore(_store.Key).IsCompleted, Is.True);
        }

        [Test]
        public void DeleteForgetsTheCompletion()
        {
            _store.MarkCompleted();

            _store.Delete();

            Assert.That(PlayerPrefs.HasKey(_store.Key), Is.False);
            Assert.That(_store.IsCompleted, Is.False);
        }

        [Test]
        public void AnotherValueUnderTheKeyIsNotCompleted()
        {
            PlayerPrefs.SetString(_store.Key, "yes");

            Assert.That(_store.IsCompleted, Is.False);
        }

        [Test]
        public void RejectsAnEmptyKey()
        {
            Assert.Throws<ArgumentNullException>(() => new TutorialProgressStore(null));
            Assert.Throws<ArgumentException>(() => new TutorialProgressStore(string.Empty));
            Assert.That(new TutorialProgressStore().Key, Is.EqualTo(TutorialProgressStore.DefaultKey));
        }
    }
}
