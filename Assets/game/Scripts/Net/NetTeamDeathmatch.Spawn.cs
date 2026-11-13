using System.Collections.Generic;
using System.Globalization;
using Fps.Core;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.Net
{
    /// <summary>
    /// <see cref="NetTeamDeathmatch"/> のうち、出現地点の収集と、出現・リスポーンでプレイヤーを置き直す処理と、出現直後の無敵の同期。
    /// </summary>
    /// <remarks>
    /// 無敵の判定は Session (<see cref="TeamDeathmatchSession.IsSpawnProtected"/>) だけが持ち、ここでは見た目と HUD 用に、
    /// 無敵の最中の人と残り秒数を <see cref="SpawnProtections"/> で配る。NetPlayer ごとの NetworkVariable にしないのは、
    /// 出現・リスポーン・撃って解けるのをどれも試合の側で書き直し、試合が消えたら一緒に消えるようにするため。
    /// </remarks>
    public sealed partial class NetTeamDeathmatch
    {
        // NetworkList は Awake で作る (フィールド初期化子だとエディタで破棄されずに残ることがある)
        private NetworkList<NetSpawnProtection> _spawnProtections;

        /// <summary>出現直後の無敵の最中のプレイヤーと、無敵が切れるまでの秒数 (切り上げ)。無敵でない人の行は無い。</summary>
        public NetworkList<NetSpawnProtection> SpawnProtections => _spawnProtections;

        /// <summary>
        /// clientId のプレイヤーの、出現直後の無敵が切れるまでの秒数 (切り上げ)。無敵でない・まだ届いていなければ 0。
        /// クライアントでもサーバーでも、配られた <see cref="SpawnProtections"/> から引く。
        /// </summary>
        public int GetSpawnProtectionSeconds(ulong clientId)
        {
            var index = FindSpawnProtectionRow(clientId);
            return index >= 0 ? _spawnProtections[index].Seconds : 0;
        }

        /// <summary>
        /// サーバーで、shooterId の出現直後の無敵を解く。弾を撃ったときに <see cref="ServerFire"/> が呼ぶ。
        /// 無敵だったときだけ <c>[Match] spawn protection ended player=.. reason=fired</c> を出し、配る一覧から消す。
        /// </summary>
        private void ServerEndSpawnProtection(int shooterId)
        {
            if (!_session.EndSpawnProtection(shooterId))
            {
                return;
            }

            Debug.Log("[Match] spawn protection ended player=" + shooterId.ToString(CultureInfo.InvariantCulture) + " reason=fired");
            SyncSpawnProtection(shooterId);
        }

        /// <summary>参加中の全員の無敵の残りを書き直す。Session の Tick の後に毎フレーム呼ぶ。変わっていなければ送られない。</summary>
        private void SyncSpawnProtections()
        {
            foreach (var playerId in _playerById.Keys)
            {
                SyncSpawnProtection(playerId);
            }
        }

        private void SyncSpawnProtection(int playerId)
        {
            if (_spawnProtections == null
                || !_playerById.TryGetValue(playerId, out var player)
                || player == null
                || !_session.HasPlayer(playerId))
            {
                return;
            }

            var seconds = NetSpawnProtection.SecondsFrom(_session.GetSpawnProtectionRemaining(playerId));
            var clientId = player.RosterId;
            var index = FindSpawnProtectionRow(clientId);
            if (seconds <= 0)
            {
                if (index >= 0)
                {
                    _spawnProtections.RemoveAt(index);
                }

                return;
            }

            var row = new NetSpawnProtection(clientId, seconds);
            if (index < 0)
            {
                _spawnProtections.Add(row);
            }
            else if (!_spawnProtections[index].Equals(row))
            {
                _spawnProtections[index] = row;
            }
        }

        private void RemoveSpawnProtectionRow(ulong clientId)
        {
            var index = FindSpawnProtectionRow(clientId);
            if (index >= 0)
            {
                _spawnProtections.RemoveAt(index);
            }
        }

        // Awake を経ない Edit Mode の試合では一覧が無い
        private int FindSpawnProtectionRow(ulong clientId)
        {
            if (_spawnProtections == null)
            {
                return -1;
            }

            for (var i = 0; i < _spawnProtections.Count; i++)
            {
                if (_spawnProtections[i].ClientId == clientId)
                {
                    return i;
                }
            }

            return -1;
        }

        private System.Numerics.Vector3 PositionOf(int playerId)
        {
            var p = _playerById[playerId].transform.position;
            return new System.Numerics.Vector3(p.x, p.y, p.z);
        }

        /// <summary>
        /// 出現地点の目印 (<see cref="NetSpawnPoint"/>) をシーンから集める。並び順は SpawnSelector の同点時の決め手になるので、
        /// 読み込まれたシーンの階層順で固定する。
        /// </summary>
        private static List<SpawnPoint> CollectSpawnPoints()
        {
            var points = new List<SpawnPoint>();
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                {
                    continue;
                }

                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var marker in root.GetComponentsInChildren<NetSpawnPoint>())
                    {
                        points.Add(marker.ToSpawnPoint());
                    }
                }
            }

            return points;
        }

        private void HandleSpawned(int playerId, SpawnPoint point)
        {
            if (!_playerById.TryGetValue(playerId, out var player))
            {
                return;
            }

            // テレポートの前後を補間して途中の位置に当たらないよう、履歴を捨てて出現地点から記録し直す。
            // 出現より前の時点まで巻き戻した射撃には、その時点にいなかったものとして当たらない
            _lagCompensator?.Remove(playerId);

            // 選んだ並びはここで初めて配る (倒れる前・試合の途中では持ち替えない)
            ArmForSpawn(_serverLoadout, _session.Spec.Rules, player.Weapon, playerId);
            ServerRefillGrenades(playerId, player);

            var p = point.Position;
            player.ServerTeleport(new Vector3(p.X, p.Y, p.Z));
            Debug.Log(MatchLog.Spawn(playerId, point));

            // 毎フレームの書き直しを待たず、出現と同じフレームで配る
            SyncSpawnProtection(playerId);
            var protection = _session.GetSpawnProtectionRemaining(playerId);
            if (protection > 0f)
            {
                Debug.Log("[Match] spawn protection player=" + playerId.ToString(CultureInfo.InvariantCulture)
                    + " seconds=" + protection.ToString("0.##", CultureInfo.InvariantCulture));
            }
        }
    }
}
