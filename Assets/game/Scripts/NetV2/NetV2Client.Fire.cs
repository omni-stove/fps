using System;
using System.Globalization;
using Shooter.Core;
using Shooter.Protocol;
using Shooter.Runtime;
using UnityEngine;

namespace Shooter.NetV2
{
    public sealed partial class NetV2Client
    {
        private void SendRequests()
        {
            if (_local == null)
            {
                return;
            }

            var direction = NetV2Math.ToNumerics(_local.AimDirection);
            Trigger(direction);

            var state = _match.LocalState;
            var feet = NetV2Math.ToUnity(state.Position) + (Vector3.up * _client.Motor.Stance.Shape(state.Stance).BottomY);
            _vehicles.SendRequests(_local, _client, feet);

            if (_local.TakeReload())
            {
                _weaponAction.Action = WeaponAction.Reload;
                _client.Send(_weaponAction, TransportChannel.Reliable);
            }

            if (_local.TakeSwitchAmmoType())
            {
                _weaponAction.Action = WeaponAction.SwitchAmmoType;
                _client.Send(_weaponAction, TransportChannel.Reliable);
            }

            var pouch = OwnGrenades();
            if (_local.TakeSwitchGrenade() && _grenadeChoice.SelectNext(pouch))
            {
                var typeId = _grenadeChoice.Current(pouch);
                Debug.Log(GrenadeThrower.LogTag + " net selected type=" + typeId + " remaining=" + pouch.RemainingOf(typeId).ToString(CultureInfo.InvariantCulture), this);
                _hud?.ShowGrenades(pouch, typeId);
            }

            if (_local.TakeGrenade())
            {
                _grenade.Direction = direction;
                _grenade.GrenadeTypeId = _grenadeChoice.Current(pouch);
                _client.Send(_grenade, TransportChannel.Reliable);
            }

            // 付けられないと分かっている印は送らない (Netcode の NetTeamDeathmatch.CanRequestPing と同じく、ルールで付けられて生きているときだけ)
            if (_local.TakePing() && CanPing())
            {
                Debug.Log(PingLog.Tag + " net request", this);
                _ping.Direction = direction;
                _client.Send(_ping, TransportChannel.Reliable);
            }
        }

        private GrenadePouchSync OwnGrenades()
        {
            var latest = _client.LatestSnapshot;
            return latest != null && latest.HasOwner ? latest.Owner.Grenades : null;
        }

        private bool CanPing()
        {
            var latest = _client.LatestSnapshot;
            if (latest == null || latest.Match.Rules.Ping != Shooter.Core.Ping.On)
            {
                return false;
            }

            var own = latest.Players.IndexOf(_client.PlayerId);
            return own >= 0 && latest.Players[own].Health > 0;
        }

        // 引き金は今の武器の撃ち方 (ITriggerModel) で、押している間の連射の間合いと、押しっぱなしで出る撃ち方の押した・離したを決める
        private void Trigger(System.Numerics.Vector3 direction)
        {
            var held = _local.IsAttackHeld;
            var pressed = _local.TakeAttack();
            var latest = _client.LatestSnapshot;
            var owner = latest != null && latest.HasOwner ? latest.Owner : null;
            var weaponId = ClientFire.CurrentWeaponId(owner);
            var trigger = _weapons.TriggerFor(weaponId);
            if (_heldContinuous != null && _heldContinuous != trigger)
            {
                // 押したまま持ち替えた・武器を失ったときも、押しっぱなしの撃ち方を止めさせる
                _heldContinuous.Drop();
                SendContinuous(direction, _heldContinuousWeaponId, _heldContinuous.Spec, default, TriggerEdge.Released, owner);
            }

            _local.BlocksAds = trigger != null && trigger.BlocksAds;
            if (trigger == null)
            {
                _hud?.ShowCharge(null);
                if (pressed)
                {
                    Debug.LogWarning(LogTag + " cannot fire " + (_weapons.TriggerError ?? "without a weapon"), this);
                }

                return;
            }

            trigger.Tick(Time.deltaTime);
            var pull = trigger.Pull(held, pressed);
            _hud?.ShowCharge(trigger.Model.ChargeFraction);
            if (trigger.IsContinuous)
            {
                PullContinuous(trigger, pull, direction, weaponId, latest, owner);
                return;
            }

            if (pull.Shots <= 0)
            {
                return;
            }

            switch (ClientFire.Decide(latest, _client.PlayerId, _match.LocalState, trigger.Spec))
            {
                case FireDecision.Fire:
                    for (var i = 0; i < pull.Shots; i++)
                    {
                        SendFire(direction, weaponId, trigger.Spec, pull, TriggerEdge.None, owner);
                    }

                    _local.ReleaseSprint(SprintInput.ReasonFire);
                    break;
                case FireDecision.Reload:
                    // 押し続けている間は鳴らさず頼み直さない (オフラインの WeaponController と同じ)
                    if (pressed)
                    {
                        _dryFire?.Play(weaponId);
                        RequestReloadIfAuto(latest);
                    }

                    break;
            }
        }

