using System;
using NUnit.Framework;
using Shooter.Core;

namespace Shooter.Protocol.Tests
{
    // 補強の要求はサーバーだけが読み、案内は持ち主の部分に載って、変わったときだけ差分に載ることを確かめる
    public sealed class HordeFortifyMessageTests
    {
        // 試合の部分が空の差分の、持ち主の部分の頭 (Id・版・tick・基準の tick・試合の部分のビット・持ち主の有無・持ち主の部分のビット・最後に処理した入力の tick)
        private const int _ownerBodyAt = 2 + 1 + 4 + 4 + 2 + 1 + 2 + 4;

        private static readonly HordeOfferSync _repair = new HordeOfferSync(3, HordeFortificationAction.Repair, "Barricade", 12, HordePurchaseVerdict.Ok);

        [Test]
        public void TheRequestHasItsOwnIdAndOnlyTheServerReadsIt()
        {
            var writer = new MessageWriter();
            writer.WriteMessage(new HordeFortifyRequestMessage());

            var read = MatchMessages.CreateServerTable().Read(writer.AsSegment());
            var client = Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(writer.AsSegment()));

            Assert.That(MessageIds.HordeFortifyRequest, Is.EqualTo(40));
            Assert.That(read, Is.InstanceOf<HordeFortifyRequestMessage>());
            Assert.That(client.Message, Does.Contain("is not accepted here"));
            Assert.That(MessageTable.IsReserved(MessageIds.HordeFortifyRequest), Is.False);
        }

        [Test]
        public void TheRequestCarriesNoBody()
        {
            var writer = new MessageWriter();
            writer.WriteMessage(new HordeFortifyRequestMessage());

            // Id (2)・版 (1) だけ
            Assert.That(writer.ToArray().Length, Is.EqualTo(2 + 1));
        }

        [Test]
        public void TheOfferIsTheNextOwnerPart()
        {
            Assert.That((int)OwnerFields.HordeOffer, Is.EqualTo(1 << 12));
            Assert.That(OwnerFields.All.HasFlag(OwnerFields.HordeOffer), Is.True);
        }

        [Test]
        public void TheOfferRoundTrips()
        {
            var sent = Snapshot(10);
            sent.Owner.HordeOffer = new HordeOfferSync(7, HordeFortificationAction.Build, "Gate East", 40, HordePurchaseVerdict.NotEnoughCurrency);

            var got = Send(sent, null);

            Assert.That(got.IsComplete, Is.True);
            Assert.That(got.Owner.HordeOffer.HasValue, Is.True);
            var offer = got.Owner.HordeOffer.Value;
            Assert.That(offer.FortificationId, Is.EqualTo(7));
            Assert.That(offer.Action, Is.EqualTo(HordeFortificationAction.Build));
            Assert.That(offer.NameKey, Is.EqualTo("Gate East"));
            Assert.That(offer.Cost, Is.EqualTo(40));
            Assert.That(offer.Verdict, Is.EqualTo(HordePurchaseVerdict.NotEnoughCurrency));
        }

        [Test]
        public void NoOfferRoundTripsAsNone()
        {
            Assert.That(Send(Snapshot(10), null).Owner.HordeOffer, Is.Null);
        }

        [Test]
        public void AnUnchangedOfferIsNotSentAgainAndAChangedOneResolves()
        {
            var baseline = Snapshot(10);
            baseline.Owner.HordeOffer = _repair;
            var unchanged = Snapshot(11);
            unchanged.Owner.CopyFrom(baseline.Owner);
            var withoutOffer = Snapshot(11);
            withoutOffer.Owner.CopyFrom(baseline.Owner);
            withoutOffer.Owner.HordeOffer = null;
            var cheaper = Snapshot(11);
            cheaper.Owner.CopyFrom(baseline.Owner);
            cheaper.Owner.HordeOffer = new HordeOfferSync(3, HordeFortificationAction.Repair, "Barricade", 8, HordePurchaseVerdict.Ok);

            // 有無 (1) だけ
            Assert.That(Size(withoutOffer, baseline) - Size(unchanged, baseline), Is.EqualTo(1));

            // 有無 (1)・番号 (4)・種類 (1)・鍵 (長さ 2 + 9)・額 (4)・払えるか (1)
            Assert.That(Size(cheaper, baseline) - Size(unchanged, baseline), Is.EqualTo(1 + 4 + 1 + 2 + 9 + 4 + 1));

            var got = Send(unchanged, baseline);
            Assert.That(got.TryResolve(Send(baseline, null)), Is.True);
            Assert.That(got.Owner.HordeOffer, Is.EqualTo((HordeOfferSync?)_repair), "the unchanged offer comes from the baseline");
            var gotCheaper = Send(cheaper, baseline);
            Assert.That(gotCheaper.TryResolve(Send(baseline, null)), Is.True);
            Assert.That(gotCheaper.Owner.HordeOffer.Value.Cost, Is.EqualTo(8));
            var gotWithout = Send(withoutOffer, baseline);
            Assert.That(gotWithout.TryResolve(Send(baseline, null)), Is.True);
            Assert.That(gotWithout.Owner.HordeOffer, Is.Null);
        }

