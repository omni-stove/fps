using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 拾える物 1 種類の調整値 (中身・拾い方・再出現の秒数・拾える範囲) をアセットとして持つ。<see cref="PickupSpawnPoint.Definition"/> に
    /// 入れると、出現点は自分の値の代わりにこれを読む。同じアセットを入れた出現点は、アセットを変えればまとめて変わる。
    /// 新しく作ったときの値は <see cref="PickupSpawnPoint"/> の既定値と同じ。
    /// </summary>
    [CreateAssetMenu(fileName = "Pickup", menuName = "Fps/Pickup")]
    public sealed class PickupDefinition : ScriptableObject
    {
        [SerializeField, Tooltip("中身")]
        private PickupContents _contents = PickupContents.ForHealth(PickupContents.DefaultHealthAmount);

        [SerializeField, Tooltip("拾い方")]
        private PickupCollectMode _collectMode = PickupCollectMode.Touch;

        [SerializeField, Min(0f), Tooltip("拾われてから再び出るまでの秒数。0 なら一度きり")]
        private float _respawnSeconds = PickupSpawnPoint.DefaultRespawnSeconds;

        [SerializeField, Min(Pickup.MinRadius), Tooltip("拾える範囲の半径 (m)")]
        private float _radius = Pickup.DefaultRadius;

        /// <summary>中身。null は <see cref="ArgumentNullException"/>、値が不正なら <see cref="ArgumentException"/>。</summary>
        public PickupContents Contents
        {
            get => _contents;
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                value.ToSpec();
                _contents = value;
            }
        }

        public PickupCollectMode CollectMode
        {
            get => _collectMode;
            set => _collectMode = value;
        }

        public float RespawnSeconds
        {
            get => _respawnSeconds;
            set => _respawnSeconds = value;
        }

        public float Radius
        {
            get => _radius;
            set => _radius = value;
        }

        /// <summary>中身の Core の表し。呼ぶたびに作る。</summary>
        /// <exception cref="ArgumentException">中身が無いか不正。メッセージにアセット名を含む。</exception>
        public PickupSpec ToSpec()
        {
            if (_contents == null)
            {
                throw new ArgumentException($"pickup definition '{SourceName}' has no contents");
            }

            try
            {
                return _contents.ToSpec();
            }
            catch (ArgumentException e)
            {
                throw new ArgumentException($"pickup definition '{SourceName}' has invalid contents: {e.Message}", e);
            }
        }

        /// <summary>再出現の時間を作る。</summary>
        /// <exception cref="ArgumentException">再出現の秒数が負か有限でない。メッセージにアセット名を含む。</exception>
        public PickupRespawn CreateRespawn()
        {
            try
            {
                return new PickupRespawn(_respawnSeconds);
            }
            catch (ArgumentException e)
            {
                throw new ArgumentException($"pickup definition '{SourceName}' has an invalid respawn time: {e.Message}", e);
            }
        }

        /// <summary>中身・再出現の秒数・範囲がすべて使えることを確かめる。</summary>
        /// <exception cref="ArgumentException">どれかが不正。メッセージにアセット名を含む。</exception>
        public void Validate()
        {
            ToSpec();
            CreateRespawn();
            if (!(_radius >= Pickup.MinRadius) || float.IsInfinity(_radius))
            {
                throw new ArgumentException($"pickup definition '{SourceName}' radius {_radius} must be a finite number of at least {Pickup.MinRadius}");
            }
        }

        private string SourceName => string.IsNullOrEmpty(name) ? "(unnamed pickup definition)" : name;
    }
}
