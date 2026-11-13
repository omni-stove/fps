using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    [NonParallelizable]
    public sealed class LocalizerTests
    {
        private TextTable _saved;

        [SetUp]
        public void SaveCurrent()
        {
            _saved = Localizer.Current;
            Localizer.Current = TextTable.Empty;
        }

        [TearDown]
        public void RestoreCurrent()
        {
            Localizer.Current = _saved;
        }

        [Test]
        public void StartsWithAnEmptyTable()
        {
            Assert.That(Localizer.Current, Is.SameAs(TextTable.Empty));
            Assert.That(Localizer.Resolve(new LocalizedText("menu.start")), Is.EqualTo("menu.start"));
        }

        [Test]
        public void ResolvesWithTheCurrentTable()
        {
            Localizer.Current = TextTable.FromJson("{\"menu.start\":\"はじめる\"}");

            Assert.That(Localizer.Resolve(new LocalizedText("menu.start")), Is.EqualTo("はじめる"));
        }

        [Test]
        public void RaisesChangedWhenTheTableIsReplaced()
        {
            var calls = new List<TextTable>();
            Action onChanged = () => calls.Add(Localizer.Current);
            Localizer.Changed += onChanged;
            try
            {
                var table = TextTable.FromJson("{\"a\":\"b\"}");
                Localizer.Current = table;
                Localizer.Current = table;

                Assert.That(calls, Is.EqualTo(new[] { table }));
            }
            finally
            {
                Localizer.Changed -= onChanged;
            }
        }

        [Test]
        public void RejectsNull()
        {
            Assert.Throws<ArgumentNullException>(() => Localizer.Current = null);
            Assert.That(Localizer.Current, Is.SameAs(TextTable.Empty));
        }
    }
}
