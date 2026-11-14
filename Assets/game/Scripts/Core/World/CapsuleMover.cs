using System;
using System.Numerics;

namespace Fps.Core
{
    /// <summary>
    /// Unity の CharacterController と同じ規則で、<see cref="IWorldCollision"/> の上の縦向きのカプセルを動かす。
    /// 1 回の <see cref="Move"/> は、段を上がる持ち上げ・横の滑り・下ろしの 3 つに分けて掃引する。
    /// 同じ状態・同じ変位・同じ世界からは同じ結果のビットになる (クライアントの予測とサーバーで同じ計算をするため)。
    /// </summary>
    public sealed class CapsuleMover
    {
        private const int _maxSlideIterations = 4;
        private const float _minMoveLength = 1e-5f;

        // 面にほぼ平行に当たったとき、面との隙間を skin に保とうとして戻る距離が際限なく伸びないようにする。
        private const float _minApproachCos = 0.25f;

        private static readonly Vector3 _up = new Vector3(0f, 1f, 0f);
        private static readonly Vector3 _down = new Vector3(0f, -1f, 0f);

        private readonly IWorldCollision _world;
        private readonly CapsuleMoverSpec _spec;
        private readonly int[] _overlap = new int[1];

        /// <param name="world">当たりを問い合わせる世界。</param>
        /// <param name="spec">坂・段差・skin の設定。</param>
        /// <param name="radius">カプセルの半径。正の有限値。</param>
        /// <param name="height">カプセルの高さ。半径の 2 倍より低ければ球になる。</param>
        /// <param name="center"><see cref="Position"/> からカプセルの中心へのずれ (ワールドの向き)。</param>
        public CapsuleMover(IWorldCollision world, CapsuleMoverSpec spec, float radius, float height, Vector3 center)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _spec = spec ?? throw new ArgumentNullException(nameof(spec));
            if (!(radius > 0f) || float.IsInfinity(radius))
            {
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "radius must be finite and positive");
            }

