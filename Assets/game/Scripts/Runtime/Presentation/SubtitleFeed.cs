using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 音が聞こえる人と同じ合図だけを字幕にするため、素材が無くて鳴らなかった音は流さない。
    // 向きと距離は聞いている人 (カメラ) によって変わるので、ここでは鳴った位置だけを渡し、受け取った字幕の側で決める。
    // 購読者が例外を投げても音の処理を止めないよう、ログに出して残りの購読者を呼ぶ。
    // 静的なので、ドメインの再読み込みを切った Play でも前回の購読が残らないよう、Play の開始時に購読をすべて外す。
    public static class SubtitleFeed
    {
        public static event Action<SoundCueSource> CuePlayed;

        public static event Action<SubtitleSpeech> Spoken;

        public static event Action<SquadOrderReply> SquadReplied;

        // 声の素材が無くても字幕を出すのは、話した内容は音の合図と違い、字幕でしか伝わらないことがあるため。
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

        // 声の素材が無くても知らせるのは Say と同じ理由。
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

    public readonly struct SoundCueSource
    {
        public SoundCueSource(SoundCueKind kind, Vector3? position, float audibleDistance)
        {
            Kind = kind;
            Position = position;
            AudibleDistance = audibleDistance;
        }

        public SoundCueKind Kind { get; }

        public Vector3? Position { get; }

        public float AudibleDistance { get; }
    }

    public readonly struct SubtitleSpeech
    {
        public SubtitleSpeech(string speaker, string text)
        {
            Speaker = speaker;
            Text = text;
        }

        public string Speaker { get; }

        public string Text { get; }
    }

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

        public string Speaker { get; }

        public BotErrandKind Order { get; }

        public bool IsTargetDown { get; }

        public static SquadOrderReply Accepted(string speaker, BotErrandKind order) => new SquadOrderReply(speaker, order, false);

        public static SquadOrderReply TargetDown(string speaker) => new SquadOrderReply(speaker, BotErrandKind.Attack, true);

        public override string ToString() => $"{Speaker} {Order}{(IsTargetDown ? " targetDown" : string.Empty)}";
    }
}
