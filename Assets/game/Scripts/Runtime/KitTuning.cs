using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public static class KitTuning
    {
        public const string LogTag = "[Tuning]";

        public const string GrenadeResourcePath = "Tuning/grenade";

        public const string PingResourcePath = "Tuning/ping";

        public const string KnockbackResourcePath = "Tuning/knockback";

        public const string InventoryResourcePath = "Tuning/inventory";

        public const string BotsResourcePath = "Tuning/bots";

        public const string SuppressionResourcePath = "Tuning/suppression";

        public const string AssistResourcePath = "Tuning/assist";

        public const string PayloadResourcePath = PayloadJson.ResourcePath;

        public const string EconomyResourcePath = EconomyJson.ResourcePath;

        public const string SupplyDropsResourcePath = "Tuning/supply-drops";

        public const string RedeployResourcePath = RedeployJson.ResourcePath;

        public const string MutatorsResourcePath = "Tuning/mutators";

        public const string PlaylistsResourcePath = "Tuning/playlists";

        public const string StealthResourcePath = "Tuning/stealth";

        public const string KeySharingResourcePath = KeySharingJson.ResourcePath;

        public const string ProgressionResourcePath = "Tuning/progression";

        public const string SkillResourcePath = "Tuning/skill";

        public const string AchievementsResourcePath = "Tuning/achievements";

        public const string NameplatesResourcePath = NameplateJson.ResourcePath;

        // ドメインの再読み込みを切っているので、Play の開始時に ResetOnPlay で戻す。
        private static readonly Dictionary<string, object> _loaded = new Dictionary<string, object>(StringComparer.Ordinal);

        public static GrenadeCatalog Grenades => Load(c => c.GrenadeTuning, GrenadeResourcePath, TuningJson.ParseGrenades, GrenadeCatalog.Default);

        public static PingSpec Ping => Load(c => c.PingTuning, PingResourcePath, TuningJson.ParsePing, PingSpec.Default);

        public static KnockbackSpec Knockback => Load(c => c.KnockbackTuning, KnockbackResourcePath, TuningJson.ParseKnockback, KnockbackSpec.Default);

        public static ItemCatalog Items => Load(c => c.InventoryTuning, InventoryResourcePath, InventoryJson.Parse, ItemCatalog.Empty);

        public static NetBotSpec Bots => Load(c => c.BotsTuning, BotsResourcePath, TuningJson.ParseBots, NetBotSpec.Default);

        public static SuppressionSpec Suppression => Load(c => c.SuppressionTuning, SuppressionResourcePath, SuppressionJson.Parse, SuppressionSpec.Default);

        public static AssistSpec Assist => Load(c => c.AssistTuning, AssistResourcePath, AssistJson.Parse, AssistSpec.Default);

        public static LeaderboardSpec Leaderboards =>
            Load(c => c.LeaderboardsTuning, LeaderboardJson.ResourcePath, LeaderboardJson.Parse, LeaderboardSpec.Default);

        public static ReplaySpec Replay => Load(c => c.ReplayTuning, ReplayJson.ResourcePath, ReplayJson.Parse, ReplaySpec.Default);

        public static IReadOnlyList<EmoteSpec> Emotes => Load(c => c.EmotesTuning, EmoteJson.ResourcePath, EmoteJson.Parse, EmoteSpec.DefaultEmotes);

        public static InfectedSpec Infected => Load(c => c.InfectedTuning, InfectedJson.ResourcePath, InfectedJson.Parse, InfectedSpec.Default);

        public static EconomyRules Economy => Load(c => c.EconomyTuning, EconomyResourcePath, EconomyJson.Parse, EconomyRules.Default);

        public static PayloadSpec Payload => Load(c => c.PayloadTuning, PayloadResourcePath, PayloadJson.Parse, PayloadSpec.Default);

        public static IReadOnlyList<GameRulesMutator> Mutators =>
            Load(c => c.MutatorsTuning, MutatorsResourcePath, MutatorJson.Parse, MutatorJson.Empty);

        public static IReadOnlyList<Playlist> Playlists =>
            Load(c => c.PlaylistsTuning, PlaylistsResourcePath, PlaylistJson.Parse, PlaylistJson.Empty);

        public static KeySharingSpec KeySharing =>
            Load(c => c.KeySharingTuning, KeySharingResourcePath, KeySharingJson.Parse, KeySharingSpec.Default);

        public static NameplateSpec Nameplates =>
            Load(c => c.NameplatesTuning, NameplatesResourcePath, NameplateJson.Parse, NameplateSpec.Default);

        public static void Reload()
        {
            _loaded.Clear();
        }

        public static T Read<T>(string text, string sourceName, Func<string, string, T> parse, T fallback)
            where T : class
        {
            if (parse == null)
            {
                throw new ArgumentNullException(nameof(parse));
            }

            if (fallback == null)
            {
                throw new ArgumentNullException(nameof(fallback));
            }

            if (text == null)
            {
                return fallback;
            }

            try
            {
                return parse(text, sourceName);
            }
            catch (TuningJsonException e)
            {
                Debug.LogError($"{LogTag} {e.Message}; using the default values instead");
                return fallback;
            }
        }

        public static T Load<T>(Func<GameContent, TextAsset> select, string path, Func<string, string, T> parse, T fallback)
            where T : class
        {
            if (select == null)
            {
                throw new ArgumentNullException(nameof(select));
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("a Resources path such as Tuning/grenade is required", nameof(path));
            }

            if (parse == null)
            {
                throw new ArgumentNullException(nameof(parse));
            }

            if (fallback == null)
            {
                throw new ArgumentNullException(nameof(fallback));
            }

            if (_loaded.TryGetValue(path, out var cached))
            {
                return cached as T
                    ?? throw new InvalidOperationException($"{LogTag} '{path}' is already read as {cached.GetType().Name}, not {typeof(T).Name}");
            }

            var spec = LoadUncached(select, path, parse, fallback);
            _loaded[path] = spec;
            return spec;
        }

        // サーバーへの書き出しが Load と違う順で読まないよう、parse の前の引き先だけを分けて出す
        public static string FindText(Func<GameContent, TextAsset> select, string path, out string sourceName)
        {
            if (select == null)
            {
                throw new ArgumentNullException(nameof(select));
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("a Resources path such as Tuning/grenade is required", nameof(path));
            }

            var fromContent = GameContent.Pick(select);
            if (fromContent != null)
            {
                sourceName = $"GameContent '{GameContent.Current.name}' ({fromContent.name})";
                return fromContent.text;
            }

            sourceName = $"Resources/{path}.json";
            var asset = Resources.Load<TextAsset>(path);
            if (asset == null)
            {
                return null;
            }

            var text = asset.text;
            Resources.UnloadAsset(asset);
            return text;
        }

        private static T LoadUncached<T>(Func<GameContent, TextAsset> select, string path, Func<string, string, T> parse, T fallback)
            where T : class
        {
            var text = FindText(select, path, out var source);
            if (text != null)
            {
                Debug.Log($"{LogTag} read {source}");
            }

            return Read(text, source, parse, fallback);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            Reload();
        }
    }
}
