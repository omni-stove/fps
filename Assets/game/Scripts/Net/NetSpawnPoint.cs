using Fps.Core;
using UnityEngine;

namespace Fps.Net
{
    /// <summary>
    /// シーンに置くリスポーン地点の目印。サーバーの <see cref="NetTeamDeathmatch"/> が実行時に集めて
    /// <see cref="SpawnPoint"/> に変換する。判断は持たない。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetSpawnPoint : MonoBehaviour
    {
        /// <summary>地点を使えるチーム。Team? は直列化できないので Any を足した別の列挙にする。</summary>
        public enum SpawnTeam
        {
            Any,
            Alpha,
            Bravo,
        }

        [SerializeField]
        [Tooltip("この地点を使えるチーム")]
        private SpawnTeam _team = SpawnTeam.Any;

        public SpawnTeam Team
        {
            get => _team;
            set => _team = value;
        }

        /// <summary>今の位置とチームから Core の <see cref="SpawnPoint"/> を作る。</summary>
        public SpawnPoint ToSpawnPoint()
        {
            var p = transform.position;
            return new SpawnPoint(new System.Numerics.Vector3(p.x, p.y, p.z), ToCoreTeam(_team));
        }

        public static Fps.Core.Team? ToCoreTeam(SpawnTeam team)
        {
            switch (team)
            {
                case SpawnTeam.Alpha:
                    return Fps.Core.Team.Alpha;
                case SpawnTeam.Bravo:
                    return Fps.Core.Team.Bravo;
                default:
                    return null;
            }
        }

        private void OnDrawGizmos()
        {
            // 編集中に保存した設定を読みに行かない (読むと全体の音量まで当ててしまう) よう、標準の組の Alpha 側から見た色で描く
            var team = ToCoreTeam(_team);
            Gizmos.color = team.HasValue
                ? Fps.Runtime.TeamColors.Of(TeamPalette.Standard, team, null)
                : Color.white;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
        }
    }
}
