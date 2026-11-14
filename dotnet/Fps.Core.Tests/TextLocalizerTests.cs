using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    [NonParallelizable]
    public sealed class TextLocalizerTests
    {
        private TextTable _saved;

        [SetUp]
        public void SaveCurrent()
        {
            _saved = TextLocalizer.Current;
            TextLocalizer.Current = TextTable.Empty;
        }

        [TearDown]
        public void RestoreCurrent()
        {
            TextLocalizer.Current = _saved;
        }

        [Test]
        public void StartsWithAnEmptyTable()
        {
            Assert.That(TextLocalizer.Current, Is.SameAs(TextTable.Empty));
            Assert.That(TextLocalizer.Resolve(new LocalizedText("menu.start")), Is.EqualTo("menu.start"));
        }

        [Test]
        public void ResolvesWithTheCurrentTable()
        {
            TextLocalizer.Current = TextTable.FromJson("{\"menu.start\":\"はじめる\"}");

            Assert.That(TextLocalizer.Resolve(new LocalizedText("menu.start")), Is.EqualTo("はじめる"));
        }

        [Test]
        public void RaisesChangedWhenTheTableIsReplaced()
        {
            var calls = new List<TextTable>();
            Action onChanged = () => calls.Add(TextLocalizer.Current);
            TextLocalizer.Changed += onChanged;
            try
            {
                var table = TextTable.FromJson("{\"a\":\"b\"}");
                TextLocalizer.Current = table;
                TextLocalizer.Current = table;

                Assert.That(calls, Is.EqualTo(new[] { table }));
            }
            finally
            {
                TextLocalizer.Changed -= onChanged;
            }
        }

        [Test]
        public void RejectsNull()
        {
            Assert.Throws<ArgumentNullException>(() => TextLocalizer.Current = null);
            Assert.That(TextLocalizer.Current, Is.SameAs(TextTable.Empty));
        }
    }
}
