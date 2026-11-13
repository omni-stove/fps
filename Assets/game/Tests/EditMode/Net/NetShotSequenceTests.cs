using System.Collections.Generic;
using Fps.Core;
using NUnit.Framework;
using UnityEngine;

namespace Fps.Net.Tests
{
    /// <summary>
    /// 持ち主 (<see cref="NetShotSequence"/>) とサーバー (<see cref="ServerShotSequence"/>) の発射番号の扱い。
    /// サーバーが弾いた射撃で番号がずれても、サーバーの番号を正として持ち主が追いつくこと、
    /// 持ち主とサーバーが同じ番号なら同じばらつきの向きになることを確かめる。
    /// 送った射撃と訂正の届く順は、どちらも Reliable の RPC なので送った順のまま、届く時刻だけを遅らせて再現する。
    /// </summary>
    public sealed class NetShotSequenceTests
    {
        private const string _id = "3f2b8c1e-0a4d-4c6e-9b1a-7d2e5f8c0b13";
        private const int _playerId = 3;

        private readonly struct SentShot
        {
            public SentShot(int index, int generation, bool accepted)
            {
                Index = index;
                Generation = generation;
                Accepted = accepted;
            }

            public int Index { get; }
            public int Generation { get; }
            public bool Accepted { get; }
        }

        private readonly struct Correction
        {
            public Correction(int generation, int clientIndex, int serverIndex)
            {
                Generation = generation;
                ClientIndex = clientIndex;
                ServerIndex = serverIndex;
            }

            public int Generation { get; }
            public int ClientIndex { get; }
            public int ServerIndex { get; }
        }

        private static WeaponSpec Spec(float hip, float ads, float move)
        {
            return new WeaponSpec(
                _id, "Test", WeaponCategory.AssaultRifle, FireMode.Auto, 30, 90, 0.1f, 2f, 10, 100f, DamageFalloff.None,
                hipSpreadDegrees: hip, adsSpreadDegrees: ads, moveSpreadDegrees: move);
        }

        // サーバーが受け入れた弾に付けた番号と、送った訂正を返す
        private static ShotNumbering Receive(ServerShotSequence server, SentShot shot, Queue<Correction> corrections)
        {
            var numbering = server.Assign(shot.Index, shot.Generation);
            if (numbering.NeedsCorrection)
            {
                corrections.Enqueue(new Correction(numbering.Generation, shot.Index, numbering.Index));
            }

            return numbering;
        }

        [Test]
        public void BothSidesCountFromZeroAndAgreeWhileEveryShotIsAccepted()
        {
            var client = new NetShotSequence();
            var server = new ServerShotSequence();

            for (var i = 0; i < 5; i++)
            {
                var generation = client.Generation;
                var index = client.Take();
                var numbering = server.Assign(index, generation);

                Assert.That(index, Is.EqualTo(i));
                Assert.That(numbering.Index, Is.EqualTo(i));
                Assert.That(numbering.NeedsCorrection, Is.False);
            }

            Assert.That(server.Generation, Is.EqualTo(0));
            Assert.That(client.NextIndex, Is.EqualTo(server.NextIndex));
        }

        [Test]
        public void ServerNumbersOnlyTheShotsItAcceptsAndUsesItsOwnIndex()
        {
            var server = new ServerShotSequence();

            server.Assign(0, 0);

            // 1 番の弾は弾かれた (Assign しない)。次に届いた 2 番は、サーバーの 1 番として判定する
            var numbering = server.Assign(2, 0);

            Assert.That(numbering.Index, Is.EqualTo(1), "the server's count wins over the client's");
            Assert.That(numbering.NeedsCorrection, Is.True);
            Assert.That(numbering.Generation, Is.EqualTo(1));
            Assert.That(server.NextIndex, Is.EqualTo(2));
        }

        [Test]
        public void ClientCatchesUpWithTheServerAfterARejectedShotEvenWithShotsInFlight()
        {
            var client = new NetShotSequence();
            var server = new ServerShotSequence();
            var corrections = new Queue<Correction>();

            // 0 は受け入れ、1 は弾かれ、2〜4 は訂正が届く前に送ってしまう
            var sent = new List<SentShot>();
            for (var i = 0; i < 5; i++)
            {
                var generation = client.Generation;
                sent.Add(new SentShot(client.Take(), generation, i != 1));
            }

            var serverIndices = new List<int>();
            foreach (var shot in sent)
            {
                if (shot.Accepted)
                {
                    serverIndices.Add(Receive(server, shot, corrections).Index);
                }
            }

            Assert.That(serverIndices, Is.EqualTo(new[] { 0, 1, 2, 3 }));
            Assert.That(corrections.Count, Is.EqualTo(1), "shots sent before the correction arrives must not be corrected again");

            var correction = corrections.Dequeue();
            Assert.That(correction.ClientIndex, Is.EqualTo(2));
            Assert.That(correction.ServerIndex, Is.EqualTo(1));
            Assert.That(client.Correct(correction.Generation, correction.ClientIndex, correction.ServerIndex), Is.True);

            Assert.That(client.NextIndex, Is.EqualTo(server.NextIndex));
            Assert.That(client.Generation, Is.EqualTo(server.Generation));

            // 訂正の後に撃った弾はサーバーの番号と一致し、訂正は来ない
            var next = new SentShot(client.Take(), client.Generation, true);
            var numbering = Receive(server, next, corrections);
            Assert.That(numbering.Index, Is.EqualTo(next.Index));
            Assert.That(numbering.NeedsCorrection, Is.False);
            Assert.That(corrections, Is.Empty);
        }

