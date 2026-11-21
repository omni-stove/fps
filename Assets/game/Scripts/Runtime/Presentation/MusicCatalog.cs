using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 曲の枠が空の場面は MusicPlayer が無音にする。カタログが無ければどの場面も無音。
    [CreateAssetMenu(fileName = "MusicCatalog", menuName = "Shooter/Music Catalog")]
    public sealed class MusicCatalog : ScriptableObject
    {
        public const string CatalogName = "Music";

        public const float DefaultVolume = 0.6f;

        public const float DefaultFadeSeconds = 1.5f;

        public const float MaxFadeSeconds = 10f;

        public static readonly string ResourcePath = PresentationCatalog.CatalogResourcePath(CatalogName);

        [SerializeField, Tooltip("メインメニューで流す曲")]
        private AudioClip _menu;

        [SerializeField, Tooltip("メニューの外 (試合・射撃場・キャンペーン) で、試合が終わるまで流す曲")]
        private AudioClip _match;

        [SerializeField, Tooltip("試合に勝ったときの曲。空なら終わりの曲")]
        private AudioClip _victory;

        [SerializeField, Tooltip("試合に負けたときの曲。空なら終わりの曲")]
        private AudioClip _defeat;

        [SerializeField, Tooltip("試合が終わったときの曲 (引き分け・観戦、勝ち負けの曲が空のとき)")]
        private AudioClip _matchEnd;

        [SerializeField, Range(0f, 1f), Tooltip("曲の音量。設定の BGM の音量と全体の音量を掛ける")]
        private float _volume = DefaultVolume;

        [SerializeField, Range(0f, MaxFadeSeconds), Tooltip("場面が変わったときに前の曲を消し、次の曲を上げるまでのそれぞれの秒数。0 ならすぐ切り替える")]
        private float _fadeSeconds = DefaultFadeSeconds;

        [SerializeField, Tooltip("試合の終わり (勝ち・負け・終わり) の曲をくり返すか。切れば 1 度だけ鳴らして止める")]
        private bool _loopMatchEnd;

        public AudioClip Menu
        {
            get => _menu;
            set => _menu = value;
        }

        public AudioClip Match
        {
            get => _match;
            set => _match = value;
        }

        public AudioClip Victory
        {
            get => _victory;
            set => _victory = value;
        }

        public AudioClip Defeat
        {
            get => _defeat;
            set => _defeat = value;
        }

        public AudioClip MatchEnd
        {
            get => _matchEnd;
            set => _matchEnd = value;
        }

        public float Volume
        {
            get => Mathf.Clamp01(_volume);
            set => _volume = Mathf.Clamp01(value);
        }

        public float FadeSeconds
        {
            get => Mathf.Clamp(_fadeSeconds, 0f, MaxFadeSeconds);
            set => _fadeSeconds = Mathf.Clamp(value, 0f, MaxFadeSeconds);
        }

        public bool LoopMatchEnd
        {
            get => _loopMatchEnd;
            set => _loopMatchEnd = value;
        }

        public static MusicCatalog Load()
        {
            var fromContent = GameContent.Pick(c => c.Music);
            return fromContent != null ? fromContent : Resources.Load<MusicCatalog>(ResourcePath);
        }

        public AudioClip ClipFor(MusicCue cue)
        {
            switch (cue)
            {
                case MusicCue.None:
                    return null;
                case MusicCue.Menu:
                    return Present(_menu);
                case MusicCue.Match:
                    return Present(_match);
                case MusicCue.Victory:
                    return Present(_victory) ?? Present(_matchEnd);
                case MusicCue.Defeat:
                    return Present(_defeat) ?? Present(_matchEnd);
                case MusicCue.MatchEnd:
                    return Present(_matchEnd);
                default:
                    throw new ArgumentOutOfRangeException(nameof(cue), cue, "unknown music cue");
            }
        }

        public bool Loops(MusicCue cue)
        {
            switch (cue)
            {
                case MusicCue.Victory:
                case MusicCue.Defeat:
                case MusicCue.MatchEnd:
                    return _loopMatchEnd;
                default:
                    return true;
            }
        }

        public void Validate()
        {
            var source = string.IsNullOrEmpty(name) ? "(unnamed music catalog)" : name;
            var problems = new List<string>();

            // 破棄されたアセットや参照切れも拾うため、Unity の == null で比べる。
            if (_menu == null)
            {
                problems.Add("menu music is missing");
            }

            if (_match == null)
            {
                problems.Add("match music is missing");
            }

            if (ClipFor(MusicCue.Victory) == null || ClipFor(MusicCue.Defeat) == null)
            {
                problems.Add("match end music is missing (set the match end clip, or both the victory and defeat clips)");
            }

            if (problems.Count > 0)
            {
                throw new PresentationCatalogException($"music catalog '{source}': {string.Join("; ", problems)}");
            }
        }

        // ?? では Unity の破棄済みの参照を null と見なさないので、== null で null にそろえる。
        private static AudioClip Present(AudioClip clip)
        {
            return clip == null ? null : clip;
        }
    }
}
