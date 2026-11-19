using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Shooter.Core;

namespace Shooter.Server
{
    internal sealed class ServerRulesData
    {
        public const string RulesDirectoryName = "rules";
        public const string TuningDirectoryName = "tuning";
        public const string PingTuningFileName = "ping.json";
        public const string GrenadeTuningFileName = "grenade.json";
        public const string BotsTuningFileName = "bots.json";
        public const string InventoryTuningFileName = "inventory.json";
        public const string KnockbackTuningFileName = "knockback.json";
        public const string AssistTuningFileName = "assist.json";
        public const string ChatWordsTuningFileName = "chat-words.json";

        // 財布の額は試合の設定 (match.economy) から読み、ここには買える品と値段の一覧を置く
        public const string EconomyTuningFileName = "economy.json";

        // 箱の中身を引く表。Unity の Resources (Tuning/loot) と同じ書き方で、表の Id はマップの地点ファイルの箱が引く
        public const string LootTuningFileName = "loot.json";

        // プレイリストはルールの並びなので、rules と並べて server-data の下に置く
        public const string PlaylistsDirectoryName = "playlists";

        private readonly NetMatchSettings _settings;

        public ServerRulesData(
            string name, NetMatchFile file, PingSpec ping, string path, string pingPath, GrenadeCatalog grenades = null, string grenadePath = null,
            NetBotSpec bots = null, string botsPath = null, ItemCatalog items = null, string itemsPath = null,
            KnockbackSpec knockback = null, string knockbackPath = null, AssistSpec assist = null, string assistPath = null,
            NetMatchSettings settings = null, ChatWordFilterSpec chatWords = null, string chatWordsPath = null,
            EconomyRules buyList = null, string economyPath = null, LootSpec loot = null, string lootPath = null)
        {
            _settings = settings;
            Loot = loot ?? LootSpec.Default;
            LootPath = lootPath;
            BuyList = buyList;
            EconomyPath = economyPath;
            ChatWords = chatWords ?? ChatWordFilterSpec.Default;
            ChatWordsPath = chatWordsPath;
            Assist = assist ?? AssistSpec.Default;
            AssistPath = assistPath;
            Knockback = knockback ?? KnockbackSpec.Default;
            KnockbackPath = knockbackPath;
            Items = items ?? ItemCatalog.Empty;
            ItemsPath = itemsPath;
            Bots = bots ?? NetBotSpec.Default;
            BotsPath = botsPath;
            Grenades = grenades ?? GrenadeCatalog.Default;
            GrenadePath = grenadePath;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            File = file ?? throw new ArgumentNullException(nameof(file));
            Ping = ping ?? throw new ArgumentNullException(nameof(ping));
            Path = path ?? "";
            PingPath = pingPath;
        }

        public string Name { get; }

        public NetMatchFile File { get; }

        public GameRules Rules => File.Rules;

        public NetMatchSettings Settings => _settings ?? File.Settings;

        // 移動のモジュール (#586) がクライアントの予測と同じ値で動かすために持つ。
        public NetMotorSettings Motor => File.Motor;

        public PingSpec Ping { get; }

        public string Path { get; }

        public string PingPath { get; }

        public GrenadeCatalog Grenades { get; }

        public string GrenadePath { get; }

        public NetBotSpec Bots { get; }

        public ItemCatalog Items { get; }

        public string ItemsPath { get; }

        public string BotsPath { get; }

        public KnockbackSpec Knockback { get; }

        public string KnockbackPath { get; }

        public AssistSpec Assist { get; }

        public string AssistPath { get; }

        public ChatWordFilterSpec ChatWords { get; }

        public string ChatWordsPath { get; }

        // 買える品の一覧 (tuning/economy.json) を読んだもの。財布の額 (Spec) は使わず、置いていなければ null で、経済を動かさない
        public EconomyRules BuyList { get; }

        public string EconomyPath { get; }

        // 置いていなければ Core の既定 (表なし) で、箱は空のまま開く
        public LootSpec Loot { get; }

        public string LootPath { get; }

        // 額は試合の設定から取るので、設定を差し替えた (WithSettings) 後もその額で組む
        public EconomyRules Economy => BuyList == null ? null : new EconomyRules(Settings.Economy, BuyList.Catalog, BuyList.ResetOnSwap);

        public ServerRulesData WithSettings(NetMatchSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            return new ServerRulesData(
                Name, File, Ping, Path, PingPath, Grenades, GrenadePath, Bots, BotsPath, Items, ItemsPath, Knockback, KnockbackPath, Assist, AssistPath,
                settings, ChatWords, ChatWordsPath, BuyList, EconomyPath, Loot, LootPath);
        }

