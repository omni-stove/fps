using System;
using System.Numerics;

namespace Fps.Core
{
    /// <summary>
    /// 他のプレイヤーは世界に入れない。予測するクライアントには他人の確定位置が無く、当てるとサーバーと毎回食い違うため。
    /// </summary>
    public sealed class CharacterBody
    {
        private readonly CapsuleMover _mover;

        public CharacterBody(IWorldCollision world, CapsuleMoverSpec moverSpec, StanceGeometry geometry)
        {
            World = world ?? throw new ArgumentNullException(nameof(world));
            MoverSpec = moverSpec ?? throw new ArgumentNullException(nameof(moverSpec));
            Geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
            var standing = geometry.Standing;
            _mover = new CapsuleMover(world, moverSpec, geometry.Radius, standing.Height, new Vector3(0f, standing.CenterY, 0f));
        }

        public IWorldCollision World { get; }

        public CapsuleMoverSpec MoverSpec { get; }

        public StanceGeometry Geometry { get; }

        public int LayerMask => MoverSpec.LayerMask;

        public float Radius => Geometry.Radius;

        public Vector3 Position => _mover.Position;

        public bool IsGrounded => _mover.IsGrounded;

        public bool IsCrouched { get; private set; }

        public StanceShape Current => Geometry.Shape(IsCrouched);

        public float FeetY(Vector3 position)
        {
            return position.Y + Current.BottomY;
        }

        public Vector3 EyePosition(Vector3 position)
        {
            return new Vector3(position.X, position.Y + Current.EyeY, position.Z);
        }

        public void ApplyStance(bool crouched)
        {
            var shape = Geometry.Shape(crouched);
            _mover.SetShape(shape.Height, new Vector3(0f, shape.CenterY, 0f));
            IsCrouched = crouched;
        }

        public void Place(Vector3 position, bool grounded)
        {
            _mover.Place(position, grounded ? CapsuleContacts.Below : CapsuleContacts.None);
        }

        public CapsuleContacts Move(Vector3 displacement)
        {
            return _mover.Move(displacement);
        }

        public bool HasHeadroomToStand()
        {
            return IsStandingClear(Position);
        }

        public bool IsStandingClear(Vector3 position)
        {
            var standing = Geometry.Standing;
            return _mover.IsClear(position, standing.Height, new Vector3(0f, standing.CenterY, 0f));
        }
    }
}
