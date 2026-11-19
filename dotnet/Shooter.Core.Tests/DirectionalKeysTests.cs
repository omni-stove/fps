using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class DirectionalKeysTests
    {
        private static readonly string[] _letterKeys = { "W", "A", "S", "D" };
        private static readonly string[] _wordKeys = { "Up", "Left", "Down", "Right" };

        [Test]
        public void SingleLetterKeysAreJoinedAndLongerOnesAreSeparated()
        {
            Assert.That(DirectionalKeys.Join(_letterKeys), Is.EqualTo("WASD"));
            Assert.That(DirectionalKeys.Join(_wordKeys), Is.EqualTo("Up/Left/Down/Right"));
            Assert.That(DirectionalKeys.Join(new[] { "W", null, " ", "D" }), Is.EqualTo("WD"));
            Assert.That(DirectionalKeys.Join(new[] { " W ", "Left" }), Is.EqualTo("W/Left"));
            Assert.That(DirectionalKeys.Join(Array.Empty<string>()), Is.Empty);
            Assert.Throws<ArgumentNullException>(() => DirectionalKeys.Join(null));
        }
    }
}