        [Test]
        public void ClearingAndCopyingASnapshotHandleTheOffer()
        {
            var sent = Snapshot(10);
            sent.Owner.HordeOffer = _repair;
            var copy = new SnapshotMessage();

            copy.CopyFrom(sent);
            sent.Clear();

            Assert.That(copy.Owner.HordeOffer, Is.EqualTo((HordeOfferSync?)_repair));
            Assert.That(sent.Owner.HordeOffer, Is.Null);
        }

        [Test]
        public void AnOfferOfNothingIsRefused()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new HordeOfferSync(0, HordeFortificationAction.None, "a", 0, HordePurchaseVerdict.Ok));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new HordeOfferSync(-1, HordeFortificationAction.Build, "a", 0, HordePurchaseVerdict.Ok));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new HordeOfferSync(0, HordeFortificationAction.Build, "a", -1, HordePurchaseVerdict.Ok));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new HordeOfferSync(0, HordeFortificationAction.Build, "a", 0, (HordePurchaseVerdict)9));

            var sent = Snapshot(10);
            sent.Owner.HordeOffer = default(HordeOfferSync);
            Assert.Throws<InvalidOperationException>(() => new MessageWriter().WriteMessage(sent));
        }

        [Test]
        public void AnOfferOfNothingIsUnreadable()
        {
            var baseline = Snapshot(10);
            var current = Snapshot(11);
            current.Owner.HordeOffer = _repair;
            var bytes = Bytes(current, baseline);

            // 有無 (1)・番号 (4) の後に種類 (1) が並ぶ
            Assert.That(bytes[_ownerBodyAt + 1 + 4], Is.EqualTo((byte)HordeFortificationAction.Repair));
            bytes[_ownerBodyAt + 1 + 4] = (byte)HordeFortificationAction.None;

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(new ArraySegment<byte>(bytes)));
        }

        [Test]
        public void ALongNameKeyIsCutToWhatCanBeSent()
        {
            var name = new string('x', MatchProtocol.MaxContentIdBytes + 10);
            var offer = new HordeOfferSync(0, HordeFortificationAction.Build, name, 1, HordePurchaseVerdict.Ok);
            var sent = Snapshot(10);
            sent.Owner.HordeOffer = offer;

            var got = Send(sent, null);

            Assert.That(offer.NameKey, Has.Length.EqualTo(MatchProtocol.MaxContentIdBytes));
            Assert.That(got.Owner.HordeOffer.Value.NameKey, Is.EqualTo(new string('x', MatchProtocol.MaxContentIdBytes)));
            Assert.That(HordeOfferSync.Fit(null), Is.Empty);
            Assert.That(HordeOfferSync.Fit("Wall"), Is.EqualTo("Wall"));

            // 3 バイトの文字を 22 個 (66 バイト) なら、64 バイトに収まる 21 個まで
            Assert.That(HordeOfferSync.Fit(new string('壁', 22)), Is.EqualTo(new string('壁', 21)));
        }

        private static SnapshotMessage Snapshot(int tick)
        {
            var snapshot = new SnapshotMessage();
            snapshot.Clear();
            snapshot.Tick = tick;
            snapshot.HasOwner = true;
            snapshot.Owner.LastProcessedInputTick = 9;
            snapshot.Match.Rules = MatchRulesSync.From(GameRules.Default);
            snapshot.Players.Add().PlayerId = 1;
            return snapshot;
        }

        private static SnapshotMessage Send(SnapshotMessage message, SnapshotMessage baseline)
        {
            var copy = new SnapshotMessage();
            copy.CopyFrom((SnapshotMessage)MatchMessages.CreateClientTable().Read(new ArraySegment<byte>(Bytes(message, baseline))));
            return copy;
        }

        private static int Size(SnapshotMessage message, SnapshotMessage baseline)
        {
            return Bytes(message, baseline).Length;
        }

        private static byte[] Bytes(SnapshotMessage message, SnapshotMessage baseline)
        {
            message.Baseline = baseline;
            var writer = new MessageWriter();
            writer.WriteMessage(message);
            message.Baseline = null;
            return writer.ToArray();
        }
    }
}
