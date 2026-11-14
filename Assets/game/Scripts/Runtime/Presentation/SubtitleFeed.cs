using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 鳴らした音の合図を字幕 (<see cref="SubtitleView"/>) に知らせる、静的なイベント。
    /// 音を鳴らす側 (<see cref="WeaponPresentation"/> の銃声とリロード音・<see cref="CharacterAudio"/> の足音と着地音・
    /// <see cref="ThrownGrenade.PlayExplosionEffects"/> の爆発音) が、実際に音を鳴らしたときだけ <see cref="Raise"/> を呼ぶ。
    /// 音が聞こえる人と同じ合図だけを字幕にするため、素材が無くて鳴らなかった音は流さない。
    /// 向きと距離は聞いている人 (カメラ) によって変わるので、ここでは鳴った位置だけを渡し、受け取った字幕の側で決める。
    /// 購読者が例外を投げても音の処理は止めず、ログに出して残りの購読者を呼ぶ。
    /// 静的なので、ドメインの再読み込みを切った Play でも前回の購読が残らないよう、Play の開始時に購読をすべて外す。
    /// </summary>
    public static class SubtitleFeed
    {
        /// <summary>音の合図が鳴ったとき。</summary>
        public static event Action<SoundCueSource> CuePlayed;

        /// <summary>
        /// kind の音を position で鳴らしたことを知らせる。position が null なら鳴らした本人の耳元の音 (向きを書かない)。
        /// audibleDistance (m) はその音が聞こえなくなる距離で、それより遠い人には字幕を出さない。
        /// </summary>
        public static void Raise(SoundCueKind kind, Vector3? position, float audibleDistance)
        {
            var handlers = CuePlayed;
            if (handlers == null)
            {
                return;
            }

            var source = new SoundCueSource(kind, position, audibleDistance);
            foreach (var handler in handlers.GetInvocationList())
            {
                try
                {
                    ((Action<SoundCueSource>)handler)(source);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        /// <summary>購読をすべて外す。Play の開始時と、テストの後片付けで呼ぶ。</summary>
        public static void ClearSubscribers()
        {
            CuePlayed = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            ClearSubscribers();
        }
    }

    /// <summary><see cref="SubtitleFeed.CuePlayed"/> で渡す、鳴らした音の合図。</summary>
    public readonly struct SoundCueSource
    {
        public SoundCueSource(SoundCueKind kind, Vector3? position, float audibleDistance)
        {
            Kind = kind;
            Position = position;
            AudibleDistance = audibleDistance;
        }

        public SoundCueKind Kind { get; }

        /// <summary>鳴った位置。鳴らした本人の耳元の音 (自分のリロード音など) なら null。</summary>
        public Vector3? Position { get; }

        /// <summary>この距離 (m) より遠いと聞こえない。</summary>
        public float AudibleDistance { get; }
    }
}
