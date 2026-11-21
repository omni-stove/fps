using System;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // bots.json の mountedGun を書いた Bot を、オフラインの EnemyMountedGunner と同じく空いた固定銃座へ乗りに行かせ、座席の武器で撃たせる。
    // 乗り降りは乗り物のモジュールの人と同じ口を通すので、座っている Bot はスナップショットの座席 (PlayerSnapshot.Vehicle) で人と同じに見える
    internal sealed partial class ServerBots
    {
        // 乗っている固定銃座を扱ったら true を返し、この tick の判断・歩き・ふつうの射撃をさせない
        private bool StepMountedGun(ServerBotAgent agent, ServerBody body, in EnemyPerception perception, bool thinking, long tick, float dt)
        {
            if (_vehicles == null || !agent.MansGuns)
            {
                return false;
            }

            var playerId = agent.PlayerId;
            if (!_vehicles.TryGetMannedGun(playerId, out var gun))
            {
                if (agent.MannedGun >= 0)
                {
                    // 倒れた・銃座が壊れた・置き直されたなど、自分で降りたのでなくてもすぐ戻らせない (オフラインと同じ)
                    _log.WriteLine(ServerBotLog.GunLost(playerId, GunName(agent.MannedGun)));
                    agent.NoteLeftGun();
                }

                return false;
            }

            if (agent.MannedGun != gun.Vehicle)
            {
                agent.StartManning(gun.Vehicle, gun.Spec);
            }

            if (!thinking)
            {
                return true;
            }

            var hasTarget = perception.Target.HasValue;
            Vector3? localTarget = null;
            if (hasTarget)
            {
                var toTarget = perception.Target.Value - gun.Eye;
                if (_vehicles.AllowsVehicleWeapon(gun.Vehicle, gun.Weapon, toTarget))
                {
                    localTarget = gun.ToLocal(toTarget);
                }
            }

            var order = agent.Man(dt, hasTarget, perception.HasLineOfSight, localTarget);
            var direction = Vector3.Normalize(gun.ToWorld(agent.GunLocalDirection));
            var frameTick = (int)Math.Min(tick, int.MaxValue);

            // 座っている間の入力は向きだけを使う (ServerMovement が座席の人を歩かせない)。体を銃の向きへ回し、ほかの人に銃手の向きを見せる
            _movement.ApplyBotInput(playerId, new InputFrame(frameTick, Vector2.Zero, MathF.Atan2(direction.X, direction.Z), InputButtons.None));
            ShootMountedGun(agent, body.EyePosition, direction, order.Fire, frameTick, dt);

            if (order.Leave.HasValue && agent.CanTryLeavingGun)
            {
                var name = GunName(gun.Vehicle);
                if (_vehicles.BotLeave(playerId))
                {
                    _log.WriteLine(ServerBotLog.GunLeft(playerId, name, order.Leave.Value));
                    agent.NoteLeftGun();
                }
                else
                {
                    agent.DelayGunLeave();
                }
            }

            return true;
        }

        // 判断 (Brain.Tick) が乗りに行くか決める前に、見えている相手へ向けられる空いた銃座を候補として渡す
        private void OfferMountedGun(ServerBotAgent agent, Vector3 feet, in EnemyPerception perception)
        {
            if (_vehicles == null || !agent.MansGuns)
            {
                return;
            }

            var target = perception.Target;
            if (target.HasValue && _vehicles.TryFindMountedGun(feet, target.Value, out var vehicle, out var boardPoint))
            {
                agent.SetGunCandidate(vehicle, boardPoint);
                return;
            }

            agent.SetGunCandidate(-1, null);
        }

        // 歩いた後に、乗る距離に着いた候補の銃座へ乗る。乗ったら true
        private bool TryBoardMountedGun(ServerBotAgent agent, Vector3 feet)
        {
            if (_vehicles == null || !agent.MansGuns)
            {
                return false;
            }

            var playerId = agent.PlayerId;
            var vehicle = agent.GunCandidate;
            if (agent.NoteSeekingGun())
            {
                var board = _vehicles.BoardPointOf(vehicle);
                _log.WriteLine(ServerBotLog.GunSeek(playerId, GunName(vehicle), new Vector2(board.X - feet.X, board.Z - feet.Z).Length()));
            }

            if (!agent.IsSeekingGun || !_vehicles.CanBoardFrom(vehicle, feet) || !_vehicles.BotBoard(playerId, vehicle)
                || !_vehicles.TryGetMannedGun(playerId, out var gun))
            {
                return false;
            }

            agent.StartManning(gun.Vehicle, gun.Spec);
            _log.WriteLine(ServerBotLog.GunManned(playerId, GunName(gun.Vehicle)));
            return true;
        }

        // 熱で撃てない間は引き金を引かせない。人の射撃と同じ検査を通るので、引くと断られた警告と違反の数が増える
        private void ShootMountedGun(ServerBotAgent agent, Vector3 eye, Vector3 direction, bool fire, int tick, float dt)
        {
            var playerId = agent.PlayerId;
            if (!_combat.TryGetBotWeapon(playerId, out var spec, out var state))
            {
                return;
            }

            var trigger = agent.TriggerFor(spec);
            trigger.Tick(dt);
            if (state.IsReloading || state.IsSwitching || state.IsOverheated)
            {
                return;
            }

            if (state.AmmoInMagazine <= 0)
            {
                _combat.BotReload(playerId);
                return;
            }

            if (!fire || !_rules.CanFire(playerId) || !trigger.Pull(true, true))
            {
                return;
            }

            if (_combat.BotFire(playerId, eye, direction, tick))
            {
                agent.Brain.RecordShot();
            }
        }

        private string GunName(int vehicle)
        {
            return vehicle >= 0 && vehicle < _vehicles.Count ? _vehicles.NameOf(vehicle) : "#" + vehicle.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
