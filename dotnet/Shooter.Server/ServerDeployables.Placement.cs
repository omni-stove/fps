using System;
using System.Numerics;
using Shooter.Core;

namespace Shooter.Server
{
    // 置けるかの決まりは Core (DeployPlacement) が持ち、ここはサーバーの当たりで調べた結果を渡すだけにする (Unity の DeploySurveyor と同じ分け方)。
    // 足場は送られた地点の真下だけを探す。置き場所はクライアントが自分の画面の見本と同じ所を送ってくるため。
    internal sealed partial class ServerDeployables
    {
        // Unity の DeploySurveyor と同じ値で調べる。
        public const float CornerProbeMargin = 0.05f;

        public const float ClearanceSkin = 0.02f;

        // Unity の DeploySurveyor が足元から探す上下の幅と同じ
        public const float MaxVerticalReach = 2.5f;

        // 射線の終点は足場の面に当たらないよう、置く地点より少し上にする
        public const float SightLift = 0.1f;

        private const int _overlapCapacity = 8;

        private readonly int[] _overlaps = new int[_overlapCapacity];

        // 持ち主の画面の位置はサーバーより遅れているので、届く距離には撃った弾の始点と同じだけのずれを許す。
        // 面に貼る C4 は、オフラインの RemoteChargeDeployDefinition.Survey と同じく体の中心からのまっすぐな距離で測る
        private bool InReach(DeployType type, ServerBody body, Vector3 position)
        {
            var allowed = type.Deploy.MaxRange + _data.Settings.MaxShotOriginOffset;
            if (type.Role == DeployRole.RemoteCharge)
            {
                return Vector3.Distance(position, body.Center) <= allowed;
            }

            var offset = position - body.Position;
            var horizontal = MathF.Sqrt((offset.X * offset.X) + (offset.Z * offset.Z));

            // 真上の屋根や壁の向こうの床を送られても置けないよう、上下のずれに上限を置く
            return horizontal <= allowed && MathF.Abs(offset.Y) <= MaxVerticalReach;
        }

        // 床に立てる物の置き場所。持ち主の体から置く地点まで壁が無いかも見る
        private ServerDeployRefusal Stand(
            ServerBody body, DeploySpec spec, Vector3 position, float facing, out Vector3 point, out Vector3 normal, out int surfaceId)
        {
            normal = Vector3.UnitY;
            surfaceId = 0;
            var verdict = Survey(spec, position, facing, out point);
            if (verdict != DeployVerdict.Ok)
            {
                return RefusalOf(verdict);
            }

            // 置く地点の物 (梁など) は足場の判定が Blocked で断るので、射線はその後に見る
            var sight = position + new Vector3(0f, SightLift, 0f) - body.Center;
            var sightDistance = sight.Length();
            if (sightDistance > 1e-4f
                && _context.World.Raycast(body.Center, sight / sightDistance, sightDistance, ServerHitboxes.RaycastLayers, out _))
            {
                return ServerDeployRefusal.OutOfReach;
            }

            return ServerDeployRefusal.None;
        }

        private DeployVerdict Survey(DeploySpec spec, Vector3 position, float yaw, out Vector3 point)
        {
            point = position;
            var reach = spec.MaxStepHeight + CornerProbeMargin;
            if (!Down(position + new Vector3(0f, reach, 0f), reach * 2f, out var ground))
            {
                return DeployPlacement.Judge(spec, DeploySurvey.NoGround);
            }

            // 面の向きが壊れた当たり (縮退した三角形など) で例外にするより、足場が無いとして断る
            var normal = ground.Normal;
            if (!IsFinite(normal) || normal.LengthSquared() < 1e-12f)
            {
                return DeployPlacement.Judge(spec, DeploySurvey.NoGround);
            }

            point = ground.Point;
            if (DeployPlacement.SlopeDegrees(normal) > spec.MaxSlopeDegrees)
            {
                return DeployPlacement.Judge(spec, new DeploySurvey(normal, 0f, 0f, true, false));
            }

            // 四隅は Unity と同じく足場の面に沿わせた所で測る。真上に立てた形で測ると、置ける傾きの坂でも隅の高低差が段差に数えられてしまう
            var up = Vector3.Normalize(normal);
            var forward = Forward(yaw);
            var facing = forward - (up * Vector3.Dot(forward, up));
            facing = facing.LengthSquared() > 1e-8f ? Vector3.Normalize(facing) : forward;
            var right = Vector3.Cross(up, facing);
            var lowest = 0f;
            var highest = 0f;
            var grounded = true;
            var halfWidth = spec.Width / 2f;
            var halfDepth = spec.Depth / 2f;
            for (var i = 0; i < 4; i++)
            {
                var corner = point + (right * ((i & 1) == 0 ? -halfWidth : halfWidth)) + (facing * ((i & 2) == 0 ? -halfDepth : halfDepth));
                if (!Down(corner + new Vector3(0f, reach, 0f), reach * 2f, out var hit))
                {
                    grounded = false;
                    continue;
                }

                var deviation = hit.Point.Y - corner.Y;
                lowest = Math.Min(lowest, deviation);
                highest = Math.Max(highest, deviation);
            }

            var blocked = IsBlocked(spec, point, yaw);
            return DeployPlacement.Judge(spec, new DeploySurvey(normal, lowest, highest, grounded, blocked));
        }

