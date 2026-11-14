using System;

namespace Fps.Protocol
{
    /// <summary>
    /// 壊せる建物 1 つの、壊れている部品の集合 (Netcode の NetDestructibleStructure の NetworkList の中身)。
    /// 部品 i が壊れていれば要素 i / 64 のビット i % 64 が立つ (<c>Fps.Core.StructureIntegrity.ToBitset</c> の形)。
    /// </summary>
    /// <remarks>
    /// 壊れるときにしか変わらず、部品が多いと大きいので、毎 tick のスナップショットに載せずに変わったときと途中参加のときに確実な届け方で送る。
    /// </remarks>
    public sealed class StructureStateMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        /// <summary>載せられる 64 ビットの語の最大数。</summary>
        public const int MaxWords = byte.MaxValue;

        private ulong[] _words = Array.Empty<ulong>();

        public ushort Id => MessageIds.StructureState;

        public byte Version => CurrentVersion;

        /// <summary>建物を指す、マップの中で重ならない番号。</summary>
        public int StructureId { get; set; }

        public int WordCount { get; private set; }

        public ulong this[int index]
        {
            get
            {
                if ((uint)index >= (uint)WordCount)
                {
                    throw new ArgumentOutOfRangeException(nameof(index), index, "index must be below " + WordCount);
                }

                return _words[index];
            }
        }

        /// <summary>words を写す。</summary>
        /// <exception cref="ArgumentException">語が <see cref="MaxWords"/> より多い。</exception>
        public void SetWords(ulong[] words)
        {
            if (words == null)
            {
                throw new ArgumentNullException(nameof(words));
            }

            if (words.Length > MaxWords)
            {
                throw new ArgumentException("at most " + MaxWords + " words fit", nameof(words));
            }

            Grow(words.Length);
            Array.Copy(words, _words, words.Length);
            WordCount = words.Length;
        }

        /// <summary>読んだ語を destination の先頭へ写す。</summary>
        /// <exception cref="ArgumentException">destination の長さが <see cref="WordCount"/> と違う。</exception>
        public void CopyWordsTo(ulong[] destination)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            if (destination.Length != WordCount)
            {
                throw new ArgumentException("destination must hold exactly " + WordCount + " words", nameof(destination));
            }

            Array.Copy(_words, destination, WordCount);
        }

        public void Write(MessageWriter writer)
        {
            if (StructureId < 0)
            {
                throw new InvalidOperationException("structure id must not be negative");
            }

            writer.WriteInt32(StructureId);
            writer.WriteByte((byte)WordCount);
            for (var i = 0; i < WordCount; i++)
            {
                writer.WriteUInt64(_words[i]);
            }
        }

        public void Read(MessageReader reader)
        {
            WordCount = 0;
            StructureId = reader.ReadInt32AtLeast(0, "structure id");
            var count = reader.ReadByteBelow(MaxWords + 1, "structure word count");
            Grow(count);
            for (var i = 0; i < count; i++)
            {
                _words[i] = reader.ReadUInt64();
            }

            WordCount = count;
        }

        private void Grow(int count)
        {
            if (count > _words.Length)
            {
                Array.Resize(ref _words, count);
            }
        }
    }
}