        // 撃てない間に押した分は押したと送らないが、押したと送った後は撃てなくなっても必ず離したと送る (サーバーが照射を止められるように)
        private void PullContinuous(
            ClientTrigger trigger, in TriggerPull pull, System.Numerics.Vector3 direction, string weaponId, SnapshotMessage latest, OwnerSnapshot owner)
        {
            var sending = _heldContinuous == trigger;

            // 離してすぐ押し直したフレームは、前の照射を止めてから次を始める
            if (sending && pull.Released)
            {
                SendContinuous(direction, weaponId, trigger.Spec, pull, TriggerEdge.Released, owner);
                sending = false;
            }

            if (pull.Pressed && !sending)
            {
                switch (ClientFire.Decide(latest, _client.PlayerId, _match.LocalState, trigger.Spec))
                {
                    case FireDecision.Fire:
                        SendContinuous(direction, weaponId, trigger.Spec, pull, TriggerEdge.Pressed, owner);
                        _heldContinuous = trigger;
                        _heldContinuousWeaponId = weaponId;
                        sending = true;
                        _local.ReleaseSprint(SprintInput.ReasonFire);
                        break;
                    case FireDecision.Reload:
                        _dryFire?.Play(weaponId);
                        RequestReloadIfAuto(latest);
                        break;
                }
            }

            if (sending && !trigger.IsHeld)
            {
                SendContinuous(direction, weaponId, trigger.Spec, pull, TriggerEdge.Released, owner);
            }
        }

        private void RequestReloadIfAuto(SnapshotMessage latest)
        {
            if (latest != null && latest.Match.Rules.ReloadMode == ReloadMode.Auto)
            {
                _weaponAction.Action = WeaponAction.Reload;
                _client.Send(_weaponAction, TransportChannel.Reliable);
            }
        }

        private void SendContinuous(
            System.Numerics.Vector3 direction, string weaponId, WeaponSpec spec, in TriggerPull pull, TriggerEdge edge, OwnerSnapshot owner)
        {
            if (edge == TriggerEdge.Released)
            {
                _heldContinuous = null;
                _heldContinuousWeaponId = null;
            }

            SendFire(direction, weaponId, spec, pull, edge, owner);
        }

        // 撃てるか (弾・間隔) と当たりはサーバーが決める。表示している tick を載せ、サーバーがその時点へ巻き戻して当てる
        private void SendFire(
            System.Numerics.Vector3 direction, string weaponId, WeaponSpec spec, in TriggerPull pull, TriggerEdge edge, OwnerSnapshot owner)
        {
            var renderTick = Math.Max(0f, _match.Interpolation.RenderTick);
            ClientFire.Fill(_fire, owner, weaponId, pull, edge, _lockOn != null ? _lockOn.LockedTargetId : null);
            _fire.Origin = NetV2Math.ToNumerics(_local.EyePosition);
            _fire.Direction = direction;
            _fire.ClientTick = (int)renderTick;
            _fire.InterpolationAlpha = renderTick - (int)renderTick;
            _fire.ShotGeneration = _shotGeneration;
            _fire.ShotIndex = _shotIndex;
            _shotIndex = unchecked(_shotIndex + 1);
            _client.Send(_fire, TransportChannel.Reliable);
            _weaponHooks.LocalFired(_fire, spec);

            // 押しっぱなしの撃ち方の見た目は受け口が出すので、1 発ごとの射撃の演出は出さない
            if (edge == TriggerEdge.None)
            {
                CombatEvents.RaiseShotFired(_local.gameObject, _local.EyePosition, _local.AimDirection, _fire.WeaponId);
            }
        }

        private bool SendReliable(IMessage message)
        {
            if (_client == null || _client.State != MatchClientState.Connected)
            {
                return false;
            }

            _client.Send(message, TransportChannel.Reliable);
            return true;
        }
    }
}
