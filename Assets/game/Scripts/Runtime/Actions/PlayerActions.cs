using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// プレイヤーに TPS のアクションを付け、ルールでどれを有効にするかを決める。アクションごとの取り付けは <see cref="IPlayerActionInstaller"/> で、
    /// 組み込みのカバー (<see cref="CoverActionInstaller"/>)・ブラインドファイア (<see cref="BlindFireActionInstaller"/>)・
    /// 回避・乗り越え・崖のぼり (<see cref="MoveActionInstaller"/>)・制圧射撃 (<see cref="SuppressionActionInstaller"/>)・時間の速さ (<see cref="SlowMotionActionInstaller"/>)・グレネード (<see cref="GrenadeActionInstaller"/>) に、
    /// <see cref="Register"/> で足したものを加えて <see cref="IPlayerActionInstaller.Order"/> の順に呼ぶ。
    /// オフラインの <see cref="GameBootstrap"/> と <see cref="CampaignRunner"/> がルールを読んだ後に呼ぶ。
    /// </summary>
    public static class PlayerActions
    {
        public const string LogTag = "[Actions]";

        private static readonly List<IPlayerActionInstaller> _installers = new List<IPlayerActionInstaller>
        {
            new CoverActionInstaller(),
            new BlindFireActionInstaller(),
            new MoveActionInstaller(),
            new SuppressionActionInstaller(),
            new SlowMotionActionInstaller(),
            new GrenadeActionInstaller(),
        };

        /// <summary>今登録されている取り付け。<see cref="Attach"/> が呼ぶ順。</summary>
        public static IReadOnlyList<IPlayerActionInstaller> Installers
        {
            get
            {
                lock (_installers)
                {
                    return _installers.ToArray();
                }
            }
        }

        /// <summary>
        /// installer を <see cref="Attach"/> で呼ぶものに加える。同じ型のものが既にあれば置き換える
        /// (ドメインの再読み込みを切った Play のたびに登録し直しても増えない)。
        /// </summary>
        /// <exception cref="ArgumentNullException">installer が null。</exception>
        public static void Register(IPlayerActionInstaller installer)
        {
            if (installer == null)
            {
                throw new ArgumentNullException(nameof(installer));
            }

            lock (_installers)
            {
                _installers.RemoveAll(existing => existing.GetType() == installer.GetType());
                _installers.Add(installer);
                _installers.Sort(CompareInstallers);
            }
        }

        /// <summary>installer と同じ型の取り付けを外す。外したら true、無ければ false。</summary>
        /// <exception cref="ArgumentNullException">installer が null。</exception>
        public static bool Unregister(IPlayerActionInstaller installer)
        {
            if (installer == null)
            {
                throw new ArgumentNullException(nameof(installer));
            }

            lock (_installers)
            {
                return _installers.RemoveAll(existing => existing.GetType() == installer.GetType()) > 0;
            }
        }

        /// <summary>
        /// player に登録された取り付け (<see cref="Installers"/>) を順に呼び、rules で各アクションの有効・無効を決める。
        /// 何度呼んでもコンポーネントは増えず、最後に渡した rules に従う。1 つの取り付けが投げても、エラーに出してほかの取り付けは続ける。
        /// 決めた内容を <see cref="FormatLog"/> の 1 行でログに出す。
        /// </summary>
        public static void Attach(GameObject player, GameRules rules)
        {
            if (player == null)
            {
                throw new ArgumentNullException(nameof(player));
            }

            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            foreach (var installer in Installers)
            {
                try
                {
                    installer.Install(player, rules);
                }
                catch (Exception e)
                {
                    Debug.LogError($"{LogTag} {installer.GetType().Name} failed to install on '{player.name}': {e.Message}", player);
                    Debug.LogException(e, player);
                }
            }

            Debug.Log(FormatLog(rules.Actions), player);
        }

        /// <summary>
        /// アクションのルールのログ。例: <c>[Actions] cover=On blindFire=Off activeReload=Off downed=Off dodge=Off vault=Off melee=Off ...</c>。
        /// 後ろに近接攻撃から時間の速さまでを <see cref="ActionRules"/> の並びで続ける。
        /// </summary>
        public static string FormatLog(ActionRules actions)
        {
            if (actions == null)
            {
                throw new ArgumentNullException(nameof(actions));
            }

            return $"{LogTag} cover={actions.Cover} blindFire={actions.BlindFire} activeReload={actions.ActiveReload} downed={actions.Downed}"
                + $" dodge={actions.Dodge} vault={actions.Vault}"
                + $" melee={actions.Melee} stagger={actions.Stagger} slide={actions.Slide} mantle={actions.Mantle} prone={actions.Prone}"
                + $" ladder={actions.Ladder} zipline={actions.Zipline} parachute={actions.Parachute} stealth={actions.Stealth}"
                + $" suppression={actions.Suppression} ping={actions.Ping} abilities={actions.Abilities} slowMotion={actions.SlowMotion}";
        }

        /// <summary>player の T を返す。無ければ付けて返す。取り付けが自分のコンポーネントを増やさないために使う。</summary>
        public static T GetOrAdd<T>(GameObject player)
            where T : Component
        {
            var component = player.GetComponent<T>();
            return component != null ? component : player.AddComponent<T>();
        }

        private static int CompareInstallers(IPlayerActionInstaller a, IPlayerActionInstaller b)
        {
            var byOrder = a.Order.CompareTo(b.Order);
            return byOrder != 0 ? byOrder : string.CompareOrdinal(a.GetType().FullName, b.GetType().FullName);
        }
    }
}