        private bool Down(Vector3 origin, float distance, out WorldHit hit)
        {
            return _context.World.Raycast(origin, -Vector3.UnitY, distance, ServerHitboxes.RaycastLayers, out hit);
        }

        // 足元の段差の分を浮かせ、接している壁まで塞いでいると数えないよう縮めて調べる (Unity の DeploySurveyor.IsBlocked と同じ)。
        // 持ち主の体も数える。自分の立っている所に置かせないため
        private bool IsBlocked(DeploySpec spec, Vector3 point, float yaw)
        {
            var lift = spec.MaxStepHeight + ClearanceSkin;
            var boxHeight = Math.Max(ClearanceSkin, spec.Height - lift);
            var halfWidth = Math.Max(ClearanceSkin, (spec.Width / 2f) - ClearanceSkin);
            var halfDepth = Math.Max(ClearanceSkin, (spec.Depth / 2f) - ClearanceSkin);
            var bottom = point.Y + lift;
            var top = bottom + boxHeight;
            var right = Right(yaw);
            var forward = Forward(yaw);
            return IsWorldInBox(point, right, forward, halfWidth, halfDepth, bottom, top)
                || IsBodyInBox(point, right, forward, halfWidth, halfDepth, bottom, top)
                || IsDeployableInBox(point, yaw, spec, bottom, top);
        }

        // サーバーの当たりは箱で重なりを調べられないので、狭い辺を直径にしたカプセルを長い辺に沿って並べて箱を埋める
        private bool IsWorldInBox(Vector3 point, Vector3 right, Vector3 forward, float halfWidth, float halfDepth, float bottom, float top)
        {
            var radius = Math.Min(halfWidth, halfDepth);
            var longHalf = Math.Max(halfWidth, halfDepth);
            var along = halfWidth >= halfDepth ? right : forward;
            var count = Math.Max(1, (int)MathF.Ceiling(longHalf / radius));
            var span = longHalf - radius;
            var low = Math.Min(bottom + radius, top - radius);
            var high = Math.Max(low, top - radius);
            for (var i = 0; i < count; i++)
            {
                var offset = count == 1 ? 0f : -span + (2f * span * i / (count - 1));
                var center = point + (along * offset);
                var p0 = new Vector3(center.X, low, center.Z);
                var p1 = new Vector3(center.X, high, center.Z);
                if (_context.World.OverlapCapsule(p0, p1, radius, ServerHitboxes.RaycastLayers, _overlaps) > 0)
                {
                    Array.Clear(_overlaps, 0, _overlaps.Length);
                    return true;
                }
            }

            return false;
        }

