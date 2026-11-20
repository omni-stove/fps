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

        // ロードアウトの能力の並び。つないだ直後の Ability1・Ability2 で置く種類をここから引く
        public IReadOnlyList<AbilityDefinition> Abilities { get; set; } = Array.Empty<AbilityDefinition>();

        // 装備画面で選べる能力の一覧。誰かが選んで置いた物の見た目と、受け入れられた選択の枠の定義をここから引く。null なら能力を選ばせないゲーム
        public AbilityCatalogAsset AbilityCatalog { get; set; }

        // 受け入れられた能力の選択を出す側。null なら枠はロードアウトの能力のまま
        internal NetV2LoadoutChooser Loadout { get; set; }

        public float KillCamSpeed { get; set; } = 1f;

        // 回線の値 (GameSignals.NetStats) を出し直す間隔 (秒)
        public float NetStatsIntervalSeconds { get; set; } = NetV2NetStatsPart.DefaultIntervalSeconds;

        // ファイルから再生する (ReplayTransport) か。再生では通報を送る相手がいないので通報の口を出さず、見方の部品を足す
        public bool Replay { get; set; }

        // 再生の自由なカメラの速さ (m/s)。プレイヤーは後から現れるので毎回読む。null なら既定
        public Func<float> ReplayFreeSpeed { get; set; }

        // null なら天候の部品がシーンの WeatherController を探す
        public Func<WeatherController> Weather { get; set; }

        // 箱・物資投下・倒れた人の箱・脱出地点の差し替えの見た目。null なら NetV2LootPart がコードで形を作る
        public NetV2LootVisuals LootVisuals { get; set; }

        // null ならエリアの部品がシーンの ShrinkingZone を探して見た目を引き継ぐ
        public Func<ShrinkingZone> ZoneLook { get; set; }
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
            var deployables = new NetV2DeployablesPart(
                context.Send, () => client.PlayerId, DeployDefinitionsOf(context.Abilities, context.AbilityCatalog), context.Log);
            var deployInput = new NetV2DeployInputPart(
                deployables, NetV2DeployInputPart.SlotsOf(context.Abilities), () => CanAct(client), context.LocalPlayer);
            if (context.Loadout != null)
            {
                deployInput.Follow(context.Loadout, context.AbilityCatalog, context.Log);
            }

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
                deployInput,
                new NetV2KillCamPart(client, context.KillCamSpeed, context.LocalPlayer, context.Log),

                // 天候と時刻もスナップショットの MatchRulesSync で届くので、マップを替えるたびにシーンの空へ当て直す
                new NetV2WeatherPart(context.Weather, context.Log),

                // 箱とエリアは、スナップショットに箱・脱出地点・エリアの欄が無ければ何も出さないので、目的で分けずに並べる。
                // 再生では開ける要求を受ける相手がいないので、箱は見せるだけにする
                new NetV2LootPart(
                    context.Send,
                    () => !context.Replay && CanAct(client),
                    context.LootVisuals,
                    () => LocalPositionOf(context.LocalPlayer),
                    log: context.Log),
                new NetV2ZonePart(context.ZoneLook),
            };

            if (context.Replay)
            {
                parts.Add(new NetV2ReplayViewPart(client, context.ReplayFreeSpeed ?? (() => DeathCamFraming.DefaultFreeSpeed)));
            }
            else
            {
                // 通報と同じく、投票と購入も送る相手 (サーバー) のいない再生では口を出さない
                parts.Add(new NetV2ReportPart(context.Send, context.Log));
                parts.Add(new NetV2VoteKickPart(context.Send, context.Log));
                parts.Add(new NetV2BuyPart(context.Send, context.Log));

                // 再生の通信路はファイルを読むだけで Ping もロスも無いので、回線の値は試合中だけ出す
                parts.Add(new NetV2NetStatsPart(client, context.NetStatsIntervalSeconds));
            }

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

        // 破棄済みの Unity の参照も無いものとして扱うため、?. ではなく != null で確かめる
        private static UnityEngine.Vector3? LocalPositionOf(Func<NetV2LocalPlayer> localPlayer)
        {
            var local = localPlayer?.Invoke();
            return local != null ? local.transform.position : (UnityEngine.Vector3?)null;
        }

        // 他の人はカタログのどの置く物を選んでいてもよいので、ロードアウトの能力だけでなくカタログ全部の置く物を渡す。
        // 同じ Id はロードアウトのものを先に置き、NetV2DeployablesPart が最初の 1 つだけを使う
        internal static List<DeployAbilityDefinition> DeployDefinitionsOf(IReadOnlyList<AbilityDefinition> abilities, AbilityCatalogAsset catalog)
        {
            var definitions = new List<DeployAbilityDefinition>();
            AddDeploys(definitions, abilities);
            if (catalog != null)
            {
                AddDeploys(definitions, catalog.Abilities);
            }

            return definitions;
        }

        private static void AddDeploys(List<DeployAbilityDefinition> definitions, IReadOnlyList<AbilityDefinition> abilities)
        {
            if (abilities == null)
            {
                return;
            }

            foreach (var ability in abilities)
            {
                if (ability is DeployAbilityDefinition deploy)
                {
                    definitions.Add(deploy);
                }
            }
        }
    }
}
