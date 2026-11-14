using System;
using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary>
    /// ルールのデバッグパネルで編集中の値。<see cref="GameRulesDebugField"/> の各項目を選択肢の番号 (<see cref="Options"/> の添字) で持ち、
    /// <see cref="Build"/> で元のルール (<see cref="Base"/>) にそれらを当てた <see cref="GameRules"/> を作る。パネルに出さない項目は元のまま残す。
    /// カバーとブラインドファイアは <see cref="ActionRules"/> の制約に合わせて連動させる
    /// (ブラインドファイアを入れるとカバーも入り、カバーを切るとブラインドファイアも切れる)。
    /// </summary>
    public sealed class GameRulesDebugDraft
    {
        public const string LogTag = "[RulesDebug]";

        /// <summary>
        /// グレネードを入れたのに元のルールの <see cref="ActionRules.GrenadeCount"/> が 0 のときに持たせる数。
        /// 0 のままでは入れても投げられず、切り替えた効き目を確かめられないため。
        /// </summary>
        public const int DefaultGrenadeCount = 3;

        private const string _grenadeCountName = "GrenadeCount";

        private static readonly string[] _boolOptions = { "Off", "On" };

        private static readonly GameRulesDebugField[] _allFields = (GameRulesDebugField[])Enum.GetValues(typeof(GameRulesDebugField));

        private static readonly IReadOnlyList<GameRulesDebugField> _readOnlyFields = Array.AsReadOnly(_allFields);

        private readonly int[] _values = new int[_allFields.Length];

        /// <param name="baseRules">初期値と、パネルに出さない項目の値を取るルール。</param>
        public GameRulesDebugDraft(GameRules baseRules)
        {
            Base = baseRules ?? throw new ArgumentNullException(nameof(baseRules));
            foreach (var field in _allFields)
            {
                _values[(int)field] = Read(baseRules, field);
            }
        }

        /// <summary>パネルに出す項目。<see cref="GameRulesDebugField"/> の定義順。</summary>
        public static IReadOnlyList<GameRulesDebugField> Fields => _readOnlyFields;

        /// <summary>初期値と、パネルに出さない項目の値を取るルール。</summary>
        public GameRules Base { get; }

        /// <summary>
        /// field の選択肢の名前。添字が <see cref="Get"/> と <see cref="Set"/> の値になる。
        /// 真偽値は { "Off", "On" }、列挙は定義順の名前。
        /// </summary>
        public static IReadOnlyList<string> Options(GameRulesDebugField field)
        {
            var type = EnumTypeOf(field);
            return type == null ? _boolOptions : Enum.GetNames(type);
        }

        /// <summary>
        /// field をオンオフの 1 つのトグルで出せるか。選択肢が { "Off", 何か } の 2 つのときだけ true で、
        /// トグルの入りは添字 1 にあたる。それ以外はドロップダウンで出す。
        /// </summary>
        public static bool IsToggle(GameRulesDebugField field)
        {
            var options = Options(field);
            return options.Count == 2 && options[0] == "Off";
        }

        /// <summary>rules の field の値を選択肢の添字で返す。</summary>
        public static int Read(GameRules rules, GameRulesDebugField field)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            var actions = rules.Actions;
            switch (field)
            {
                case GameRulesDebugField.FriendlyFire: return (int)rules.FriendlyFire;
                case GameRulesDebugField.RespawnMode: return (int)rules.RespawnMode;
                case GameRulesDebugField.DeathBody: return (int)rules.DeathBody;
                case GameRulesDebugField.DeathCamera: return (int)rules.DeathCamera;
                case GameRulesDebugField.InfiniteReserve: return rules.InfiniteReserve ? 1 : 0;
                case GameRulesDebugField.Destruction: return (int)rules.Destruction;
                case GameRulesDebugField.Downed: return (int)actions.Downed;
                case GameRulesDebugField.Cover: return (int)actions.Cover;
                case GameRulesDebugField.BlindFire: return (int)actions.BlindFire;
                case GameRulesDebugField.ActiveReload: return (int)actions.ActiveReload;
                case GameRulesDebugField.Dodge: return (int)actions.Dodge;
                case GameRulesDebugField.Vault: return (int)actions.Vault;
                case GameRulesDebugField.Grenade: return (int)actions.Grenade;
                case GameRulesDebugField.Melee: return (int)actions.Melee;
                case GameRulesDebugField.Stagger: return (int)actions.Stagger;
                case GameRulesDebugField.Slide: return (int)actions.Slide;
                case GameRulesDebugField.Mantle: return (int)actions.Mantle;
                case GameRulesDebugField.Prone: return (int)actions.Prone;
                case GameRulesDebugField.Ladder: return (int)actions.Ladder;
                case GameRulesDebugField.Zipline: return (int)actions.Zipline;
                case GameRulesDebugField.Parachute: return (int)actions.Parachute;
                case GameRulesDebugField.Stealth: return (int)actions.Stealth;
                case GameRulesDebugField.Suppression: return (int)actions.Suppression;
                case GameRulesDebugField.Ping: return (int)actions.Ping;
                case GameRulesDebugField.Abilities: return (int)actions.Abilities;
                case GameRulesDebugField.SlowMotion: return (int)actions.SlowMotion;
                case GameRulesDebugField.FallDamage: return (int)rules.FallDamage;
                case GameRulesDebugField.HudDensity: return (int)rules.HudDensity;
                default: throw new ArgumentOutOfRangeException(nameof(field), field, "field is not a defined GameRulesDebugField");
            }
        }

        /// <summary>編集中の field の値 (選択肢の添字)。</summary>
        public int Get(GameRulesDebugField field)
        {
            return _values[IndexOf(field)];
        }

        /// <summary>
        /// field を選択肢の添字 option にする。範囲外なら <see cref="ArgumentOutOfRangeException"/> で、値は変えない。
        /// <see cref="GameRulesDebugField.BlindFire"/> を入れるとカバーも入れ、<see cref="GameRulesDebugField.Cover"/> を切るとブラインドファイアも切る。
        /// </summary>
        public void Set(GameRulesDebugField field, int option)
        {
            var index = IndexOf(field);
            if (option < 0 || option >= Options(field).Count)
            {
                throw new ArgumentOutOfRangeException(nameof(option), option, $"option is out of range for {field}");
            }

            _values[index] = option;
            if (field == GameRulesDebugField.BlindFire && option == (int)BlindFire.On)
            {
                _values[(int)GameRulesDebugField.Cover] = (int)Cover.On;
            }
            else if (field == GameRulesDebugField.Cover && option == (int)Cover.Off)
            {
                _values[(int)GameRulesDebugField.BlindFire] = (int)BlindFire.Off;
            }
        }

        /// <summary><see cref="Base"/> に編集中の値を当てたルール。パネルに出さない項目は <see cref="Base"/> のまま。</summary>
        public GameRules Build()
        {
            var cover = (Cover)Get(GameRulesDebugField.Cover);
            var grenade = (Grenade)Get(GameRulesDebugField.Grenade);

            // 先にブラインドファイアを切っておかないと、カバーを切る途中で ActionRules の制約に掛かる。
            var actions = Base.Actions
                .WithBlindFire(BlindFire.Off)
                .WithCover(cover)
                .WithBlindFire((BlindFire)Get(GameRulesDebugField.BlindFire))
                .WithDowned((Downed)Get(GameRulesDebugField.Downed))
                .WithActiveReload((ActiveReload)Get(GameRulesDebugField.ActiveReload))
                .WithDodge((Dodge)Get(GameRulesDebugField.Dodge))
                .WithVault((Vault)Get(GameRulesDebugField.Vault))
                .WithGrenade(grenade)
                .WithMelee((Melee)Get(GameRulesDebugField.Melee))
                .WithStagger((Stagger)Get(GameRulesDebugField.Stagger))
                .WithSlide((Slide)Get(GameRulesDebugField.Slide))
                .WithMantle((Mantle)Get(GameRulesDebugField.Mantle))
                .WithProne((Prone)Get(GameRulesDebugField.Prone))
                .WithLadder((Ladder)Get(GameRulesDebugField.Ladder))
                .WithZipline((Zipline)Get(GameRulesDebugField.Zipline))
                .WithParachute((Parachute)Get(GameRulesDebugField.Parachute))
                .WithStealth((Stealth)Get(GameRulesDebugField.Stealth))
                .WithSuppression((Suppression)Get(GameRulesDebugField.Suppression))
                .WithPing((Ping)Get(GameRulesDebugField.Ping))
                .WithAbilities((Abilities)Get(GameRulesDebugField.Abilities))
                .WithSlowMotion((SlowMotion)Get(GameRulesDebugField.SlowMotion));
            if (grenade == Grenade.On && actions.GrenadeCount == 0)
            {
                actions = actions.WithGrenadeCount(DefaultGrenadeCount);
            }

            return Base
                .WithFriendlyFire((FriendlyFire)Get(GameRulesDebugField.FriendlyFire))
                .WithRespawnMode((RespawnMode)Get(GameRulesDebugField.RespawnMode))
                .WithDeathBody((DeathBody)Get(GameRulesDebugField.DeathBody))
                .WithDeathCamera((DeathCamera)Get(GameRulesDebugField.DeathCamera))
                .WithInfiniteReserve(Get(GameRulesDebugField.InfiniteReserve) == 1)
                .WithDestruction((Destruction)Get(GameRulesDebugField.Destruction))
                .WithFallDamage((FallDamage)Get(GameRulesDebugField.FallDamage))
                .WithHudDensity((HudDensity)Get(GameRulesDebugField.HudDensity))
                .WithActions(actions);
        }

        /// <summary>
        /// before から after で変わった項目を「名前: 前 -> 後」で、<see cref="Fields"/> の順に返す。
        /// グレネードを入れたときに <see cref="DefaultGrenadeCount"/> を持たせた分も GrenadeCount として最後に入る。
        /// </summary>
        public static IReadOnlyList<string> Changes(GameRules before, GameRules after)
        {
            if (before == null)
            {
                throw new ArgumentNullException(nameof(before));
            }

            if (after == null)
            {
                throw new ArgumentNullException(nameof(after));
            }

            var changes = new List<string>();
            foreach (var field in _allFields)
            {
                var from = Read(before, field);
                var to = Read(after, field);
                if (from != to)
                {
                    var options = Options(field);
                    changes.Add($"{field}: {options[from]} -> {options[to]}");
                }
            }

            if (before.Actions.GrenadeCount != after.Actions.GrenadeCount)
            {
                changes.Add($"{_grenadeCountName}: {before.Actions.GrenadeCount} -> {after.Actions.GrenadeCount}");
            }

            return changes;
        }

        /// <summary>適用したときのログ。before は今のシーンが使っているルール、after は上書きに使うルール。</summary>
        public static string FormatApplyLog(GameRules before, GameRules after)
        {
            var changes = Changes(before, after);
            var body = changes.Count == 0 ? "no changes" : string.Join(", ", changes);
            return $"{LogTag} apply: {body}";
        }

        /// <summary>リセットしたときのログ。</summary>
        public static string FormatResetLog()
        {
            return $"{LogTag} reset: GameRulesOverride cleared, reloading with the scene rules";
        }

        private static int IndexOf(GameRulesDebugField field)
        {
            var index = (int)field;
            if (index < 0 || index >= _allFields.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(field), field, "field is not a defined GameRulesDebugField");
            }

            return index;
        }

        /// <summary>field の値の列挙型。真偽値の項目なら null。</summary>
        private static Type EnumTypeOf(GameRulesDebugField field)
        {
            switch (field)
            {
                case GameRulesDebugField.FriendlyFire: return typeof(FriendlyFire);
                case GameRulesDebugField.RespawnMode: return typeof(RespawnMode);
                case GameRulesDebugField.DeathBody: return typeof(DeathBody);
                case GameRulesDebugField.DeathCamera: return typeof(DeathCamera);
                case GameRulesDebugField.InfiniteReserve: return null;
                case GameRulesDebugField.Destruction: return typeof(Destruction);
                case GameRulesDebugField.Downed: return typeof(Downed);
                case GameRulesDebugField.Cover: return typeof(Cover);
                case GameRulesDebugField.BlindFire: return typeof(BlindFire);
                case GameRulesDebugField.ActiveReload: return typeof(ActiveReload);
                case GameRulesDebugField.Dodge: return typeof(Dodge);
                case GameRulesDebugField.Vault: return typeof(Vault);
                case GameRulesDebugField.Grenade: return typeof(Grenade);
                case GameRulesDebugField.Melee: return typeof(Melee);
                case GameRulesDebugField.Stagger: return typeof(Stagger);
                case GameRulesDebugField.Slide: return typeof(Slide);
                case GameRulesDebugField.Mantle: return typeof(Mantle);
                case GameRulesDebugField.Prone: return typeof(Prone);
                case GameRulesDebugField.Ladder: return typeof(Ladder);
                case GameRulesDebugField.Zipline: return typeof(Zipline);
                case GameRulesDebugField.Parachute: return typeof(Parachute);
                case GameRulesDebugField.Stealth: return typeof(Stealth);
                case GameRulesDebugField.Suppression: return typeof(Suppression);
                case GameRulesDebugField.Ping: return typeof(Ping);
                case GameRulesDebugField.Abilities: return typeof(Abilities);
                case GameRulesDebugField.SlowMotion: return typeof(SlowMotion);
                case GameRulesDebugField.FallDamage: return typeof(FallDamage);
                case GameRulesDebugField.HudDensity: return typeof(HudDensity);
                default: throw new ArgumentOutOfRangeException(nameof(field), field, "field is not a defined GameRulesDebugField");
            }
        }
    }
}
