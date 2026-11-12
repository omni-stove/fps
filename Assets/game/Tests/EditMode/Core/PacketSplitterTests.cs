using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class PacketSplitterTests
    {
        private const int _packetSize = 16;

        private static byte[] Message(int length)
        {
            var bytes = new byte[length];
            for (var i = 0; i < length; i++)
            {
                bytes[i] = (byte)(i * 7 + 3);
            }

            return bytes;
        }

        private static List<byte[]> Split(PacketSplitter splitter, byte[] message)
        {
            var packets = new List<byte[]>();
            splitter.Split(new ArraySegment<byte>(message), packet =>
            {
                var copy = new byte[packet.Count];
                Buffer.BlockCopy(packet.Array, packet.Offset, copy, 0, packet.Count);
                packets.Add(copy);
            });
            return packets;
        }

        private static byte[] ToArray(ArraySegment<byte> segment)
        {
            var copy = new byte[segment.Count];
            Buffer.BlockCopy(segment.Array, segment.Offset, copy, 0, segment.Count);
            return copy;
        }

        [Test]
        public void SmallMessageIsOnePacket()
        {
            var splitter = new PacketSplitter(_packetSize);
            var message = Message(_packetSize - PacketSplitter.WholeHeaderSize);

            var packets = Split(splitter, message);

            Assert.That(packets, Has.Count.EqualTo(1));
            Assert.That(packets[0], Has.Length.EqualTo(_packetSize));
            Assert.That(packets[0][0], Is.EqualTo(PacketSplitter.WholeTag));

            var reassembler = new PacketReassembler(1024, 4);
            Assert.That(reassembler.Read(new ArraySegment<byte>(packets[0]), out var result), Is.EqualTo(PacketReadResult.Complete));
            Assert.That(ToArray(result), Is.EqualTo(message));
        }

        [Test]
        public void EmptyMessageRoundTrips()
        {
            var packets = Split(new PacketSplitter(_packetSize), Array.Empty<byte>());
            var reassembler = new PacketReassembler(1024, 4);

            Assert.That(reassembler.Read(new ArraySegment<byte>(packets[0]), out var result), Is.EqualTo(PacketReadResult.Complete));
            Assert.That(result.Count, Is.Zero);
        }

        [Test]
        public void LargeMessageIsSplitWithinPacketSize()
        {
            var splitter = new PacketSplitter(_packetSize);
            var chunk = _packetSize - PacketSplitter.FragmentHeaderSize;
            var message = Message(chunk * 3 + 1);

            var packets = Split(splitter, message);

            Assert.That(packets, Has.Count.EqualTo(4));
            foreach (var packet in packets)
            {
                Assert.That(packet.Length, Is.LessThanOrEqualTo(_packetSize));
                Assert.That(packet[0], Is.EqualTo(PacketSplitter.FragmentTag));
            }

            var reassembler = new PacketReassembler(1024, 4);
            for (var i = 0; i < packets.Count - 1; i++)
            {
                Assert.That(reassembler.Read(new ArraySegment<byte>(packets[i]), out _), Is.EqualTo(PacketReadResult.Incomplete));
            }

            Assert.That(reassembler.Read(new ArraySegment<byte>(packets[packets.Count - 1]), out var result),
                Is.EqualTo(PacketReadResult.Complete));
            Assert.That(ToArray(result), Is.EqualTo(message));
            Assert.That(reassembler.PendingCount, Is.Zero);
        }

        [Test]
        public void FragmentsMayArriveOutOfOrderAndInterleaved()
        {
            var splitter = new PacketSplitter(_packetSize);
            var first = Message(40);
            var second = Message(30);
            Array.Reverse(second);
            var firstPackets = Split(splitter, first);
            var secondPackets = Split(splitter, second);
            var reassembler = new PacketReassembler(1024, 4);
            var completed = new List<byte[]>();

            var order = new List<byte[]>();
            for (var i = Math.Max(firstPackets.Count, secondPackets.Count) - 1; i >= 0; i--)
            {
                if (i < secondPackets.Count)
                {
                    order.Add(secondPackets[i]);
                }

                if (i < firstPackets.Count)
                {
                    order.Add(firstPackets[i]);
                }
            }

            foreach (var packet in order)
            {
                if (reassembler.Read(new ArraySegment<byte>(packet), out var result) == PacketReadResult.Complete)
                {
                    completed.Add(ToArray(result));
                }
            }

            Assert.That(completed, Has.Count.EqualTo(2));
            Assert.That(completed, Has.Member(first));
            Assert.That(completed, Has.Member(second));
        }

        [Test]
        public void ReadsPacketsAtAnOffset()
        {
            var packets = Split(new PacketSplitter(_packetSize), Message(5));
            var padded = new byte[packets[0].Length + 3];
            Buffer.BlockCopy(packets[0], 0, padded, 3, packets[0].Length);
            var reassembler = new PacketReassembler(1024, 4);

            Assert.That(reassembler.Read(new ArraySegment<byte>(padded, 3, packets[0].Length), out var result),
                Is.EqualTo(PacketReadResult.Complete));
            Assert.That(ToArray(result), Is.EqualTo(Message(5)));
        }

        [Test]
        public void EvictsOldestIncompleteMessage()
        {
            var splitter = new PacketSplitter(_packetSize);
            var reassembler = new PacketReassembler(1024, 2);
            var a = Split(splitter, Message(20));
            var b = Split(splitter, Message(20));
            var c = Split(splitter, Message(20));

            reassembler.Read(new ArraySegment<byte>(a[0]), out _);
            reassembler.Read(new ArraySegment<byte>(b[0]), out _);
            reassembler.Read(new ArraySegment<byte>(c[0]), out _);

            Assert.That(reassembler.PendingCount, Is.EqualTo(2));
            // a は捨てられたので、残りの断片だけでは揃わない
            Assert.That(reassembler.Read(new ArraySegment<byte>(a[1]), out _), Is.EqualTo(PacketReadResult.Incomplete));
            Assert.That(reassembler.Read(new ArraySegment<byte>(c[1]), out _), Is.Not.EqualTo(PacketReadResult.Malformed));
        }

        [Test]
        public void RejectsMessagesOverTheLimit()
        {
            var splitter = new PacketSplitter(_packetSize);
            var reassembler = new PacketReassembler(20, 4);
            var packets = Split(splitter, Message(30));

            var results = new List<PacketReadResult>();
            foreach (var packet in packets)
            {
                results.Add(reassembler.Read(new ArraySegment<byte>(packet), out _));
            }

            Assert.That(results, Has.Member(PacketReadResult.Malformed));
            Assert.That(results, Has.No.Member(PacketReadResult.Complete));
        }

        [Test]
        public void RejectsMalformedPackets()
        {
            var reassembler = new PacketReassembler(1024, 4);

            Assert.That(reassembler.Read(new ArraySegment<byte>(Array.Empty<byte>()), out _), Is.EqualTo(PacketReadResult.Malformed));
            Assert.That(reassembler.Read(new ArraySegment<byte>(new byte[] { 9, 1, 2 }), out _), Is.EqualTo(PacketReadResult.Malformed));
            // 断片なのにヘッダしか無い
            Assert.That(reassembler.Read(new ArraySegment<byte>(new byte[] { PacketSplitter.FragmentTag, 0, 0, 0, 0, 2, 0 }), out _),
                Is.EqualTo(PacketReadResult.Malformed));
            // 番号が総数以上
            Assert.That(reassembler.Read(new ArraySegment<byte>(new byte[] { PacketSplitter.FragmentTag, 0, 0, 2, 0, 2, 0, 1 }), out _),
                Is.EqualTo(PacketReadResult.Malformed));
            // 総数が 1 (分ける必要が無い)
            Assert.That(reassembler.Read(new ArraySegment<byte>(new byte[] { PacketSplitter.FragmentTag, 0, 0, 0, 0, 1, 0, 1 }), out _),
                Is.EqualTo(PacketReadResult.Malformed));
        }

        [Test]
        public void RejectsDuplicateFragment()
        {
            var packets = Split(new PacketSplitter(_packetSize), Message(30));
            var reassembler = new PacketReassembler(1024, 4);

            reassembler.Read(new ArraySegment<byte>(packets[0]), out _);

            Assert.That(reassembler.Read(new ArraySegment<byte>(packets[0]), out _), Is.EqualTo(PacketReadResult.Malformed));
            Assert.That(reassembler.PendingCount, Is.Zero);
        }

        [Test]
        public void ReportsLimits()
        {
            var splitter = new PacketSplitter(1170);

            Assert.That(splitter.MaxPacketSize, Is.EqualTo(1170));
            Assert.That(splitter.MaxMessageSize, Is.EqualTo(ushort.MaxValue * (1170 - PacketSplitter.FragmentHeaderSize)));
        }

        [Test]
        public void ValidatesArguments()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PacketSplitter(PacketSplitter.FragmentHeaderSize));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PacketReassembler(0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PacketReassembler(1, 0));

            var splitter = new PacketSplitter(_packetSize);
            Assert.Throws<ArgumentException>(() => splitter.Split(default, _ => { }));
            Assert.Throws<ArgumentNullException>(() => splitter.Split(new ArraySegment<byte>(Message(1)), null));
        }
    }
}
