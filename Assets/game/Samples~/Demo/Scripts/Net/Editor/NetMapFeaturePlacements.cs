using System;
using UnityEngine;

namespace Shooter.Editor
{
    public readonly struct NetMapLadderTower
    {
        public NetMapLadderTower(string name, Vector3 baseCenter, Vector3 size, string side, float ladderWidth)
        {
            Name = name;
            BaseCenter = baseCenter;
            Size = size;
            Side = side;
            LadderWidth = ladderWidth;
        }

        public string Name { get; }

        public Vector3 BaseCenter { get; }

        public Vector3 Size { get; }

        public string Side { get; }

        public float LadderWidth { get; }

        public Vector3 SideDirection => NetMapFeatureLayout.TryGetSideDirection(Side, out var direction)
            ? direction
            : throw new InvalidOperationException("ladder tower '" + Name + "' has the side '" + Side + "'");
    }

    public readonly struct NetMapWaterPool
    {
        public NetMapWaterPool(string name, Vector3 groundCenter, Vector3 size, float surfaceHeight)
        {
            Name = name;
            GroundCenter = groundCenter;
            Size = size;
            SurfaceHeight = surfaceHeight;
        }

        public string Name { get; }

        public Vector3 GroundCenter { get; }

        public Vector3 Size { get; }

        public float SurfaceHeight { get; }

        public Bounds Footprint => new Bounds(GroundCenter + (Vector3.up * (Size.y / 2f)), Size);
    }

    public readonly struct NetMapVehicleSpot
    {
        public NetMapVehicleSpot(string name, string kind, Vector3 position, float yawDegrees)
        {
            Name = name;
            Kind = kind;
            Position = position;
            YawDegrees = yawDegrees;
        }

        public string Name { get; }

        public string Kind { get; }

        public Vector3 Position { get; }

        public float YawDegrees { get; }

        public Quaternion Rotation => Quaternion.Euler(0f, YawDegrees, 0f);
    }
}
