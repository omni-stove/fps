using System.Collections.Generic;
using Fps.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.Net
{
    /// <summary>
    /// <see cref="NetTeamDeathmatch"/> のうち、出現地点の収集と、出現・リスポーンでプレイヤーを置き直す処理。
    /// </summary>
    public sealed partial class NetTeamDeathmatch
    {
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

            ArmForSpawn(_serverLoadout, _session.Spec.Rules, player.Weapon);

            var p = point.Position;
            player.ServerTeleport(new Vector3(p.X, p.Y, p.Z));
            Debug.Log(MatchLog.Spawn(playerId, point));
        }
    }
}
