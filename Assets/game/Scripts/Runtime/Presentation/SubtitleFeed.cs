using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    /// <summary>
    /// 鳴らした音の合図を字幕 (既定の UI では Shooter.Runtime.UI の SubtitleView) に知らせる、静的なイベント。
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

        /// <summary>誰かが話したとき。</summary>
        public static event Action<SubtitleSpeech> Spoken;

        /// <summary>分隊の bot が指示に応えたとき。何と言うかは受け取った UI が決める。</summary>
        public static event Action<SquadOrderReply> SquadReplied;

        /// <summary>
        /// speaker が text (今の言語で組み立てた文) を話したことを知らせる。声の素材が無くても字幕は出す
        /// (話した内容は音の合図と違い、字幕でしか伝わらないことがあるので)。
        /// </summary>
        /// <exception cref="ArgumentNullException">speaker か text が null。</exception>
        public static void Say(string speaker, string text)
        {
            if (speaker == null)
            {
                throw new ArgumentNullException(nameof(speaker));
            }

            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            var handlers = Spoken;
            if (handlers == null)
            {
                return;
            }

            var speech = new SubtitleSpeech(speaker, text);
            foreach (var handler in handlers.GetInvocationList())
            {
                try
                {
                    ((Action<SubtitleSpeech>)handler)(speech);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        /// <summary>
        /// 分隊の bot が指示に応えたことを知らせる。声の素材が無くても知らせる (<see cref="Say"/> と同じ理由)。
        /// </summary>
        public static void Reply(SquadOrderReply reply)
        {
            var handlers = SquadReplied;
            if (handlers == null)
            {
                return;
            }

            foreach (var handler in handlers.GetInvocationList())
            {
                try
                {
                    ((Action<SquadOrderReply>)handler)(reply);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

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
            Spoken = null;
            SquadReplied = null;
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

    /// <summary><see cref="SubtitleFeed.Spoken"/> で渡す、話した人と話した文。</summary>
    public readonly struct SubtitleSpeech
    {
        public SubtitleSpeech(string speaker, string text)
        {
            Speaker = speaker;
            Text = text;
        }

        /// <summary>話した人。同じ人の字幕は 1 行にまとめる。</summary>
        public string Speaker { get; }

        /// <summary>出す文 (今の言語で組み立て済み)。</summary>
        public string Text { get; }
    }

    /// <summary><see cref="SubtitleFeed.SquadReplied"/> で渡す、分隊の bot の指示への応答。</summary>
    public readonly struct SquadOrderReply
    {
        private SquadOrderReply(string speaker, BotErrandKind order, bool isTargetDown)
        {
            if (speaker == null)
            {
                throw new ArgumentNullException(nameof(speaker));
            }

            if (!BotErrand.IsOrderKind(order))
            {
                throw new ArgumentOutOfRangeException(nameof(order), order, "order must be a squad order (Hold, Attack or Follow)");
            }

            Speaker = speaker;
            Order = order;
            IsTargetDown = isTargetDown;
        }

        /// <summary>応えた bot (<see cref="EnemyAgent.Id"/>)。同じ bot の字幕は 1 行にまとめる。</summary>
        public string Speaker { get; }

        /// <summary>応えた指示。</summary>
        public BotErrandKind Order { get; }

        /// <summary>狙う指示の敵を片付けて戻るときの応答か。false なら指示を受けたときの応答。</summary>
        public bool IsTargetDown { get; }

        /// <summary>speaker が order の指示を受けたときの応答。</summary>
        /// <exception cref="ArgumentNullException">speaker が null。</exception>
        /// <exception cref="ArgumentOutOfRangeException">order が指示 (<see cref="BotErrand.IsOrderKind"/>) でない。</exception>
        public static SquadOrderReply Accepted(string speaker, BotErrandKind order) => new SquadOrderReply(speaker, order, false);

        /// <summary>speaker が狙う指示の敵を片付けて戻るときの応答。</summary>
        /// <exception cref="ArgumentNullException">speaker が null。</exception>
        public static SquadOrderReply TargetDown(string speaker) => new SquadOrderReply(speaker, BotErrandKind.Attack, true);

        public override string ToString() => $"{Speaker} {Order}{(IsTargetDown ? " targetDown" : string.Empty)}";
    }
}
