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
        private void HandleSnapshot(SnapshotMessage snapshot)
        {
            if (_match == null)
            {
                return;
            }

            if (_mapChange.TryTakeChange(snapshot.Match.State, out var nextMap))
            {
                ChangeMap(nextMap);
                return;
            }

            _hud?.Apply(snapshot);
            var grenades = snapshot.HasOwner ? snapshot.Owner.Grenades : null;
            _hud?.ShowGrenades(grenades, _grenadeChoice.Current(grenades));
            _weaponHooks.Apply(snapshot);
            ForgetGrenades(snapshot);
            _loadout.Apply(snapshot, _client.PlayerId);
            ShowCosmetics(snapshot);
            ShowControlPoints(snapshot);
            PlayReloadSound(snapshot);
            foreach (var part in _parts)
            {
                part.Apply(snapshot);
            }
        }

        // リロードを始めた (サーバーの自動リロードを含む) ときにリロード音を鳴らす (Netcode の NetPlayer と同じ)
        private void PlayReloadSound(SnapshotMessage snapshot)
        {
            if (!snapshot.HasOwner)
            {
                return;
            }

            var reloading = snapshot.Owner.Weapon.IsReloading;
            var weaponId = ClientFire.CurrentWeaponId(snapshot.Owner);
            if (reloading && !_wasReloading && weaponId != null && _presentation != null && _presentation.isActiveAndEnabled)
            {
                _presentation.PlayReload(weaponId);
            }

            _wasReloading = reloading;
        }

        private void HandleLadderGrabbed(int ladder, float yaw)
        {
            Debug.Log("[Ladder] grab index=" + ladder.ToString(CultureInfo.InvariantCulture)
                + " yaw=" + (yaw * Mathf.Rad2Deg).ToString("F1", CultureInfo.InvariantCulture), this);
            _local?.FaceYaw(yaw);
        }

        private void HandleReconciled(ReconcileResult result)
        {
            if (result.Outcome == ReconcileOutcome.Corrected)
            {
                Debug.Log(LogTag + " reconciled tick=" + result.Tick.ToString(CultureInfo.InvariantCulture)
                    + " error=" + result.Error.ToString("F3", CultureInfo.InvariantCulture)
                    + " resimulated=" + result.ResimulatedTicks.ToString(CultureInfo.InvariantCulture), this);
            }
        }

        private void HandleMessage(IMessage message)
        {
            switch (message)
            {
                case AntiCheatToClientMessage antiCheat:
                    _antiCheat.HandleMessage(antiCheat, Time.realtimeSinceStartupAsDouble);
                    break;
                case KilledMessage killed:
                    var entry = ClientKillFeed.FromMessage(killed);
                    Debug.Log(CombatLogTag + " Killed " + entry, this);
                    if (killed.VictimId == _client.PlayerId && _local != null)
                    {
                        _local.ReleaseHolds(CrouchInput.ReasonDeath);
                    }

                    CombatEvents.RaiseKilled(entry);

                    // キルカムは続けて届くクリップに倒した武器を添えるので、キルフィードと別に部品にも渡す
                    HandleByParts(killed);
                    break;
                case DamageTakenMessage damage:
                    Vector3? source = damage.HasSource ? NetV2Math.ToUnity(damage.SourcePosition) : (Vector3?)null;
                    Debug.Log(CombatLogTag + " DamageTaken amount=" + damage.Amount.ToString(CultureInfo.InvariantCulture)
                        + " source=" + (source.HasValue ? source.Value.ToString("F2", CultureInfo.InvariantCulture) : "none"), this);
                    CombatEvents.RaiseDamageTaken(source, Math.Max(0, damage.Amount));
                    break;
                case HitConfirmMessage hit:
                    var confirm = new HitConfirm(Math.Max(0, hit.Damage), hit.Zone, hit.Killed, hit.Downed);
                    Debug.Log(CombatLogTag + " HitConfirmed damage=" + confirm.Damage.ToString(CultureInfo.InvariantCulture)
                        + " zone=" + confirm.Zone + " killed=" + confirm.Killed + " downed=" + confirm.Downed, this);
                    CombatEvents.RaiseHitConfirmed(confirm);
                    break;
                case ShotMessage shot:
                    ShowShot(shot);
                    break;
                case ExplosionMessage explosion:
                    var center = NetV2Math.ToUnity(explosion.Center);
                    Debug.Log(CombatLogTag + " Explosion weapon=" + explosion.WeaponId + " at=" + center.ToString("F2", CultureInfo.InvariantCulture)
                        + " thrower=" + (explosion.HasThrower ? explosion.ThrowerId.ToString(CultureInfo.InvariantCulture) : "none"), this);
                    ThrownGrenade.PlayExplosionEffects(center);
                    CombatEvents.RaiseExploded(center, explosion.WeaponId);
                    break;
                case TeleportedMessage teleported:
                    HandleTeleported(teleported);
                    break;
                case ShotIndexCorrectedMessage corrected:
                    // Netcode のクライアント (NetShotSequence.Correct) と同じく、まだサーバーが数えていない弾の分も含めて同じだけずらす
                    if (corrected.Generation != _shotGeneration)
                    {
                        _shotIndex = unchecked(_shotIndex + (corrected.ServerIndex - corrected.ClientIndex));
                        _shotGeneration = corrected.Generation;
                    }

                    break;
                case IdleWarningMessage idle:
                    if (idle.Active)
                    {
                        _hud?.ShowIdleWarning(idle.RemainingSeconds);
                    }
                    else
                    {
                        _hud?.ClearIdleWarning();
                    }

                    break;
                case LoadoutAnswerMessage answer:
                    _loadout.Answer(answer);
                    break;
                case InventoryStateMessage inventory:
                    _inventory.Receive(inventory);
                    break;
                case StructureStateMessage structure:
                    _structures.Receive(structure);
                    break;
                case VehicleCorrectedMessage corrected:
                    _match?.Vehicles.Correct(corrected);
                    break;
                case VoteKickStateMessage voteKick:
                    _voteKick.Receive(voteKick);
                    break;
                default:
                    if (!_weaponHooks.Handle(message) && !HandleByParts(message))
                    {
                        _pings.Handle(message, Time.time);
                    }

                    break;
            }
        }

        private bool HandleByParts(IMessage message)
        {
            foreach (var part in _parts)
            {
                if (part.Handle(message))
                {
                    return true;
                }
            }

            return false;
        }

        private void DrawBeam(BeamView beam)
        {
            var shooter = ShooterObject(beam.ShooterId);
            var from = NetV2Math.ToUnity(beam.Start);
            var to = NetV2Math.ToUnity(beam.End);

            // 目の位置から線を出すと一人称では線が画面の中心に潰れて見えないので、オフラインの BeamDelivery と同じく銃口から出す
            var presentation = WeaponPresentation.FindFor(shooter);
            if (presentation != null)
            {
                from = presentation.Sockets.MuzzlePose(from, to - from).position;
            }

            BeamLine.Show(shooter, beam.WeaponId, beam.TickSeconds, from, to, Time.time);
        }

        private void HideBeam(int shooterId)
        {
            var shooter = ShooterObject(shooterId);
            if (shooter != null && shooter.TryGetComponent<BeamLine>(out var line))
            {
                line.Hide();
            }
        }

        private void DrawSpray(SprayView spray)
        {
            var shooter = ShooterObject(spray.ShooterId);
            var from = NetV2Math.ToUnity(spray.Start);
            var toward = NetV2Math.ToUnity(spray.End) - from;
            var length = toward.magnitude;
            if (length < 1e-4f)
            {
                return;
            }

            var direction = toward / length;

            // 目の位置から出すと一人称では火炎が画面の中心に潰れて見えないので、オフラインの SprayDelivery と同じく銃口から出す
            var presentation = WeaponPresentation.FindFor(shooter);
            if (presentation != null)
            {
                from = presentation.Sockets.MuzzlePose(from, direction).position;
            }

            SprayFlame.Show(shooter, spray.WeaponId, spray.TickSeconds, from, direction, length, spray.ConeDegrees, Time.time);
        }

        private void HideSpray(int shooterId)
        {
            var shooter = ShooterObject(shooterId);
            if (shooter != null && shooter.TryGetComponent<SprayFlame>(out var flame))
            {
                flame.Stop();
            }
        }

        private GameObject ShooterObject(int shooterId)
        {
            if (_client != null && shooterId == _client.PlayerId)
            {
                return _local != null ? _local.gameObject : null;
            }

            return _remotes.TryGetValue(shooterId, out var remote) && remote != null ? remote.gameObject : null;
        }

        private void ShowShot(ShotMessage shot)
        {
            GameObject shooter = null;
            if (shot.HasShooter && _remotes.TryGetValue(shot.ShooterId, out var remote))
            {
                shooter = remote.gameObject;
            }

            // 演出は 1 発ごとに射撃→着弾の順に出す前提 (WeaponPresentation) なので、区間ごとに対で出す
            for (var i = 0; i < shot.SegmentCount; i++)
            {
                ref var segment = ref shot.Segment(i);
                var origin = NetV2Math.ToUnity(segment.Origin);
                var direction = NetV2Math.ToUnity(segment.Direction);

                // 2 区間目以降は、前の着弾から続く弾 (跳弾・貫通) も同じ射撃の別のペレットも、銃声やマズルフラッシュを出し直さない。
                if (i > 0)
                {
                    CombatEvents.RaiseShotContinued(shooter, origin, direction, shot.WeaponId);
                }
                else
                {
                    CombatEvents.RaiseShotFired(shooter, origin, direction, shot.WeaponId);
                }

                if (segment.HasImpact)
                {
                    HitZone? zone = segment.ImpactIsCharacter && segment.HasImpactZone ? segment.ImpactZone : (HitZone?)null;
                    CombatEvents.RaiseImpact(new ImpactInfo(
                        NetV2Math.ToUnity(segment.ImpactPoint), NetV2Math.ToUnity(segment.ImpactNormal), null,
                        segment.ImpactMaterialId ?? "", segment.ImpactIsCharacter, zone));
                }
            }
        }

        private void HandleTeleported(TeleportedMessage teleported)
        {
            if (teleported.PlayerId != _client.PlayerId)
            {
                _match?.ForgetRemoteHistory(teleported.PlayerId);
                return;
            }

            // サーバーは置き直した位置を落ちたときに戻す先にもする (ServerMovement.Teleport)
            if (_match != null)
            {
                _match.Prediction.Simulation.SpawnPosition = teleported.Position;
            }

            _local?.ReleaseHolds(CrouchInput.ReasonRespawn);
        }

        private void AddRemote(int playerId)
        {
            if (_remoteRoot == null || _remotes.ContainsKey(playerId))
            {
                return;
            }

            _remotes.Add(playerId, NetV2RemotePlayer.Create(playerId, _client.Motor.Stance, _client.Motor.Motor, _remoteRoot));
            Debug.Log(LogTag + " player " + playerId.ToString(CultureInfo.InvariantCulture) + " is in view", this);
        }

        private void RemoveRemote(int playerId)
        {
            _weaponHooks.StatusEffects.Forget(playerId);
            if (!_remotes.TryGetValue(playerId, out var remote))
            {
                return;
            }

            _remotes.Remove(playerId);
            if (remote != null)
            {
                Destroy(remote.gameObject);
            }

            Debug.Log(LogTag + " player " + playerId.ToString(CultureInfo.InvariantCulture) + " left", this);
        }

        // 自分の体は一人称で見えないので、燃えている見た目は相手の体にだけ付け、自分には HUD (NetV2Hud) で出す。
        private void ShowStatusEffect(int playerId, string kindId)
        {
            Debug.Log(LogTag + " status player=" + playerId.ToString(CultureInfo.InvariantCulture) + " kind=" + kindId + " on", this);
            if (_remotes.TryGetValue(playerId, out var remote) && remote != null)
            {
                StatusEffectVisuals.For(remote.gameObject).Show(kindId);
            }
        }

        private void HideStatusEffect(int playerId, string kindId)
        {
            Debug.Log(LogTag + " status player=" + playerId.ToString(CultureInfo.InvariantCulture) + " kind=" + kindId + " off", this);
            if (_remotes.TryGetValue(playerId, out var remote) && remote != null)
            {
                var visuals = remote.GetComponent<StatusEffectVisuals>();
                if (visuals != null)
                {
                    visuals.Hide(kindId);
                }
            }
        }

        // 体を作るのは ClientMatch が相手を足したときで、そのときは見た目の Id がまだ届いていないことがあるので、スナップショットのたびに合わせる (同じ Id なら何もしない)
        private void ShowCosmetics(SnapshotMessage snapshot)
        {
            var players = snapshot.Players;
            for (var i = 0; i < players.Count; i++)
            {
                if (_remotes.TryGetValue(players[i].PlayerId, out var remote) && remote != null)
                {
                    remote.ShowCosmetic(players[i].CosmeticId);
                }
            }
        }

        private void ShowRemotes()
        {
            foreach (var pair in _remotes)
            {
                if (_match.TrySampleRemote(pair.Key, out var pose))
                {
                    pair.Value.Show(pose);
                }
            }
        }

        private void ShowGrenades()
        {
            _grenadeCarrier ??= ShownCarrier;
            var renderTick = _match.Interpolation.RenderTick;
            foreach (var id in _world.GrenadeIds)
            {
                if (!_world.TryShowGrenade(id, renderTick, _grenadeCarrier, out var position, out var rotation))
                {
                    continue;
                }

                if (!_grenadeViews.TryGetValue(id, out var view) || view == null)
                {
                    view = CreateGrenadeView(_world.GrenadeTypeId(id));
                    _grenadeViews[id] = view;
                }

                view.transform.SetPositionAndRotation(NetV2Math.ToUnity(position), NetV2Math.ToUnity(rotation));
            }
        }

        // 自分は予測した位置に、他の人は補間した位置に出しているので、くっついたグレネードもその見せている位置に付ける
        private bool ShownCarrier(int playerId, out System.Numerics.Vector3 position, out float yaw)
        {
            if (playerId == _client.PlayerId)
            {
                position = _match.LocalState.Position;
                yaw = _ownYaw;
                return _local != null;
            }

            if (_match.TrySampleRemote(playerId, out var pose))
            {
                position = pose.Position;
                yaw = pose.Yaw;
                return true;
            }

            position = default;
            yaw = 0f;
            return false;
        }

        private GameObject CreateGrenadeView(string typeId)
        {
            GameObject view;
            if (GrenadePrefab != null)
            {
                view = new GameObject(ThrownGrenade.ObjectName);
                VisualPrefab.Instantiate(GrenadePrefab, view.transform, ThrownGrenade.VisualName, stripColliders: true);
            }
            else
            {
                view = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                view.name = ThrownGrenade.ObjectName;
                var spec = KitTuning.Grenades.TryFind(typeId, out var typed) ? typed : ThrownGrenade.UntypedSpec;
                view.transform.localScale = Vector3.one * (spec.BodyRadius * 2f);

                // 当たりはサーバーが取るので、見た目に Unity の物理を持たせない
                Destroy(view.GetComponent<Collider>());
            }

            view.transform.SetParent(_remoteRoot, false);
            return view;
        }

        private void ForgetGrenades(SnapshotMessage snapshot)
        {
            _goneGrenades.Clear();
            _world.ApplyGrenades(snapshot, _goneGrenades);
            foreach (var id in _goneGrenades)
            {
                if (_grenadeViews.TryGetValue(id, out var view))
                {
                    _grenadeViews.Remove(id);
                    if (view != null)
                    {
                        Destroy(view);
                    }
                }
            }
        }

        private void ShowArea(AreaEffectState state)
        {
            EndArea(state);
            var zone = AreaEffectZone.Show(state.KindId, NetV2Math.ToUnity(state.Center), state.Radius, state.RemainingSeconds);
            _areaZones[state.AreaId] = zone;
        }

        private void UpdateArea(AreaEffectState state)
        {
            if (_areaZones.TryGetValue(state.AreaId, out var zone) && zone != null)
            {
                zone.SetRemaining(state.RemainingSeconds);
                return;
            }

            ShowArea(state);
        }

        private void EndArea(AreaEffectState state)
        {
            if (_areaZones.TryGetValue(state.AreaId, out var zone))
            {
                _areaZones.Remove(state.AreaId);
                if (zone != null)
                {
                    zone.End();
                }
            }
        }
    }
}
