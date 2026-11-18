using System;
using System.Globalization;
using Shooter.Core;

namespace Shooter.Protocol
{
    // 読んだ並びがそのまま KillCamClip になるよう、0 から始まり増え続ける秒という KillCamClip の決まりを読むときにも確かめる。
    public sealed class KillCamClipMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public const int MaxSamples = 1024;

        private float[] _offsets = Array.Empty<float>();
        private KillCamPose[] _poses = Array.Empty<KillCamPose>();

        public ushort Id => MessageIds.KillCamClip;

        public byte Version => CurrentVersion;

        public int KillerId { get; set; }

        public int VictimId { get; set; }

        public int Count { get; private set; }

        public float OffsetAt(int index)
        {
            CheckIndex(index);
            return _offsets[index];
        }

        public KillCamPose PoseAt(int index)
        {
            CheckIndex(index);
            return _poses[index];
        }

        public void Clear()
        {
            Count = 0;
        }

        public void Add(float offset, KillCamPose pose)
        {
            if (Count == MaxSamples)
            {
                throw new InvalidOperationException("a kill cam clip carries at most " + MaxSamples + " samples");
            }

            if (float.IsNaN(offset) || float.IsInfinity(offset))
            {
                throw new ArgumentOutOfRangeException(nameof(offset), offset, "offset must be finite");
            }

            if (Count == 0 ? offset != 0f : offset <= _offsets[Count - 1])
            {
                throw new ArgumentOutOfRangeException(nameof(offset), offset, "offsets must start at 0 and strictly increase");
            }

            Grow(Count + 1);
            _offsets[Count] = offset;
            _poses[Count] = pose;
            Count++;
        }

        public KillCamClip ToClip()
        {
            if (Count == 0)
            {
                return KillCamClip.Empty;
            }

            var offsets = new float[Count];
            var poses = new KillCamPose[Count];
            Array.Copy(_offsets, offsets, Count);
            Array.Copy(_poses, poses, Count);
            return new KillCamClip(offsets, poses);
        }

        public void Write(MessageWriter writer)
        {
            if (KillerId < 0 || VictimId < 0)
            {
                throw new InvalidOperationException("killer and victim ids must not be negative");
            }

            writer.WriteInt32(KillerId);
            writer.WriteInt32(VictimId);
            writer.WriteUInt16((ushort)Count);
            for (var i = 0; i < Count; i++)
            {
                writer.WriteSingle(_offsets[i]);
                writer.WriteVector3(_poses[i].Eye);
                writer.WriteSingle(_poses[i].Yaw);
            }
        }

        public void Read(MessageReader reader)
        {
            Count = 0;
            KillerId = reader.ReadInt32AtLeast(0, "kill cam killer");
            VictimId = reader.ReadInt32AtLeast(0, "kill cam victim");
            var count = (int)reader.ReadUInt16();
            if (count > MaxSamples)
            {
                throw new MessageReadException(
                    "a kill cam clip carries " + count.ToString(CultureInfo.InvariantCulture) + " samples; at most "
                    + MaxSamples.ToString(CultureInfo.InvariantCulture) + " are allowed");
            }

            Grow(count);
            for (var i = 0; i < count; i++)
            {
                var offset = reader.ReadFiniteSingle("kill cam offset");
                if (i == 0 ? offset != 0f : offset <= _offsets[i - 1])
                {
                    throw new MessageReadException("kill cam offsets must start at 0 and strictly increase");
                }

                var eye = reader.ReadFiniteVector3("kill cam eye");
                var yaw = reader.ReadFiniteSingle("kill cam yaw");
                _offsets[i] = offset;
                _poses[i] = new KillCamPose(eye, yaw);
            }

            Count = count;
        }

        private void CheckIndex(int index)
        {
            if ((uint)index >= (uint)Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "index must be below " + Count);
            }
        }

        private void Grow(int count)
        {
            if (count > _offsets.Length)
            {
                var size = Math.Max(count, Math.Min(MaxSamples, Math.Max(16, _offsets.Length * 2)));
                Array.Resize(ref _offsets, size);
                Array.Resize(ref _poses, size);
            }
        }
    }
}
