using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 演出に使う素材 (今はヒットマーカーの音) をまとめる設定。Resources の <see cref="ResourcePath"/> に置いたものを
    /// <see cref="Load"/> で読む。置いていなければ演出は音を鳴らさずに見た目だけ出す。
    /// 中身はエディタースクリプトでアセットを作って入れる (Inspector での手の配線は前提にしない)。
    /// </summary>
    public sealed class PresentationCatalog : ScriptableObject
    {
        /// <summary>Resources からのパス (どこかの Resources フォルダの Presentation/PresentationCatalog.asset)。</summary>
        public const string ResourcePath = "Presentation/PresentationCatalog";

        [SerializeField, Tooltip("生きている相手に当てたときの音。空なら鳴らさない")]
        private AudioClip _hitMarkerSound;

        [SerializeField, Tooltip("倒したときの音。空なら当てたときの音を使う")]
        private AudioClip _killMarkerSound;

        [SerializeField, Range(0f, 1f), Tooltip("ヒットマーカーの音量")]
        private float _hitMarkerVolume = 0.4f;

        /// <summary>当てたときの音。無ければ null。</summary>
        public AudioClip HitMarkerSound
        {
            get => _hitMarkerSound;
            set => _hitMarkerSound = value;
        }

        /// <summary>倒したときの音。無ければ null。</summary>
        public AudioClip KillMarkerSound
        {
            get => _killMarkerSound;
            set => _killMarkerSound = value;
        }

        /// <summary>ヒットマーカーの音量 ([0, 1])。</summary>
        public float HitMarkerVolume
        {
            get => _hitMarkerVolume;
            set => _hitMarkerVolume = Mathf.Clamp01(value);
        }

        /// <summary>killed かどうかで鳴らす音。倒したときの音が無ければ当てたときの音。どちらも無ければ null。</summary>
        public AudioClip HitMarkerSoundFor(bool killed)
        {
            return killed && _killMarkerSound != null ? _killMarkerSound : _hitMarkerSound;
        }

        /// <summary>Resources の <see cref="ResourcePath"/> から読む。置いていなければ null。</summary>
        public static PresentationCatalog Load()
        {
            return Resources.Load<PresentationCatalog>(ResourcePath);
        }
    }
}
