using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    /// <summary>
    /// ゲームごとに変える調整値のうち、JSON (<see cref="TuningJson"/> の形) から読むもの。入口 (<see cref="GameContent"/>) に JSON があればそれを、無ければ Resources の JSON を読む。
    /// <list type="bullet">
    /// <item>グレネード: <see cref="GrenadeResourcePath"/> (<see cref="Grenades"/>)。オフラインの <see cref="GrenadeThrower"/> とネット対戦が使う。</item>
    /// <item>印: <see cref="PingResourcePath"/> (<see cref="Ping"/>)。オフラインの <see cref="PingController"/> とネット対戦が使う。</item>
    /// <item>吹き飛ばし: <see cref="KnockbackResourcePath"/> (<see cref="Knockback"/>)。プレイヤーの <see cref="KnockbackMovement"/> と <see cref="EnemyAgent"/> が使う。</item>
    /// <item>持ち物: <see cref="InventoryResourcePath"/> (<see cref="Items"/>、<see cref="InventoryJson"/> の形)。オフラインの <see cref="PlayerInventory"/> とネット対戦が使う。</item>
    /// <item>ボット: <see cref="BotsResourcePath"/> (<see cref="Bots"/>、サーバーの bots.json と同じ形)。</item>
    /// <item>制圧: <see cref="SuppressionResourcePath"/> (<see cref="Suppression"/>、<see cref="SuppressionJson"/> の形)。<see cref="SuppressionActionInstaller"/> がプレイヤーと敵に使う。</item>
    /// <item>アシスト: <see cref="AssistResourcePath"/> (<see cref="Assist"/>、<see cref="AssistJson"/> の形)。<see cref="OfflineAssists"/> が使う。</item>
    /// </list>
    /// ほかの調整値も <see cref="Load{T}"/> に入口の欄と Resources のパスを渡せば同じ規則で読める。
    /// ファイルが無ければ Spec の既定値 (<see cref="GrenadeCatalog.Default"/>・<see cref="PingSpec.Default"/>・<see cref="KnockbackSpec.Default"/>・
    /// <see cref="ItemCatalog.Empty"/>・<see cref="NetBotSpec.Default"/>・<see cref="SuppressionSpec.Default"/>・<see cref="AssistSpec.Default"/>) で動く。
    /// 壊れている・値が不正なら、ファイル名と項目名を添えたエラーを出して既定値で続ける。
    /// 初めて使うときに読み、Play の間は持ち続ける (Play の開始時に読み直す)。
    /// </summary>
    public static class KitTuning
    {
        public const string LogTag = "[Tuning]";

        /// <summary>グレネードの調整値を置く Resources のパス (拡張子なし)。</summary>
        public const string GrenadeResourcePath = "Tuning/grenade";

        /// <summary>印の調整値を置く Resources のパス (拡張子なし)。</summary>
        public const string PingResourcePath = "Tuning/ping";

        /// <summary>吹き飛ばしの調整値を置く Resources のパス (拡張子なし)。</summary>
        public const string KnockbackResourcePath = "Tuning/knockback";

        public const string InventoryResourcePath = "Tuning/inventory";

        public const string BotsResourcePath = "Tuning/bots";

        public const string SuppressionResourcePath = "Tuning/suppression";

        public const string AssistResourcePath = "Tuning/assist";

        // ドメインの再読み込みを切っているので、Play の開始時に ResetOnPlay で戻す。
        private static readonly Dictionary<string, object> _loaded = new Dictionary<string, object>(StringComparer.Ordinal);

        /// <summary>グレネードの種類の調整値。<see cref="GrenadeResourcePath"/> が無いか読めなければ <see cref="GrenadeCatalog.Default"/>。</summary>
        public static GrenadeCatalog Grenades => Load(c => c.GrenadeTuning, GrenadeResourcePath, TuningJson.ParseGrenades, GrenadeCatalog.Default);

        /// <summary>印の調整値。<see cref="PingResourcePath"/> が無いか読めなければ <see cref="PingSpec.Default"/>。</summary>
        public static PingSpec Ping => Load(c => c.PingTuning, PingResourcePath, TuningJson.ParsePing, PingSpec.Default);

        /// <summary>吹き飛ばしの調整値。<see cref="KnockbackResourcePath"/> が無いか読めなければ <see cref="KnockbackSpec.Default"/>。</summary>
        public static KnockbackSpec Knockback => Load(c => c.KnockbackTuning, KnockbackResourcePath, TuningJson.ParseKnockback, KnockbackSpec.Default);

        public static ItemCatalog Items => Load(c => c.InventoryTuning, InventoryResourcePath, InventoryJson.Parse, ItemCatalog.Empty);

        public static NetBotSpec Bots => Load(c => c.BotsTuning, BotsResourcePath, TuningJson.ParseBots, NetBotSpec.Default);

        public static SuppressionSpec Suppression => Load(c => c.SuppressionTuning, SuppressionResourcePath, SuppressionJson.Parse, SuppressionSpec.Default);

        public static AssistSpec Assist => Load(c => c.AssistTuning, AssistResourcePath, AssistJson.Parse, AssistSpec.Default);

        /// <summary>読んだ値を捨て、次に使うときに入口か Resources から読み直させる。</summary>
        public static void Reload()
        {
            _loaded.Clear();
        }

        /// <summary>
        /// text を parse で読む。text が null (ファイルが無い) なら fallback を返す。
        /// 読めなければ sourceName と項目名を添えたエラーを出して fallback を返す。Resources を使わずに試すときに呼ぶ。
        /// </summary>
        /// <exception cref="ArgumentNullException">parse か fallback が null。</exception>
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

        /// <summary>
        /// 入口 (<see cref="GameContent.Current"/>) の select の欄の JSON を、それが空なら <c>Resources/&lt;path&gt;.json</c> を parse で読む。
        /// どちらも無ければ fallback。壊れていればエラーを出して fallback (<see cref="Read{T}"/>)。
        /// 結果は path ごとに覚え、<see cref="Reload"/> (入口を替えたときと Play の開始時) まで読み直さない。
        /// </summary>
        /// <exception cref="ArgumentNullException">select・parse・fallback が null。</exception>
        /// <exception cref="ArgumentException">path が空。</exception>
        /// <exception cref="InvalidOperationException">同じ path を別の型で読んだ。</exception>
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
