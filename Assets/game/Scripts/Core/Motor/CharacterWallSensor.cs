using System;
using System.Numerics;

namespace Shooter.Core
{
    public readonly struct WallContact
    {
        public WallContact(WallRunSide side, Vector3 normal, float distance)
        {
            Side = side;
            Normal = side == WallRunSide.None ? Vector3.Zero : normal;
            Distance = side == WallRunSide.None ? 0f : distance;
        }

        public static WallContact None => default;

        public WallRunSide Side { get; }

        public Vector3 Normal { get; }

        public float Distance { get; }

        public bool IsFound => Side != WallRunSide.None;
    }

    public sealed class CharacterWallSensor
    {
        public const float ReachBeyondRadius = 0.3f;

        // 垂直に限らず少し傾いた面も壁とするのは、組んだ壁の誤差で走れなくならないため。これより寝た面は坂や天井として取らない。
        public const float MaxNormalY = 0.1f;

        // 法線の一致まで求めないのは、曲がった壁を少しずつ向きを変えて追うため。
        public const float FollowDot = 0.95f;

        private readonly CharacterBody _body;

        public CharacterWallSensor(CharacterBody body)
        {
            _body = body ?? throw new ArgumentNullException(nameof(body));
        }

        public float Reach => _body.Radius + ReachBeyondRadius;

        public WallContact Probe(Vector3 position, float yaw)
        {
            var forward = CharacterMotorSimulation.YawForward(yaw);
            var right = new Vector3(forward.Z, 0f, -forward.X);
            var origin = Origin(position);
            var onRight = Cast(origin, right, WallRunSide.Right);
            var onLeft = Cast(origin, -right, WallRunSide.Left);
            if (!onLeft.IsFound)
            {
                return onRight;
            }

            return onRight.IsFound && onRight.Distance <= onLeft.Distance ? onRight : onLeft;
        }

        public WallContact Follow(Vector3 position, WallRunSide side, Vector3 normal)
        {
            if (side == WallRunSide.None)
            {
                throw new ArgumentOutOfRangeException(nameof(side), side, "side must be Left or Right");
            }

            var flat = Flatten(normal);
            if (flat == Vector3.Zero)
            {
                return WallContact.None;
            }

            var contact = Cast(Origin(position), -flat, side);
            return contact.IsFound && Vector3.Dot(contact.Normal, flat) >= FollowDot ? contact : WallContact.None;
        }

        // 足元や頭の高さでは低い段や張り出しを壁と取るので、体の中心の高さで調べる
        private Vector3 Origin(Vector3 position)
        {
            return new Vector3(position.X, position.Y + _body.Current.CenterY, position.Z);
        }

        private WallContact Cast(Vector3 origin, Vector3 direction, WallRunSide side)
        {
            if (!_body.World.Raycast(origin, direction, Reach, _body.LayerMask, out var hit) || Math.Abs(hit.Normal.Y) > MaxNormalY)
            {
                return WallContact.None;
            }

            var normal = Flatten(hit.Normal);
            return normal == Vector3.Zero ? WallContact.None : new WallContact(side, normal, hit.Distance);
        }

        private static Vector3 Flatten(Vector3 normal)
        {
            var length = MathF.Sqrt((normal.X * normal.X) + (normal.Z * normal.Z));
            return length > 0f ? new Vector3(normal.X / length, 0f, normal.Z / length) : Vector3.Zero;
        }
    }
}
