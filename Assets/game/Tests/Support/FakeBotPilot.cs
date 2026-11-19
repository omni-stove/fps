using System;
using System.Collections.Generic;
using Shooter.Runtime;
using UnityEngine;

namespace Shooter.Tests.EditMode
{
    public sealed class FakeBotPilot : MonoBehaviour, IBotPilot
    {
        private Transform[] _waypoints = Array.Empty<Transform>();

        public IVehicleRider Rider { get; set; }

        public IReadOnlyList<Transform> Waypoints => _waypoints;

        public bool FollowWaypointHeight { get; set; }

        public int ReleaseCount { get; private set; }

        public void SetWaypoints(params Transform[] points)
        {
            _waypoints = points != null ? (Transform[])points.Clone() : Array.Empty<Transform>();
        }

        public void Release()
        {
            ReleaseCount++;
        }
    }
}