        // ルールのファイルを作り直すのは、NetMatchFile が目的に要る設定とロードアウトを作るときに確かめるので、
        // 次の試合で使えないルールを受け付けた後で試合の合間に止まらないようにするため。ファイルを読み直さないのは、調整値を試合の途中で替えないため
        public ServerRulesData WithRules(GameRules rules)
        {
            ArgumentNullException.ThrowIfNull(rules);
            var file = new NetMatchFile(
                File.Name, rules, File.Settings, File.LoadoutName, File.LoadoutRule, File.LoadoutWeapons, File.SwitchSeconds, File.CatalogName,
                File.Catalog, File.Motor);
            return new ServerRulesData(
                Name, file, Ping, Path, PingPath, Grenades, GrenadePath, Bots, BotsPath, Items, ItemsPath, Knockback, KnockbackPath, Assist, AssistPath,
                _settings, ChatWords, ChatWordsPath, BuyList, EconomyPath, Loot, LootPath);
        }

        public string LogLine => ServerProgram.LogTag + " rules " + Name
            + " objective=" + Rules.Objective
            + " weapons=" + File.LoadoutWeapons.Count.ToString(CultureInfo.InvariantCulture)
            + " from=" + Path
            + " ping=" + (PingPath ?? "default")
            + " grenade=" + (GrenadePath ?? "default")
            + " bots=" + (BotsPath ?? "default")
            + " inventory=" + (ItemsPath ?? "default")
            + " knockback=" + (KnockbackPath ?? "default")
            + " assist=" + (AssistPath ?? "default")
            + " chatWords=" + (ChatWordsPath ?? "default")
            + " economy=" + (EconomyPath ?? "none")
            + " loot=" + (LootPath ?? "default")
            + " walk=" + Motor.Motor.WalkSpeed.ToString(CultureInfo.InvariantCulture)
            + " sprint=" + Motor.Motor.SprintSpeed.ToString(CultureInfo.InvariantCulture)
            + " height=" + Motor.Stance.Standing.Height.ToString(CultureInfo.InvariantCulture)
            + " crouchHeight=" + Motor.Stance.Crouched.Height.ToString(CultureInfo.InvariantCulture)
            + " radius=" + Motor.Stance.Radius.ToString(CultureInfo.InvariantCulture)
            + " weather=" + (Settings.WeatherId ?? "default")
            + " timeOfDay=" + (Settings.TimeOfDayId ?? "default");

        public static bool TryLoad(string name, IReadOnlyList<string> searchRoots, out ServerRulesData data, out string error)
        {
            ArgumentNullException.ThrowIfNull(searchRoots);

            data = null;
            if (!NetLaunchOptions.IsMapName(name))
            {
                error = "rules name '" + name + "' is not a name of letters, digits, '_' or '-'";
                return false;
            }

            var fileName = name + NetMatchFile.Extension;
            string root = null;
            var looked = new List<string>();
            foreach (var candidate in searchRoots)
            {
                var directory = System.IO.Path.GetFullPath(System.IO.Path.Combine(candidate, ServerMapData.DataDirectoryName, RulesDirectoryName));
                looked.Add(directory);
                if (System.IO.File.Exists(System.IO.Path.Combine(directory, fileName)))
                {
                    root = candidate;
                    break;
                }
            }

            if (root == null)
            {
                error = "rules " + name + " not found: " + fileName + " is not in " + string.Join(" or ", looked)
                    + "; export it from Unity with make export-server-rules";
                return false;
            }

            var path = System.IO.Path.GetFullPath(System.IO.Path.Combine(root, ServerMapData.DataDirectoryName, RulesDirectoryName, fileName));
            NetMatchFile file;
            try
            {
                file = NetMatchFile.Parse(System.IO.File.ReadAllText(path), path);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ServerDataJsonException || e is GameRulesJsonException)
            {
                error = "cannot read rules " + name + ": " + e.Message;
                return false;
            }

            // 印の調整値は Unity では Resources の JSON から読むので、無ければ同じく既定値で動かす
            var pingPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(root, ServerMapData.DataDirectoryName, TuningDirectoryName, PingTuningFileName));
            var ping = PingSpec.Default;
            if (System.IO.File.Exists(pingPath))
            {
                try
                {
                    ping = TuningJson.ParsePing(System.IO.File.ReadAllText(pingPath), pingPath);
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is TuningJsonException)
                {
                    error = "cannot read ping tuning: " + e.Message;
                    return false;
                }
            }
            else
            {
                pingPath = null;
            }

            // グレネードも Unity では Resources の JSON (KitTuning.Grenades) から読むので、無ければ同じく既定値で動かす
            var grenadePath = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(root, ServerMapData.DataDirectoryName, TuningDirectoryName, GrenadeTuningFileName));
            var grenades = GrenadeCatalog.Default;
            if (System.IO.File.Exists(grenadePath))
            {
                try
                {
                    grenades = TuningJson.ParseGrenades(System.IO.File.ReadAllText(grenadePath), grenadePath);
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is TuningJsonException)
                {
                    error = "cannot read grenade tuning: " + e.Message;
                    return false;
                }
            }
            else
            {
                grenadePath = null;
            }

