using System;
using System.Collections.Generic;
using Shooter.Runtime;

namespace Shooter.NetV2
{
    internal sealed class NetV2Projectiles : IClientProjectileViews
    {
        private readonly NetV2Weapons _weapons;

        // 弾ごとに届く知らせから引くので、引いた見た目 (無かったことも) を覚えてアセットを読み直さない。
        private readonly Dictionary<string, ProjectileSettings> _looks = new Dictionary<string, ProjectileSettings>(StringComparer.Ordinal);

        public NetV2Projectiles(NetV2Weapons weapons)
        {
            _weapons = weapons ?? throw new ArgumentNullException(nameof(weapons));
        }

        public IClientProjectileView Show(string weaponId, System.Numerics.Vector3 position, System.Numerics.Vector3 velocity)
        {
            var body = ProjectileBody.Show(LookFor(weaponId), weaponId, NetV2Math.ToUnity(position), NetV2Math.ToUnity(velocity));
            return new View(body);
        }

        private ProjectileSettings LookFor(string weaponId)
        {
            if (string.IsNullOrEmpty(weaponId))
            {
                return null;
            }

            if (!_looks.TryGetValue(weaponId, out var look))
            {
                look = Find(weaponId);
                _looks.Add(weaponId, look);
            }

            return look;
        }

        private ProjectileSettings Find(string weaponId)
        {
            var definition = _weapons.DefinitionFor(weaponId);
            return definition != null ? definition.Projectile : null;
        }

        private sealed class View : IClientProjectileView
        {
            private readonly ProjectileBody _body;

            public View(ProjectileBody body)
            {
                _body = body;
            }

            // 刺さった弾は見た目の秒数が過ぎると自分で消えるので、消えた後に届く知らせでは何もしない。
            public void Move(System.Numerics.Vector3 position, System.Numerics.Vector3 velocity)
            {
                if (_body != null)
                {
                    _body.MoveTo(NetV2Math.ToUnity(position), NetV2Math.ToUnity(velocity));
                }
            }

            public void Stick(System.Numerics.Vector3 position)
            {
                if (_body != null)
                {
                    _body.StickAt(NetV2Math.ToUnity(position));
                }
            }

            public void Remove()
            {
                if (_body != null)
                {
                    _body.Remove();
                }
            }
        }
    }
}
