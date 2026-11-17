using System;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    /// <summary>
    /// サーバーから届いた印 (付けた人の味方にだけ届く) を、Netcode のクライアントと同じ <see cref="SharedPingMarkers"/> に写す。
    /// 表示はオフラインと同じ <c>PingController.ShowShared</c> が <see cref="Markers"/> を読む。
    /// </summary>
    public sealed class ClientPings
    {
        private readonly Action<string> _log;

        /// <param name="log"><c>[Ping]</c> 付きの 1 行を受け取る先。</param>
        public ClientPings(Action<string> log)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public SharedPingMarkers Markers { get; } = new SharedPingMarkers();

        /// <summary>印のメッセージなら写して true。now はこちらの時計 (秒) で、消える時刻はそこから数える。</summary>
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