            // Bot の判断と入力の調整値は Unity のサーバーではコードの定数だったので、server-data にだけ置き、無ければその値で動かす
            var botsPath = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(root, ServerMapData.DataDirectoryName, TuningDirectoryName, BotsTuningFileName));
            var bots = NetBotSpec.Default;
            if (System.IO.File.Exists(botsPath))
            {
                try
                {
                    bots = TuningJson.ParseBots(System.IO.File.ReadAllText(botsPath), botsPath);
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is TuningJsonException)
                {
                    error = "cannot read bots tuning: " + e.Message;
                    return false;
                }
            }
            else
            {
                botsPath = null;
            }

            // 持ち物も Unity では Resources の JSON (KitTuning.Items) から読むので、無ければ同じくアイテムなしで動かす
            var itemsPath = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(root, ServerMapData.DataDirectoryName, TuningDirectoryName, InventoryTuningFileName));
            var items = ItemCatalog.Empty;
            if (System.IO.File.Exists(itemsPath))
            {
                try
                {
                    items = InventoryJson.Parse(System.IO.File.ReadAllText(itemsPath), itemsPath);
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is TuningJsonException)
                {
                    error = "cannot read inventory tuning: " + e.Message;
                    return false;
                }
            }
            else
            {
                itemsPath = null;
            }

            // 吹き飛ばしも Unity では Resources の JSON (KitTuning.Knockback) から読むので、無ければ同じく既定値で動かす
            var knockbackPath = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(root, ServerMapData.DataDirectoryName, TuningDirectoryName, KnockbackTuningFileName));
            var knockback = KnockbackSpec.Default;
            if (System.IO.File.Exists(knockbackPath))
            {
                try
                {
                    knockback = TuningJson.ParseKnockback(System.IO.File.ReadAllText(knockbackPath), knockbackPath);
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is TuningJsonException)
                {
                    error = "cannot read knockback tuning: " + e.Message;
                    return false;
                }
            }
            else
            {
                knockbackPath = null;
            }

            // アシストも Unity では Resources の JSON (KitTuning.Assist) から読むので、無ければ同じく既定値で動かす
            var assistPath = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(root, ServerMapData.DataDirectoryName, TuningDirectoryName, AssistTuningFileName));
            var assist = AssistSpec.Default;
            if (System.IO.File.Exists(assistPath))
            {
                try
                {
                    assist = AssistJson.Parse(System.IO.File.ReadAllText(assistPath), assistPath);
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is TuningJsonException)
                {
                    error = "cannot read assist tuning: " + e.Message;
                    return false;
                }
            }
            else
            {
                assistPath = null;
            }

            // NG ワードも Unity では Resources の JSON (Tuning/chat-words) から読むので、無ければ同じく語なしで動かす
            var chatWordsPath = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(root, ServerMapData.DataDirectoryName, TuningDirectoryName, ChatWordsTuningFileName));
            var chatWords = ChatWordFilterSpec.Default;
            if (System.IO.File.Exists(chatWordsPath))
            {
                try
                {
                    chatWords = ChatWordFilterJson.Parse(System.IO.File.ReadAllText(chatWordsPath), chatWordsPath);
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is TuningJsonException)
                {
                    error = "cannot read chat word tuning: " + e.Message;
                    return false;
                }
            }
            else
            {
                chatWordsPath = null;
            }

            // 買える品の一覧は Unity では Resources の JSON (KitTuning.Economy) から読む。無ければ既定の品で動かさず、経済そのものを止める
            var economyPath = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(root, ServerMapData.DataDirectoryName, TuningDirectoryName, EconomyTuningFileName));
            EconomyRules buyList = null;
            if (System.IO.File.Exists(economyPath))
            {
                try
                {
                    buyList = EconomyJson.Parse(System.IO.File.ReadAllText(economyPath), economyPath);
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is TuningJsonException)
                {
                    error = "cannot read economy tuning: " + e.Message;
                    return false;
                }
            }
            else
            {
                economyPath = null;
            }

            // 箱の表も Unity では Resources の JSON (Tuning/loot) から読むので、無ければ同じく表なしで動かす
            var lootPath = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(root, ServerMapData.DataDirectoryName, TuningDirectoryName, LootTuningFileName));
            var loot = LootSpec.Default;
            if (System.IO.File.Exists(lootPath))
            {
                try
                {
                    loot = LootJson.Parse(System.IO.File.ReadAllText(lootPath), lootPath);
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is TuningJsonException)
                {
                    error = "cannot read loot tuning: " + e.Message;
                    return false;
                }
            }
            else
            {
                lootPath = null;
            }

            data = new ServerRulesData(
                name, file, ping, path, pingPath, grenades, grenadePath, bots, botsPath, items, itemsPath, knockback, knockbackPath, assist, assistPath,
                chatWords: chatWords, chatWordsPath: chatWordsPath, buyList: buyList, economyPath: economyPath, loot: loot, lootPath: lootPath);
            error = null;
            return true;
        }
    }
}
