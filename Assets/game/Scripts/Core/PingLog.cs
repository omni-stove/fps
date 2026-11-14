using System;
using System.Globalization;
using System.Numerics;

namespace Fps.Core
{
    /// <summary>印 (<see cref="PingMarker"/>) の <c>[Ping]</c> 付きログの文言。</summary>
    public static class PingLog
    {
        public const string Tag = "[Ping]";

        /// <summary>ルールと設定を受け取った。</summary>
        /// <example><c>[Ping] rule=On life=6.00 max=3 distance=150.0</c></example>
        public static string Rule(Ping rule, PingSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} rule={1} life={2:0.00} max={3} distance={4:0.0}",
                Tag,
                rule,
                spec.LifetimeSeconds,
                spec.MaxMarkers,
                spec.MaxDistance);
        }

        /// <summary>印を付けた。target は敵の名前 (場所なら null)。</summary>
        /// <example><c>[Ping] placed id=1 kind=Enemy target=Dummy_0 at=(1.00, 1.10, 10.00) life=6.00 refreshed=false replaced=none</c></example>
        public static string Placed(PingPlacement placement, string target)
        {
            var marker = placement.Marker;
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} placed id={1} kind={2} target={3} at={4} life={5:0.00} refreshed={6} replaced={7}",
                Tag,
                marker.Id,
                marker.Kind,
                Token(target),
                Point(marker.Position),
                marker.ExpiresAt - marker.PlacedAt,
                placement.Refreshed ? "true" : "false",
                placement.Replaced.HasValue ? placement.Replaced.Value.Id.ToString(CultureInfo.InvariantCulture) : "none");
        }

        /// <summary>印を付けようとして付けなかった。</summary>
        /// <example><c>[Ping] refused reason=RuleOff</c></example>
        public static string Refused(PingRefusal reason)
        {
            return Tag + " refused reason=" + reason;
        }

        /// <summary>敵の印が敵を見失い、最後に見た位置に残った。</summary>
        /// <example><c>[Ping] lost id=1 target=Dummy_0 at=(1.00, 1.10, 10.00)</c></example>
        public static string Lost(PingMarker marker, string target)
        {
            return Tag + " lost id=" + Id(marker) + " target=" + Token(target) + " at=" + Point(marker.Position);
        }

        /// <summary>寿命が尽きて印が消えた。</summary>
        /// <example><c>[Ping] expired id=1 kind=Enemy at=(1.00, 1.10, 10.00) tracking=false</c></example>
        public static string Expired(PingMarker marker)
        {
            return Tag + " expired id=" + Id(marker) + " kind=" + marker.Kind + " at=" + Point(marker.Position)
                + " tracking=" + (marker.IsTracking ? "true" : "false");
        }

        /// <summary>敵が倒れたか居なくなったので印を消した。</summary>
        /// <example><c>[Ping] removed id=1 reason=TargetGone target=Dummy_0</c></example>
        public static string TargetGone(PingMarker marker, string target)
        {
            return Tag + " removed id=" + Id(marker) + " reason=TargetGone target=" + Token(target);
        }

        /// <summary>ルールが変わるなどして印をすべて消した。</summary>
        /// <example><c>[Ping] cleared count=2</c></example>
        public static string Cleared(int count)
        {
            return Tag + " cleared count=" + count.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>HUD で印が画面の中に入ったか、端に寄った。</summary>
        /// <example><c>[Ping] screen id=1 onScreen=false edge=Left angle=-90.0 at=(0.03, 0.50)</c></example>
        public static string Screen(int id, PingScreenPlacement placement)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} screen id={1} onScreen={2} edge={3} angle={4:0.0} at=({5:0.00}, {6:0.00})",
                Tag,
                id,
                placement.IsOnScreen ? "true" : "false",
                placement.Edge,
                placement.AngleDegrees,
                placement.X,
                placement.Y);
        }

        /// <summary>味方の bot が、印の付いた敵を狙うことにした。</summary>
        /// <example><c>[Ping] ally id=Bot_1 side=0 target=Enemy_3</c></example>
        public static string AllyTarget(string botId, Side side, string target)
        {
            return Tag + " ally id=" + Token(botId) + " side=" + side.Id.ToString(CultureInfo.InvariantCulture) + " target=" + Token(target);
        }

        /// <summary>HUD に印を出し始めた。view は出す部品の名前。</summary>
        /// <example><c>[Ping] view=PingMarkersView</c></example>
        public static string View(string view)
        {
            return Tag + " view=" + Token(view);
        }

        /// <example><c>[Ping] shared</c></example>
        public static string Shared()
        {
            return Tag + " shared";
        }

        /// <example><c>[Ping] net placed id=4 player=1 team=Alpha kind=Enemy target=2 at=(1.00, 1.10, 10.00) life=6.00 refreshed=false recipients=2</c></example>
        public static string NetPlaced(SharedPing ping, bool refreshed, int recipients)
        {
            var marker = ping.Marker;
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} net placed id={1} player={2} team={3} kind={4} target={5} at={6} life={7:0.00} refreshed={8} recipients={9}",
                Tag,
                marker.Id,
                ping.PingerId,
                MatchLog.TeamName(ping.Team),
                marker.Kind,
                marker.TargetKey.HasValue ? marker.TargetKey.Value.ToString(CultureInfo.InvariantCulture) : "none",
                Point(marker.Position),
                marker.ExpiresAt - marker.PlacedAt,
                refreshed ? "true" : "false",
                recipients);
        }

        /// <example><c>[Ping] net refused player=1 reason=TooSoon</c></example>
        public static string NetRefused(int playerId, PingRefusal reason)
        {
            return Tag + " net refused player=" + playerId.ToString(CultureInfo.InvariantCulture) + " reason=" + reason;
        }

        /// <example><c>[Ping] net lost id=4 player=1 at=(1.00, 1.10, 10.00)</c></example>
        public static string NetLost(SharedPing ping)
        {
            return Tag + " net lost id=" + Id(ping.Marker) + " player=" + ping.PingerId.ToString(CultureInfo.InvariantCulture)
                + " at=" + Point(ping.Marker.Position);
        }

        /// <example><c>[Ping] net removed id=4 player=1 reason=Expired</c></example>
        public static string NetRemoved(SharedPing ping, PingRemoval reason)
        {
            return Tag + " net removed id=" + Id(ping.Marker) + " player=" + ping.PingerId.ToString(CultureInfo.InvariantCulture)
                + " reason=" + reason;
        }

        /// <example><c>[Ping] received placed id=4 kind=Enemy at=(1.00, 1.10, 10.00) life=5.95 tracking=true</c></example>
        public static string ReceivedPlaced(int id, PingKind kind, Vector3 position, float remainingSeconds, bool tracking)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} received placed id={1} kind={2} at={3} life={4:0.00} tracking={5}",
                Tag,
                id,
                kind,
                Point(position),
                remainingSeconds,
                tracking ? "true" : "false");
        }

        /// <example><c>[Ping] received lost id=4 at=(1.00, 1.10, 10.00)</c></example>
        public static string ReceivedLost(int id, Vector3 position)
        {
            return Tag + " received lost id=" + id.ToString(CultureInfo.InvariantCulture) + " at=" + Point(position);
        }

        /// <example><c>[Ping] received removed id=4 reason=Expired</c></example>
        public static string ReceivedRemoved(int id, PingRemoval reason)
        {
            return Tag + " received removed id=" + id.ToString(CultureInfo.InvariantCulture) + " reason=" + reason;
        }

        /// <summary>印 1 つの説明。</summary>
        /// <example><c>id=1 kind=Enemy key=42 at=(1.00, 1.10, 10.00) tracking=true expiresAt=6.00</c></example>
        public static string Describe(PingMarker marker)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "id={0} kind={1} key={2} at={3} tracking={4} expiresAt={5:0.00}",
                marker.Id,
                marker.Kind,
                marker.TargetKey.HasValue ? marker.TargetKey.Value.ToString(CultureInfo.InvariantCulture) : "none",
                Point(marker.Position),
                marker.IsTracking ? "true" : "false",
                marker.ExpiresAt);
        }

        private static string Id(PingMarker marker) => marker.Id.ToString(CultureInfo.InvariantCulture);

        private static string Point(Vector3 point)
        {
            return string.Format(CultureInfo.InvariantCulture, "({0:0.00}, {1:0.00}, {2:0.00})", point.X, point.Y, point.Z);
        }

        // 名前に空白があってもログの区切りが崩れないよう、空白は _ に、空なら none にする。
        private static string Token(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "none";
            }

            var chars = text.Trim().ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                if (char.IsWhiteSpace(chars[i]))
                {
                    chars[i] = '_';
                }
            }

            return new string(chars);
        }
    }
}
