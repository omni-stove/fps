using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class SquadOrderRecipientsTests
    {
        private static readonly Side _mine = new Side(0);
        private static readonly Side _theirs = new Side(1);

        private static readonly SquadOrderCandidate[] _bots =
        {
            new SquadOrderCandidate(_mine, new Vector3(5f, 0f, 0f), true),
            new SquadOrderCandidate(_theirs, new Vector3(3f, 0f, 0f), true),
            new SquadOrderCandidate(_mine, new Vector3(0f, 0f, 40f), true),
            new SquadOrderCandidate(_mine, new Vector3(1f, 0f, 1f), false),
            new SquadOrderCandidate(_mine, new Vector3(0f, 50f, 10f), true),
        };

        [Test]
        public void WithoutSquadsOnlyNearbyAlliesAreOrdered()
        {
            var recipients = new List<int> { 99 };

            var count = SquadOrderRecipients.Select(_mine, Vector3.Zero, false, 30f, _bots, recipients);

            Assert.That(count, Is.EqualTo(2));
            Assert.That(recipients, Is.EqualTo(new[] { 0, 4 }), "the enemy, the far ally and the dead ally are left out; height does not count");
        }

        [Test]
        public void WithSquadsTheWholeSquadIsOrderedHoweverFar()
        {
            var recipients = new List<int>();

            SquadOrderRecipients.Select(_mine, Vector3.Zero, true, 30f, _bots, recipients);

            Assert.That(recipients, Is.EqualTo(new[] { 0, 2, 4 }));
        }

        [Test]
        public void TheRadiusIncludesItsEdge()
        {
            var recipients = new List<int>();

            SquadOrderRecipients.Select(_mine, Vector3.Zero, false, 5f, _bots, recipients);

            Assert.That(recipients, Is.EqualTo(new[] { 0 }));
        }

        [Test]
        public void BadArgumentsAreRejected()
        {
            var recipients = new List<int>();
            Assert.Throws<ArgumentNullException>(() => SquadOrderRecipients.Select(_mine, Vector3.Zero, false, 5f, null, recipients));
            Assert.Throws<ArgumentNullException>(() => SquadOrderRecipients.Select(_mine, Vector3.Zero, false, 5f, _bots, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => SquadOrderRecipients.Select(_mine, Vector3.Zero, false, 0f, _bots, recipients));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => SquadOrderRecipients.Select(_mine, Vector3.Zero, false, float.PositiveInfinity, _bots, recipients));
        }
    }
}