        [Test]
        public void ARejectionAmongShotsInFlightIsCorrectedByTheNextGeneration()
        {
            var client = new NetShotSequence();
            var server = new ServerShotSequence();
            var corrections = new Queue<Correction>();

            // 0 は弾かれ、1 で訂正 (世代 1)。訂正が届く前に送った 2 も弾かれる
            var inFlight = new[]
            {
                new SentShot(client.Take(), 0, false),
                new SentShot(client.Take(), 0, true),
                new SentShot(client.Take(), 0, false),
                new SentShot(client.Take(), 0, true),
            };
            foreach (var shot in inFlight)
            {
                if (shot.Accepted)
                {
                    Receive(server, shot, corrections);
                }
            }

            Assert.That(corrections.Count, Is.EqualTo(1));
            var first = corrections.Dequeue();
            client.Correct(first.Generation, first.ClientIndex, first.ServerIndex);

            // 世代 1 で撃った弾はまだ 1 つずれているので、世代 2 の訂正でもう一度合わせる
            var shotAfterFirst = new SentShot(client.Take(), client.Generation, true);
            var numbering = Receive(server, shotAfterFirst, corrections);
            Assert.That(numbering.NeedsCorrection, Is.True);
            Assert.That(numbering.Generation, Is.EqualTo(2));

            var second = corrections.Dequeue();
            Assert.That(client.Correct(second.Generation, second.ClientIndex, second.ServerIndex), Is.True);
            Assert.That(client.NextIndex, Is.EqualTo(server.NextIndex));
            Assert.That(client.Generation, Is.EqualTo(server.Generation));
        }

        [Test]
        public void ClientIgnoresACorrectionOfTheGenerationItAlreadyHas()
        {
            var client = new NetShotSequence();
            client.Take();
            client.Take();

            Assert.That(client.Correct(1, 1, 0), Is.True);
            Assert.That(client.NextIndex, Is.EqualTo(1));

            Assert.That(client.Correct(1, 1, 0), Is.False);
            Assert.That(client.NextIndex, Is.EqualTo(1));
        }

        [Test]
        public void ClientCanAlsoBeBehindTheServer()
        {
            var client = new NetShotSequence();
            var server = new ServerShotSequence();
            server.Assign(0, 0);
            server.Assign(1, 0);

            // 持ち主の番号が戻った (作り直された) とき、サーバーの番号の方へ進める
            var numbering = server.Assign(client.Take(), client.Generation);
            Assert.That(numbering.Index, Is.EqualTo(2));
            Assert.That(numbering.NeedsCorrection, Is.True);

            client.Correct(numbering.Generation, 0, numbering.Index);
            Assert.That(client.NextIndex, Is.EqualTo(server.NextIndex));
        }

        [Test]
        public void IndicesWrapAroundTheSameWayOnBothSides()
        {
            var client = new NetShotSequence();
            client.Take();
            client.Correct(1, 0, int.MaxValue);

            Assert.That(client.NextIndex, Is.EqualTo(int.MinValue));
        }

        [Test]
        public void SameSeedGivesTheSameDirectionOnTheOwnerAndTheServer()
        {
            var spec = Spec(hip: 8f, ads: 1f, move: 4f);
            var aim = new Vector3(0.2f, 0.1f, 1f).normalized;
            var cone = NetPlayer.SpreadConeDegrees(spec, false, 0f);

            var predicted = NetPlayer.SpreadDirection(aim, cone, _playerId, 7);
            var judged = NetPlayer.SpreadDirection(aim, cone, _playerId, 7);
            var otherShot = NetPlayer.SpreadDirection(aim, cone, _playerId, 8);

            Assert.That(predicted, Is.EqualTo(judged));
            Assert.That(otherShot, Is.Not.EqualTo(predicted));
            Assert.That(Vector3.Angle(aim, predicted), Is.LessThanOrEqualTo(cone + 0.01f));
        }

        [Test]
        public void AimingNarrowsTheConeToTheAdsSpreadAndMovingWidensIt()
        {
            var spec = Spec(hip: 8f, ads: 1f, move: 4f);

            Assert.That(NetPlayer.SpreadConeDegrees(spec, false, 0f), Is.EqualTo(8f));
            Assert.That(NetPlayer.SpreadConeDegrees(spec, true, 0f), Is.EqualTo(1f));
            Assert.That(NetPlayer.SpreadConeDegrees(spec, true, 1f), Is.EqualTo(5f).Within(1e-5f));
        }

        [Test]
        public void NoSpreadKeepsTheAimAsIs()
        {
            var aim = new Vector3(0f, 0f, 2f);

            Assert.That(NetPlayer.SpreadDirection(aim, 0f, _playerId, 0), Is.EqualTo(aim));
        }

        [Test]
        public void ShotLogFormatsTheDirectionLikeTheOwnersFireLog()
        {
            Assert.That(
                NetHitLog.Shot(1, 5, 2.5f, 0.0123f, -0.0456f, 0.9989f),
                Is.EqualTo("[Net] shot attacker=1 index=5 cone=2.50 dir=(0.0123,-0.0456,0.9989)"));
            Assert.That(
                NetHitLog.ShotIndexCorrected(1, 7, 5, 1),
                Is.EqualTo("[Net] shot index corrected attacker=1 client=7 server=5 generation=1"));
        }
    }
}
