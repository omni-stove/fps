using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Shooter.Core;

namespace Shooter.Protocol
{
    // 構造体の VehicleSnapshot に載せるので、代入で差分の基準と中身を共有しても基準が書き換わらないよう、作った後は変えない。
    // 速さと燃えた時間を送らないのは、クライアントはフレアを自分で動かさず、届いた位置で捕捉が外れるかを見るだけのため。
    public sealed class VehicleFlareSync
    {
        public const int MaxFlares = 16;

        private readonly int[] _ids;
        private readonly Vector3[] _positions;

        private VehicleFlareSync(int[] ids, Vector3[] positions)
        {
            _ids = ids;
            _positions = positions;
        }

        public static VehicleFlareSync None { get; } = new VehicleFlareSync(Array.Empty<int>(), Array.Empty<Vector3>());

        public int Count => _ids.Length;

        // CountermeasurePod は撒いた順に並べるので、収まらないときは先に燃え尽きる古いものを外す。
        public static VehicleFlareSync From(IReadOnlyList<CountermeasureFlare> flares)
        {
            if (flares == null || flares.Count == 0)
            {
                return None;
            }

            var skip = Math.Max(0, flares.Count - MaxFlares);
            var count = flares.Count - skip;
            var ids = new int[count];
            var positions = new Vector3[count];
            for (var i = 0; i < count; i++)
            {
                var flare = flares[skip + i];
                if (flare.Id < 0)
                {
                    throw new ArgumentException("flare id must not be negative", nameof(flares));
                }

                if (!IsFinite(flare.Position))
                {
                    throw new ArgumentException("flare position must be finite", nameof(flares));
                }

                ids[i] = flare.Id;
                positions[i] = flare.Position;
            }

            return new VehicleFlareSync(ids, positions);
        }

        public int IdAt(int index)
        {
            Check(index);
            return _ids[index];
        }

        public Vector3 PositionAt(int index)
        {
            Check(index);
            return _positions[index];
        }

        public bool TryLocate(int flareId, out Vector3 position)
        {
            for (var i = 0; i < _ids.Length; i++)
            {
                if (_ids[i] == flareId)
                {
                    position = _positions[i];
                    return true;
                }
            }

            position = default;
            return false;
        }

        public bool SameAs(VehicleFlareSync other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (other == null || other.Count != Count)
            {
                return false;
            }

            for (var i = 0; i < Count; i++)
            {
                if (_ids[i] != other._ids[i] || !Same.Bits(_positions[i], other._positions[i]))
                {
                    return false;
                }
            }

            return true;
        }

        internal void Write(MessageWriter writer)
        {
            writer.WriteByte((byte)Count);
            for (var i = 0; i < Count; i++)
            {
                writer.WriteInt32(_ids[i]);
                writer.WriteVector3(_positions[i]);
            }
        }

        internal static VehicleFlareSync Read(MessageReader reader)
        {
            var count = reader.ReadByteBelow(MaxFlares + 1, "vehicle flare count");
            if (count == 0)
            {
                return None;
            }

            var ids = new int[count];
            var positions = new Vector3[count];
            for (var i = 0; i < count; i++)
            {
                var id = reader.ReadInt32AtLeast(0, "vehicle flare id");
                for (var j = 0; j < i; j++)
                {
                    if (ids[j] == id)
                    {
                        throw new MessageReadException("vehicle flare " + Text(id) + " is listed twice");
                    }
                }

                ids[i] = id;
                positions[i] = reader.ReadFiniteVector3("vehicle flare position");
            }

            return new VehicleFlareSync(ids, positions);
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.X) && !float.IsInfinity(value.X)
                && !float.IsNaN(value.Y) && !float.IsInfinity(value.Y)
                && !float.IsNaN(value.Z) && !float.IsInfinity(value.Z);
        }

        private void Check(int index)
        {
            if ((uint)index >= (uint)Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "index must be below " + Text(Count));
            }
        }

        private static string Text(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