            Radius = radius;
            SetShape(height, center);
        }

        public float Radius { get; }

        public float Height { get; private set; }

        public Vector3 Center { get; private set; }

        public Vector3 Position { get; private set; }

        /// <summary>最後の <see cref="Move"/> で歩ける面に立ったか (<see cref="CapsuleContacts.Below"/>)。</summary>
        public bool IsGrounded => (Contacts & CapsuleContacts.Below) != 0;

        public CapsuleContacts Contacts { get; private set; }

        /// <summary>
        /// 当たりを調べずに置く。再シミュレーションで前の tick の状態に戻すときにも使う。
        /// </summary>
        public void Place(Vector3 position, CapsuleContacts contacts)
        {
            RequireFinite(position, nameof(position));
            Position = position;
            Contacts = contacts;
        }

        /// <summary>
        /// 当たりを調べずに高さと中心を変える。立ち上がるなど広がるときは、先に <see cref="HasHeadroom"/> で確かめる。
        /// </summary>
        public void SetShape(float height, Vector3 center)
        {
            if (!(height >= 0f) || float.IsInfinity(height))
            {
                throw new ArgumentOutOfRangeException(nameof(height), height, "height must be finite and not negative");
            }

            RequireFinite(center, nameof(center));
            Height = height;
            Center = center;
        }

        /// <summary>今の位置で、この高さと中心のカプセルが何にも重ならないか。</summary>
        public bool HasHeadroom(float height, Vector3 center)
        {
            return IsClear(Position, height, center);
        }

        /// <summary>
        /// position に置いたこの高さと中心のカプセルが何にも重ならないか。
        /// 床や壁に skin の隙間で触れているだけで塞がっていると見なさないよう、半径を skin (半径の半分まで) だけ細くして調べる。
        /// </summary>
        public bool IsClear(Vector3 position, float height, Vector3 center)
        {
            if (!(height >= 0f) || float.IsInfinity(height))
            {
                throw new ArgumentOutOfRangeException(nameof(height), height, "height must be finite and not negative");
            }

            RequireFinite(position, nameof(position));
            RequireFinite(center, nameof(center));
            var margin = Math.Min(_spec.SkinWidth, Radius * 0.5f);
            SphereCenters(position, height, center, out var bottom, out var top);
            return _world.OverlapCapsule(bottom, top, Radius - margin, _spec.LayerMask, _overlap) == 0;
        }

        /// <summary>
        /// displacement だけ動かす。壁や歩けない坂には沿って滑り、<see cref="CapsuleMoverSpec.StepOffset"/> までの段は上がる。
        /// 動かす前から重なっているものは通り抜ける (<see cref="IWorldCollision.CapsuleCast"/> と同じ)。
        /// </summary>
        public CapsuleContacts Move(Vector3 displacement)
        {
            RequireFinite(displacement, nameof(displacement));
            var horizontal = new Vector3(displacement.X, 0f, displacement.Z);
            var vertical = displacement.Y;

            // 跳んでいる間に段の高さだけ持ち上げると、縁に手をかけたように上がってしまう。
            var step = (displacement.X != 0f || displacement.Z != 0f) && vertical <= 0f ? _spec.StepOffset : 0f;
            var flags = TryMove(Position, horizontal, vertical, step, out var end, out var landedOnSteep);

            // 持ち上げると、坂の途中に乗ったり、低い天井の縁に頭をぶつけて進めなかったりする。そのときは持ち上げずに動かした方を取る。
            if (step > 0f && (landedOnSteep || (flags & CapsuleContacts.Sides) != 0))
            {
                var flatFlags = TryMove(Position, horizontal, vertical, 0f, out var flatEnd, out _);
                if (landedOnSteep || HorizontalDistanceSquared(Position, flatEnd) >= HorizontalDistanceSquared(Position, end))
                {
                    flags = flatFlags;
                    end = flatEnd;
                }
            }

            Position = end;
            Contacts = flags;
            return flags;
        }

        private CapsuleContacts TryMove(
            Vector3 start, Vector3 horizontal, float vertical, float step, out Vector3 end, out bool landedOnSteep)
        {
            var flags = CapsuleContacts.None;
            var position = start;
            var lifted = 0f;
            var up = step + Math.Max(vertical, 0f);
            if (up > 0f)
            {
                var moved = SweepStraight(ref position, _up, up, out var hitCeiling);
                if (hitCeiling && vertical > 0f)
                {
                    flags |= CapsuleContacts.Above;
                }

                if (step > 0f)
                {
                    lifted = moved;
                }
            }

            SphereCenters(start, Height, Center, out var startBottom, out _);
            var highestFoothold = startBottom.Y - Radius + _spec.StepOffset;
            position = SlideSideways(position, horizontal, highestFoothold, ref flags);

            landedOnSteep = false;
            var down = lifted + Math.Max(-vertical, 0f);
            if (down > 0f)
            {
                position = SlideDown(position, down, step > 0f, highestFoothold, ref flags, out landedOnSteep);
            }

            end = position;
            return flags;
        }

        private float SweepStraight(ref Vector3 position, Vector3 direction, float distance, out bool hit)
        {
            hit = Cast(position, direction, distance + _spec.SkinWidth, out var worldHit);
            if (!hit)
            {
                position += direction * distance;
                return distance;
            }

            var travel = Travel(worldHit, direction, distance);
            position += direction * travel;
            return travel;
        }

        private Vector3 SlideSideways(Vector3 position, Vector3 motion, float highestFoothold, ref CapsuleContacts flags)
        {
            var remaining = motion;
            var previousNormal = Vector3.Zero;
            var hasPrevious = false;
            for (var i = 0; i < _maxSlideIterations; i++)
            {
                var length = Length(remaining);
                if (length <= _minMoveLength)
                {
                    break;
                }

                var direction = Scale(remaining, 1f / length);
                if (!Cast(position, direction, length + _spec.SkinWidth, out var hit))
                {
                    position += remaining;
                    break;
                }

                var travel = Travel(hit, direction, length);
                position += direction * travel;
                var normal = hit.Normal;
                // 丸い底は段の角を坂として登れてしまうので、足元から段の高さより上に触れたものは壁として扱う。
                if (!IsWalkableNormal(normal) || hit.Point.Y > highestFoothold)
                {
                    flags |= CapsuleContacts.Sides;

                    // 歩けない坂の法線のまま滑らせると坂を登ってしまうので、壁として水平に倒す。
                    normal = FlattenOrKeep(normal);
                }

                var rest = direction * (length - travel);
                if (!TrySlide(rest, normal, motion, hasPrevious, previousNormal, out remaining))
                {
                    break;
                }

                previousNormal = normal;
                hasPrevious = true;
            }

            return position;
        }

        private Vector3 SlideDown(
            Vector3 position,
            float distance,
            bool validateStep,
            float highestFoothold,
            ref CapsuleContacts flags,
            out bool landedOnSteep)
        {
            landedOnSteep = false;
            var motion = _down * distance;
            var remaining = motion;
            var previousNormal = Vector3.Zero;
            var hasPrevious = false;
            for (var i = 0; i < _maxSlideIterations; i++)
            {
                var length = Length(remaining);
                if (length <= _minMoveLength)
                {
                    break;
                }

                var direction = Scale(remaining, 1f / length);
                if (!Cast(position, direction, length + _spec.SkinWidth, out var hit))
                {
                    position += remaining;
                    break;
                }

                var travel = Travel(hit, direction, length);
                position += direction * travel;

                // 持ち上げた後に下ろして段の角に乗ると、丸い底のぶん段の高さより高い所に立ててしまう。
                if (!hasPrevious && validateStep && hit.Point.Y > highestFoothold)
                {
                    landedOnSteep = true;
                    break;
                }

                if (IsWalkableContact(position, hit))
                {
                    flags |= CapsuleContacts.Below;
                    break;
                }

                if (!hasPrevious && validateStep)
                {
                    landedOnSteep = true;
                    break;
                }

                flags |= CapsuleContacts.Sides;
                var rest = direction * (length - travel);
                if (!TrySlide(rest, hit.Normal, motion, hasPrevious, previousNormal, out remaining))
                {
                    break;
                }

                previousNormal = hit.Normal;
                hasPrevious = true;
            }

            return position;
        }

        private static bool TrySlide(
            Vector3 rest, Vector3 normal, Vector3 motion, bool hasPrevious, Vector3 previousNormal, out Vector3 slid)
        {
            slid = rest - (normal * Dot(rest, normal));

            // 2 枚の面の角では、片方に沿わせるともう片方へ押し込むので、2 枚の交わる線に沿わせる。
            if (hasPrevious && Dot(slid, previousNormal) < 0f)
            {
                var crease = Cross(previousNormal, normal);
                var creaseLength = Length(crease);
                if (creaseLength <= _minMoveLength)
                {
                    return false;
                }

                crease = Scale(crease, 1f / creaseLength);
                slid = crease * Dot(rest, crease);
            }

            // 来た向きへ押し戻す動きは、角で行き来して震えるもとになるので捨てる。
            return Dot(slid, motion) > 0f;
        }

        private bool Cast(Vector3 position, Vector3 direction, float distance, out WorldHit hit)
        {
            SphereCenters(position, Height, Center, out var bottom, out var top);
            return _world.CapsuleCast(bottom, top, Radius, direction, distance, _spec.LayerMask, out hit);
        }

        private float Travel(WorldHit hit, Vector3 direction, float distance)
        {
            // 当たる所まで進むと面に接し、次の掃引で「動かす前から重なっている」として通り抜けかねないので、面から skin だけ離して止める。
            var approach = Math.Max(-Dot(direction, hit.Normal), _minApproachCos);
            var travel = hit.Distance - (_spec.SkinWidth / approach);
            return Math.Min(Math.Max(travel, 0f), distance);
        }

        private bool IsWalkableNormal(Vector3 normal)
        {
            return normal.Y >= _spec.MinWalkableNormalY;
        }

        private bool IsWalkableContact(Vector3 position, WorldHit hit)
        {
            var normal = hit.Normal;
            if (IsWalkableNormal(normal))
            {
                return true;
            }

            if (normal.Y <= 0f)
            {
                return false;
            }

            // 段の角に乗ると、当たりの法線は角を丸めた斜めの向きになり坂と見分けられない。
            // 当たった点から少し面の奥へ寄った所を真下へ調べ、そこにある面の法線で歩けるかを決める。
            var flatLength = MathF.Sqrt((normal.X * normal.X) + (normal.Z * normal.Z));
            if (flatLength <= _minMoveLength)
            {
                return false;
            }

            var probe = Math.Min(_spec.SkinWidth, Radius * 0.5f);
            SphereCenters(position, Height, Center, out var bottom, out _);
            var origin = new Vector3(
                hit.Point.X - (normal.X / flatLength * probe),
                bottom.Y,
                hit.Point.Z - (normal.Z / flatLength * probe));
            var rayLength = bottom.Y - hit.Point.Y + _spec.SkinWidth;
            return _world.Raycast(origin, _down, rayLength, _spec.LayerMask, out var face) && IsWalkableNormal(face.Normal);
        }

        private void SphereCenters(Vector3 position, float height, Vector3 center, out Vector3 bottom, out Vector3 top)
        {
            var half = Math.Max((height * 0.5f) - Radius, 0f);
            var middle = position + center;
            bottom = new Vector3(middle.X, middle.Y - half, middle.Z);
            top = new Vector3(middle.X, middle.Y + half, middle.Z);
        }

        private static Vector3 FlattenOrKeep(Vector3 normal)
        {
            var flatLength = MathF.Sqrt((normal.X * normal.X) + (normal.Z * normal.Z));
            if (flatLength <= _minMoveLength)
            {
                return normal;
            }

            return new Vector3(normal.X / flatLength, 0f, normal.Z / flatLength);
        }

        // Vector3.Dot・Length・Cross は実行環境によって SIMD で足す順番が変わり、クライアントとサーバーで結果のビットがずれうるので、順番を書き下す。
        private static float Dot(Vector3 a, Vector3 b)
        {
            return (a.X * b.X) + (a.Y * b.Y) + (a.Z * b.Z);
        }

        private static float Length(Vector3 v)
        {
            return MathF.Sqrt(Dot(v, v));
        }

        private static Vector3 Cross(Vector3 a, Vector3 b)
        {
            return new Vector3((a.Y * b.Z) - (a.Z * b.Y), (a.Z * b.X) - (a.X * b.Z), (a.X * b.Y) - (a.Y * b.X));
        }

        private static float HorizontalDistanceSquared(Vector3 from, Vector3 to)
        {
            var x = to.X - from.X;
            var z = to.Z - from.Z;
            return (x * x) + (z * z);
        }

        private static Vector3 Scale(Vector3 v, float factor)
        {
            return new Vector3(v.X * factor, v.Y * factor, v.Z * factor);
        }

        private static void RequireFinite(Vector3 value, string name)
        {
            if (!IsFinite(value.X) || !IsFinite(value.Y) || !IsFinite(value.Z))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be finite");
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
