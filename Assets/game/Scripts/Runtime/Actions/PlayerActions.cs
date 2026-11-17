using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public static class PlayerActions
    {
        public const string LogTag = "[Actions]";

        private static readonly List<IPlayerActionInstaller> _installers = new List<IPlayerActionInstaller>
        {
            new CoverActionInstaller(),
            new BlindFireActionInstaller(),
            new MoveActionInstaller(),
            new SuppressionActionInstaller(),
            new ProneActionInstaller(),
            new SlowMotionActionInstaller(),
            new AbilityActionInstaller(),
            new GrenadeActionInstaller(),
        };

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

        // 同じ型のものを置き換えるのは、ドメインの再読み込みを切った Play のたびに登録し直しても増えないようにするため。
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
                + $" suppression={actions.Suppression} ping={actions.Ping} abilities={actions.Abilities} slowMotion={actions.SlowMotion}"
                + $" aimToFire={actions.AimToFire} jump={actions.Jump}";
        }

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
