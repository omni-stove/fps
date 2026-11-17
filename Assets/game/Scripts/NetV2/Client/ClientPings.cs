using System;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    // 表示をオフラインと同じ PingController.ShowShared に任せるため、Netcode のクライアントと同じ SharedPingMarkers に写す。
    public sealed class ClientPings
    {
        private readonly Action<string> _log;

        public ClientPings(Action<string> log)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public SharedPingMarkers Markers { get; } = new SharedPingMarkers();

        public bool Handle(IMessage message, float now)
        {
            switch (message)
            {
                case PingPlacedMessage placed:
                    if (Markers.Place(placed.PingId, placed.Kind, placed.Position, placed.Tracking, placed.RemainingSeconds, now))
                    {
                        _log(PingLog.ReceivedPlaced(placed.PingId, placed.Kind, placed.Position, placed.RemainingSeconds, placed.Tracking));
                    }
                    else
                    {
                        _log(PingLog.Tag + " ignored an invalid ping from the server id=" + placed.PingId + " kind=" + placed.Kind
                            + " remaining=" + placed.RemainingSeconds);
                    }

                    return true;
                case PingMovedMessage moved:
                    Markers.Move(moved.PingId, moved.Position);
                    return true;
                case PingLostMessage lost:
                    if (Markers.Lose(lost.PingId, lost.Position))
                    {
                        _log(PingLog.ReceivedLost(lost.PingId, lost.Position));
                    }

                    return true;
                case PingRemovedMessage removed:
                    if (Markers.Remove(removed.PingId))
                    {
                        _log(PingLog.ReceivedRemoved(removed.PingId, removed.Reason));
                    }

                    return true;
                default:
                    return false;
            }
        }
    }
}
