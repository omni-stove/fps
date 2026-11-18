using System;
using System.Globalization;
using System.IO;
using Shooter.Core;
using Shooter.Protocol;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.NetV2
{
    public sealed partial class NetV2Client
    {
        private const string _sceneExtension = ".unity";

        private void LoadMap(string map)
        {
            _mapChange.Enter(map);
            _loadingMap = map;
            if (SceneManager.GetActiveScene().name == map)
            {
                EnterMap(map);
                return;
            }

            if (!TryFindScene(map, out var path))
            {
                Leave(ExitMisconfigured, LogTag + " the server's map " + map + " is not a scene in this build; build the map and rebuild");
                return;
            }

            Debug.Log(LogTag + " loading the server's map " + map + " (" + path + ")", this);
            _mapLoad = SceneManager.LoadSceneAsync(path, LoadSceneMode.Single);
            if (_mapLoad == null)
            {
                Leave(ExitMisconfigured, LogTag + " cannot load the server's map " + map + " (" + path + ")");
            }
        }

        // サーバーは同じ接続のまま次の試合を新しいマップで始めるので、つなぎ直さずに、マップから作った物だけを捨てて新しいシーンで作り直す。
        private void ChangeMap(string map)
        {
            Debug.Log(MapVoteLog.ClientMapChanging(_mapChange.CurrentMap, map), this);
            LeaveMap();
            LoadMap(map);
        }

        // Destroy はフレームの終わりまで遅れ、その間も Unity の == null は偽のままなので、捨てた物を指す欄は自分で空にする。
        private void LeaveMap()
        {
            if (_match != null)
            {
                _match.PlayerAdded -= AddRemote;
                _match.PlayerRemoved -= RemoveRemote;
                _match.Reconciled -= HandleReconciled;
                _match.LadderGrabbed -= HandleLadderGrabbed;
                _match.Detach();
                _match = null;
            }

            _heldContinuous = null;
            _heldContinuousWeaponId = null;
            _wasReloading = false;
            _loadout.Bind(null);
            DestroyViews();
            _local = null;
            _presentation = null;
            _dryFire = null;
            _lockOn = null;
            _pingDisplay = null;
            _hud = null;
            _remoteRoot = null;
            _world = null;
            _structures?.ClearStates();
        }

        private System.Numerics.Vector3? ControlPointPosition(int index)
        {
            foreach (var zone in _zones)
            {
                if (zone != null && zone.Index == index)
                {
                    return NetV2Math.ToNumerics(zone.transform.position);
                }
            }

            return null;
        }

        private void EnterMap(string map)
        {
            if (!ClientWorld.TryLoad(MapCollisionFiles.Directory, map, out _world, out var error))
            {
                Leave(ExitMisconfigured, LogTag + " " + error);
                return;
            }

            Debug.Log(_world.LogLine, this);
            var motor = _client.Motor;
            Debug.Log(LogTag + " motor from the server's rules walk=" + motor.Motor.WalkSpeed.ToString(CultureInfo.InvariantCulture)
                + " sprint=" + motor.Motor.SprintSpeed.ToString(CultureInfo.InvariantCulture)
                + " height=" + motor.Stance.Standing.Height.ToString(CultureInfo.InvariantCulture)
                + " radius=" + motor.Stance.Radius.ToString(CultureInfo.InvariantCulture)
                + " proneSpeed=" + motor.Prone.Speed.ToString(CultureInfo.InvariantCulture), this);
            _match = new ClientMatch(_client, _world.CreateSimulation(motor, _client.TickRate, _client.KillPlaneY, _client.Knockback));
            _weaponHooks.Projectiles.Attach(
                new NetV2Projectiles(_weapons), _world.Collision, Physics.DefaultRaycastLayers, NetV2Math.ToNumerics(Physics.gravity), _client.PlayerId);
            _weaponHooks.Beams.Attach(_world.Collision, Physics.DefaultRaycastLayers, _client.PlayerId);
            _weaponHooks.Spray.Attach(_world.Collision, Physics.DefaultRaycastLayers, _client.PlayerId);
            _match.PlayerAdded += AddRemote;
            _match.PlayerRemoved += RemoveRemote;
            _match.Reconciled += HandleReconciled;
            _match.LadderGrabbed += HandleLadderGrabbed;
            var scene = SceneManager.GetActiveScene();
            _structures.Enter(scene, _world);
            _vehicles.Enter(scene, _match.Vehicles, _client.Motor.Stance, _weapons);
            CollectControlPoints(scene);
            _remoteRoot = new GameObject("NetV2Players").transform;
            foreach (var id in _match.RemotePlayerIds)
            {
                AddRemote(id);
            }

            _hud = NetV2Hud.Create(_client.PlayerId, _client.MaxHealth, _weapons.OwnSpecFor);
            _hud.BindStatusEffects(_weaponHooks.StatusEffects);
            _hud.BindInventory(_inventory);

            _loadout.Bind(GameUi.LoadoutSelect);

            if (_client.LatestSnapshot != null)
            {
                HandleSnapshot(_client.LatestSnapshot);
            }

            Debug.Log(LogTag + " synchronized with the server; map " + map, this);
        }

        private void CollectControlPoints(Scene scene)
        {
            _zones.Clear();
            foreach (var root in scene.GetRootGameObjects())
            {
                _zones.AddRange(root.GetComponentsInChildren<ControlPointZone>(false));
            }
        }

        // 拠点の円盤を持ち主の色で塗る (Netcode の NetControlPoint と同じ)。拠点はマップの番号 (ControlPointZone.Index) で指す
        private void ShowControlPoints(SnapshotMessage snapshot)
        {
            var points = snapshot.ControlPoints;
            foreach (var zone in _zones)
            {
                if (zone == null)
                {
                    continue;
                }

                var at = points.IndexOf(zone.Index);
                Team? owner = at >= 0 && points[at].HasOwner ? points[at].Owner : (Team?)null;
                if (zone.Owner != owner)
                {
                    zone.ShowOwner(owner);
                }
            }
        }

        private static bool TryFindScene(string map, out string path)
        {
            for (var i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                var candidate = SceneUtility.GetScenePathByBuildIndex(i);
                var normalized = candidate.Replace('\\', '/');
                if (normalized.EndsWith(_sceneExtension, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(Path.GetFileNameWithoutExtension(normalized), map, StringComparison.Ordinal))
                {
                    path = candidate;
                    return true;
                }
            }

            path = null;
            return false;
        }
    }
}
