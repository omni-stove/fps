using System;
using Fps.Core;
using Fps.Protocol;

namespace Fps.NetV2
{
    // 武器の機能ごとの受け口を 1 か所で振り分けるのは、機能の issue が NetV2Client を触らずに受け口だけを埋められるようにするため。
    public sealed class ClientWeaponHooks
    {
        private readonly Func<string, WeaponSpec> _findWeapon;

        public ClientWeaponHooks(Func<string, WeaponSpec> findWeapon)
        {
            _findWeapon = findWeapon ?? throw new ArgumentNullException(nameof(findWeapon));
        }

        public ClientProjectiles Projectiles { get; } = new ClientProjectiles();

        public ClientBeams Beams { get; } = new ClientBeams();

        public ClientSpray Spray { get; } = new ClientSpray();

        public ClientStatusEffects StatusEffects { get; } = new ClientStatusEffects();

        public ClientAreaEffects AreaEffects { get; } = new ClientAreaEffects();

        public bool Handle(IMessage message)
        {
            switch (message)
            {
                case ProjectileSpawnedMessage spawned:
                    Projectiles.Spawned(spawned);
                    return true;
                case ProjectileEndedMessage ended:
                    Projectiles.Ended(ended);
                    return true;
                case BeamStateMessage beam:
                    // ビームと火炎は同じ照射の知らせで届くので、武器の届け方で分ける。知らない武器はビームとして出す。
                    if (_findWeapon(beam.WeaponId)?.Round.Delivery == RoundDelivery.Spray)
                    {
                        Spray.Receive(beam);
                    }
                    else
                    {
                        Beams.Receive(beam);
                    }

                    return true;
                case StatusEffectMessage status:
                    StatusEffects.Receive(status);
                    return true;
                case AreaEffectMessage area:
                    AreaEffects.Receive(area);
                    return true;
                default:
                    return false;
            }
        }

        public void Apply(SnapshotMessage snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            Projectiles.Apply(snapshot);
        }

        // 撃った本人はサーバーの知らせを待たずに見た目を出すので、送った要求をそのまま渡す。
        public void LocalFired(FireRequestMessage request, WeaponSpec spec)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            switch (spec.Round.Delivery)
            {
                case RoundDelivery.Projectile:
                    Projectiles.LocalFired(request, spec);
                    break;
                case RoundDelivery.Beam:
                    Beams.LocalTrigger(request, spec);
                    break;
                case RoundDelivery.Spray:
                    Spray.LocalTrigger(request, spec);
                    break;
            }
        }
    }
}