        private bool IsBodyInBox(Vector3 point, Vector3 right, Vector3 forward, float halfWidth, float halfDepth, float bottom, float top)
        {
            foreach (var body in _bodies.All)
            {
                if (!body.HasSpawned || !_combat.IsHittable(body.PlayerId))
                {
                    continue;
                }

                body.Capsule(out var capsuleBottom, out var capsuleTop);
                var radius = body.Radius;
                if (capsuleTop.Y + radius <= bottom || capsuleBottom.Y - radius >= top)
                {
                    continue;
                }

                var offset = body.Position - point;
                if (Math.Abs(Vector3.Dot(offset, right)) < halfWidth + radius && Math.Abs(Vector3.Dot(offset, forward)) < halfDepth + radius)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsDeployableInBox(Vector3 point, float yaw, DeploySpec spec, float bottom, float top)
        {
            foreach (var other in _placed.Values)
            {
                var otherSpec = other.Type.Deploy;
                if (other.Position.Y + otherSpec.Height <= bottom || other.Position.Y >= top)
                {
                    continue;
                }

                if (Overlaps(point, yaw, spec.Width / 2f, spec.Depth / 2f, other.Position, other.Yaw, otherSpec.Width / 2f, otherSpec.Depth / 2f))
                {
                    return true;
                }
            }

            return false;
        }

        // 立てた 2 つの箱の足元の長方形が重なるか (分離軸)。どちらも真上を向いて立つので、上から見た向きの 4 本の軸だけを見ればよい
        private static bool Overlaps(Vector3 a, float yawA, float halfWidthA, float halfDepthA, Vector3 b, float yawB, float halfWidthB, float halfDepthB)
        {
            var axes = new[] { Right(yawA), Forward(yawA), Right(yawB), Forward(yawB) };
            var between = b - a;
            between.Y = 0f;
            foreach (var axis in axes)
            {
                var reachA = (halfWidthA * Math.Abs(Vector3.Dot(Right(yawA), axis))) + (halfDepthA * Math.Abs(Vector3.Dot(Forward(yawA), axis)));
                var reachB = (halfWidthB * Math.Abs(Vector3.Dot(Right(yawB), axis))) + (halfDepthB * Math.Abs(Vector3.Dot(Forward(yawB), axis)));
                if (Math.Abs(Vector3.Dot(between, axis)) >= reachA + reachB)
                {
                    return false;
                }
            }

            return true;
        }

        // 置いた物の形は足元が原点・前が +Z の立てた箱 (DeployAbilityDefinition の決まり)。
        // 始点が箱の中なら当てない。Unity の Raycast も始点を含むコライダーには当たらず、置いた物の当たりを知らないサーバーの移動では
        // 人が置いた物の中へ入れるので、中から撃った弾をその場で止めないため
        private static bool RayBox(
            Vector3 origin, Vector3 unit, float maxDistance, Vector3 position, float yaw, DeploySpec spec, out float distance, out Vector3 normal)
        {
            distance = 0f;
            normal = -unit;
            var right = Right(yaw);
            var forward = Forward(yaw);
            var relative = origin - (position + new Vector3(0f, spec.Height / 2f, 0f));
            var localOrigin = new Vector3(Vector3.Dot(relative, right), relative.Y, Vector3.Dot(relative, forward));
            var localDirection = new Vector3(Vector3.Dot(unit, right), unit.Y, Vector3.Dot(unit, forward));
            var half = new Vector3(spec.Width / 2f, spec.Height / 2f, spec.Depth / 2f);
            var near = 0f;
            var far = maxDistance;
            var enteredAxis = -1;
            var enteredSign = 0f;
            for (var axis = 0; axis < 3; axis++)
            {
                var o = Component(localOrigin, axis);
                var d = Component(localDirection, axis);
                var h = Component(half, axis);
                if (Math.Abs(d) < 1e-8f)
                {
                    if (Math.Abs(o) > h)
                    {
                        return false;
                    }

                    continue;
                }

                var t1 = (-h - o) / d;
                var t2 = (h - o) / d;
                if (t1 > t2)
                {
                    (t1, t2) = (t2, t1);
                }

                if (t1 > near)
                {
                    near = t1;
                    enteredAxis = axis;
                    enteredSign = d > 0f ? -1f : 1f;
                }

                far = Math.Min(far, t2);
                if (near > far)
                {
                    return false;
                }
            }

            if (enteredAxis < 0)
            {
                return false;
            }

            distance = near;
            normal = (enteredAxis == 0 ? right : enteredAxis == 1 ? Vector3.UnitY : forward) * enteredSign;
            return true;
        }

        private static float DistanceToBox(Vector3 point, Vector3 position, float yaw, DeploySpec spec)
        {
            var right = Right(yaw);
            var forward = Forward(yaw);
            var relative = point - (position + new Vector3(0f, spec.Height / 2f, 0f));
            var outside = new Vector3(
                Math.Max(0f, Math.Abs(Vector3.Dot(relative, right)) - (spec.Width / 2f)),
                Math.Max(0f, Math.Abs(relative.Y) - (spec.Height / 2f)),
                Math.Max(0f, Math.Abs(Vector3.Dot(relative, forward)) - (spec.Depth / 2f)));
            return outside.Length();
        }

        private static Vector3 CenterOf(Vector3 position, DeploySpec spec)
        {
            return position + new Vector3(0f, spec.Height / 2f, 0f);
        }

        private static float Component(Vector3 value, int axis)
        {
            return axis == 0 ? value.X : axis == 1 ? value.Y : value.Z;
        }

        // Unity の向き (Y 軸まわりの度、0 度で +Z を向き、右が +X) にそろえる。
        private static Vector3 Forward(float yawDegrees)
        {
            var radians = yawDegrees * (MathF.PI / 180f);
            return new Vector3(MathF.Sin(radians), 0f, MathF.Cos(radians));
        }

        private static Vector3 Right(float yawDegrees)
        {
            var radians = yawDegrees * (MathF.PI / 180f);
            return new Vector3(MathF.Cos(radians), 0f, -MathF.Sin(radians));
        }

        private static float NormalizeYaw(float degrees)
        {
            var normalized = degrees % 360f;
            return normalized < 0f ? normalized + 360f : normalized;
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
        }
    }
}
