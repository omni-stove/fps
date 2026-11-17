using System;
using System.Collections.Generic;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;
using Shooter.Runtime;

namespace Shooter.NetV2
{
    public sealed class NetV2ClientPartsContext
    {
        public NetV2ClientPartsContext(MatchClient client, Func<IMessage, bool> send, Action<string> log)
        {
            Client = client ?? throw new ArgumentNullException(nameof(client));
            Send = send ?? throw new ArgumentNullException(nameof(send));
            Log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public MatchClient Client { get; }

        public Func<IMessage, bool> Send { get; }

        public Action<string> Log { get; }

        public SharedPingMarkers Pings { get; set; } = new SharedPingMarkers();

        public MinimapSpec Minimap { get; set; } = MinimapSpec.Default;

        public Func<int, Vector3?> ControlPointPosition { get; set; } = _ => null;

        public Action<string> MapDecided { get; set; }

        public Func<NetV2LocalPlayer> LocalPlayer { get; set; }

        public IVoiceSession Voice { get; set; }

        public string VoiceLobbyId { get; set; }

        public VoiceRoster VoiceRoster { get; set; } = new VoiceRoster();

        public VoiceSettingsStore VoiceSettings { get; set; } = new VoiceSettingsStore();

        public Func<bool> PushToTalkHeld { get; set; }
    }

    public static class NetV2ClientParts
    {
        public static List<INetV2ClientPart> Create(NetV2ClientPartsContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var parts = new List<INetV2ClientPart>
            {
                new NetV2MinimapPart(() => context.Client.PlayerId, context.Pings, context.Minimap, context.ControlPointPosition),
                new NetV2ChatPart(context.Send, ClientChat.DefaultCapacity, context.Log),
                new NetV2MapVotePart(context.Send, context.Log, context.MapDecided),
                new NetV2SpectatePart(context.Client, context.Send, context.LocalPlayer),
            };

            if (context.Voice != null)
            {
                parts.Add(new NetV2VoicePart(
                    context.Client, context.Voice, context.VoiceLobbyId, context.VoiceRoster, context.VoiceSettings, context.Log, context.PushToTalkHeld));
            }

            return parts;
        }
    }
}
