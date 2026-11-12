using System;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// キャンペーンのチェックポイント。プレイヤー (<see cref="PlayerTag"/> の付いた Collider) がトリガーの Collider に入ったら、
    /// <see cref="CampaignRunner.SaveAt"/> に <see cref="CheckpointId"/> を渡してセーブさせる。セーブできたら以後は何もしない
    /// (失敗したときは、次に入ったときにもう一度試す)。
    /// 倒れて再開するときは <see cref="CampaignRunner"/> が Id でこれを探し、<see cref="RespawnPosition"/> にプレイヤーを移す。
    /// CampaignRunner は Inspector で指定しなければシーンから探す。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CheckpointTrigger : MonoBehaviour
    {
        [SerializeField, Tooltip("セーブさせる CampaignRunner。空ならシーンから探す")]
        private CampaignRunner _runner;

        [SerializeField, Tooltip("シーン内で一意なチェックポイントの Id。セーブデータに残る")]
        private string _checkpointId = string.Empty;

        [SerializeField, Tooltip("プレイヤーとみなす Collider のタグ")]
        private string _playerTag = ObjectiveAreaBase.DefaultPlayerTag;

        [SerializeField, Tooltip("再開するときのプレイヤーの位置。この GameObject の位置からのずれ (ワールド座標)")]
        private Vector3 _respawnOffset = new Vector3(0f, GameBootstrap.PlayerHeight / 2f, 0f);

        private bool _reached;

        public string CheckpointId
        {
            get => _checkpointId;
            set => _checkpointId = value;
        }

        public CampaignRunner Runner
        {
            get => _runner;
            set => _runner = value;
        }

        public string PlayerTag
        {
            get => _playerTag;
            set => _playerTag = value;
        }

        /// <summary>
        /// 再開位置のずれ。既定ではこの GameObject を床に置いたとき、プレイヤー (原点がカプセルの中心) の足が床に着く高さ。
        /// </summary>
        public Vector3 RespawnOffset
        {
            get => _respawnOffset;
            set => _respawnOffset = value;
        }

        /// <summary>再開するときにプレイヤーの原点を置く位置。</summary>
        public Vector3 RespawnPosition => transform.position + _respawnOffset;

        /// <summary>このチェックポイントでセーブ済み (または、ここから再開した) なら true。</summary>
        public bool IsReached => _reached;

        private void Start()
        {
            if (string.IsNullOrWhiteSpace(_checkpointId))
            {
                Debug.LogError($"{CampaignRunner.LogTag} CheckpointTrigger '{name}' に CheckpointId が設定されていません。", this);
                enabled = false;
                return;
            }

            if (!HasTriggerCollider())
            {
                Debug.LogError($"{CampaignRunner.LogTag} CheckpointTrigger '{name}' に isTrigger の Collider がありません。", this);
                enabled = false;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // 無効なコンポーネントにもトリガーの通知は届くので、ここで止める。
            if (enabled)
            {
                HandleEnter(other);
            }
        }

        /// <summary>
        /// Collider が範囲に入った。まだセーブしていないチェックポイントにプレイヤーが入ったならセーブさせ、セーブできたら true。
        /// </summary>
        public bool HandleEnter(Collider other)
        {
            if (_reached || !IsPlayer(other))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(_checkpointId))
            {
                Debug.LogError($"{CampaignRunner.LogTag} CheckpointTrigger '{name}' に CheckpointId が設定されていません。", this);
                return false;
            }

            var runner = _runner != null ? _runner : FindAnyObjectByType<CampaignRunner>();
            if (runner == null)
            {
                Debug.LogError($"{CampaignRunner.LogTag} シーンに CampaignRunner がないため、チェックポイント '{_checkpointId}' でセーブできません。", this);
                return false;
            }

            if (!runner.SaveAt(_checkpointId))
            {
                return false;
            }

            _reached = true;
            return true;
        }

        /// <summary>セーブ済みとして扱い、以後は入ってもセーブさせない。ここから再開したときに CampaignRunner が呼ぶ。</summary>
        public void MarkReached()
        {
            _reached = true;
        }

        /// <summary>
        /// シーンにある有効な CheckpointTrigger から Id が checkpointId のものを探す。無ければ null。
        /// 同じ Id が複数あれば警告を出し、そのうち 1 つを返す。
        /// </summary>
        public static CheckpointTrigger Find(string checkpointId)
        {
            if (string.IsNullOrEmpty(checkpointId))
            {
                return null;
            }

            CheckpointTrigger found = null;
            foreach (var candidate in FindObjectsByType<CheckpointTrigger>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!string.Equals(candidate._checkpointId, checkpointId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (found != null)
                {
                    Debug.LogWarning(
                        $"{CampaignRunner.LogTag} チェックポイント '{checkpointId}' が複数あります ('{found.name}', '{candidate.name}')。'{found.name}' を使います。",
                        candidate);
                    continue;
                }

                found = candidate;
            }

            return found;
        }

        private bool IsPlayer(Collider other)
        {
            return other != null && !string.IsNullOrEmpty(_playerTag) && other.CompareTag(_playerTag);
        }

        private bool HasTriggerCollider()
        {
            foreach (var collider in GetComponents<Collider>())
            {
                if (collider.isTrigger)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
