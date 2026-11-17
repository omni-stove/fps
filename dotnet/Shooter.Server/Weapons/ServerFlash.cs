using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Shooter.Core;

namespace Shooter.Server
{
    internal sealed class ServerFlash
    {
        public const string LogTag = "[Flash]";

        // 目の位置で弾けたときに、向きの線が長さ 0 にならないよう測らない
        private const float _minSightLength = 1e-4f;

        private readonly List<int> _targets = new List<int>();

        public ServerFlash(ServerCombat combat)
        {
            Combat = combat ?? throw new ArgumentNullException(nameof(combat));
        }

        public ServerCombat Combat { get; }

        public int Detonate(Vector3 center, BlindSpec blind, int? throwerId, string weaponId)
        {
            ArgumentNullException.ThrowIfNull(blind);
            ArgumentNullException.ThrowIfNull(weaponId);

            // かけた知らせで一覧が変わっても回し続けられるよう、写してから回す
            _targets.Clear();
            foreach (var body in Combat.Context.Bodies.All)
            {
                _targets.Add(body.PlayerId);
            }

            var blinded = 0;
            foreach (var playerId in _targets)
            {
                if (!Combat.IsHittable(playerId) || !Combat.Context.Bodies.TryGet(playerId, out var body))
                {
                    continue;
                }

                var eye = body.EyePosition;
                var seconds = BlindEffect.Seconds(blind, center, eye, Facing(body.Yaw));
                if (!(seconds > 0f))
                {
                    continue;
                }

                if (IsSightBlocked(center, eye))
                {
                    Combat.Context.Log.WriteLine(LogTag + " blocked victim=" + playerId.ToString(CultureInfo.InvariantCulture));
                    continue;
                }

                Combat.StatusEffects.Afflict(throwerId, weaponId, playerId, BlindEffect.StatusEffect(seconds));
                blinded++;
            }

            Combat.Context.Log.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "{0} net flashed thrower={1} at=({2:0.0}, {3:0.0}, {4:0.0}) blinded={5}",
                LogTag,
                throwerId.HasValue ? throwerId.Value.ToString(CultureInfo.InvariantCulture) : "none",
                center.X,
                center.Y,
                center.Z,
                blinded));
            return blinded;
        }

        // サーバーは人の上下の向きを持たないので、水平の向きで測る
        private static Vector3 Facing(float yawRadians)
        {
            return new Vector3(MathF.Sin(yawRadians), 0f, MathF.Cos(yawRadians));
        }

        private bool IsSightBlocked(Vector3 from, Vector3 to)
        {
            var offset = to - from;
            var length = offset.Length();
            if (length < _minSightLength)
            {
                return false;
            }

            return Combat.Context.World.Raycast(from, offset / length, length, ServerHitboxes.RaycastLayers, out _)
                || Combat.SightBlockers.Blocks(from, to);
        }
    }
}
