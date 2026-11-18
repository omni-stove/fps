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

        public BlockListStore Blocks { get; set; } = new BlockListStore();

        // null ならオフラインと同じ Resources の調整値 (melee・streaks の JSON) を使う
        public MeleeSpec Melee { get; set; }

        public StreakSpec Streaks { get; set; }

        // ロードアウトの能力の並び。置く物の能力の見た目と、Ability1・Ability2 で置く種類をここから引く
        public IReadOnlyList<AbilityDefinition> Abilities { get; set; } = Array.Empty<AbilityDefinition>();

        public float KillCamSpeed { get; set; } = 1f;
    }

    public static class NetV2ClientParts
    {
        public static List<INetV2ClientPart> Create(NetV2ClientPartsContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var client = context.Client;
            var blocks = new NetV2BlockPart(
                client, context.VoiceRoster, context.Blocks ?? new BlockListStore(), context.Voice == null, context.Log);
            var deployables = new NetV2DeployablesPart(context.Send, () => client.PlayerId, DeployDefinitionsOf(context.Abilities), context.Log);
            var parts = new List<INetV2ClientPart>
            {
                blocks,
                new NetV2MinimapPart(() => client.PlayerId, context.Pings, context.Minimap, context.ControlPointPosition),
                new NetV2ChatPart(context.Send, ClientChat.DefaultCapacity, context.Log, blocks.Blocks),
                new NetV2MapVotePart(context.Send, context.Log, context.MapDecided),
                new NetV2SpectatePart(client, context.Send, context.LocalPlayer),

                // ストリークと近接の規則はスナップショットの MatchRulesSync で届くので、最後に届いた値で決める
                new NetV2StreakPart(client, context.Send, () => StreaksRule(client), context.Log, context.Streaks, context.LocalPlayer),
                new NetV2MeleePart(client, context.Melee, context.LocalPlayer, context.Log),
                deployables,
                new NetV2DeployInputPart(
                    deployables, NetV2DeployInputPart.SlotsOf(context.Abilities), () => CanAct(client), context.LocalPlayer),
                new NetV2KillCamPart(client, context.KillCamSpeed, context.LocalPlayer, context.Log),
            };

            if (context.Voice != null)
            {
                parts.Add(new NetV2VoicePart(
                    context.Client,
                    context.Voice,
                    context.VoiceLobbyId,
                    context.VoiceRoster,
                    context.VoiceSettings,
                    context.Log,
                    context.PushToTalkHeld,
                    blocks.Blocks));
            }

            return parts;
        }

        public static Streaks StreaksRule(MatchClient client)
        {
            var latest = client?.LatestSnapshot;
            return latest != null ? latest.Match.Rules.Streaks : Streaks.Off;
        }

        // 置く要求はサーバーが確かめ直すので、ここでは試合中に自分が立っているときだけ送り、倒れている間の空の要求で拒まれるログを増やさない
        public static bool CanAct(MatchClient client)
        {
            var latest = client?.LatestSnapshot;
            if (latest == null || latest.Match.State != GameModeState.Playing)
            {
                return false;
            }

            var own = latest.Players.IndexOf(client.PlayerId);
            if (own < 0)
            {
                return false;
            }

            var row = latest.Players[own];
            return row.Health > 0 && !row.Downed.IsDowned;
        }

        private static List<DeployAbilityDefinition> DeployDefinitionsOf(IReadOnlyList<AbilityDefinition> abilities)
        {
            var definitions = new List<DeployAbilityDefinition>();
            if (abilities == null)
            {
                return definitions;
            }

            foreach (var ability in abilities)
            {
                if (ability is DeployAbilityDefinition deploy)
                {
                    definitions.Add(deploy);
                }
            }

            return definitions;
        }
    }
}
