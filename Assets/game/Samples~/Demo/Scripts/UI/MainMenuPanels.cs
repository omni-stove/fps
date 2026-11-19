using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    public enum MainMenuPanelKind
    {
        Stats,
        Leaderboard,
        Replays,
        Cosmetics,
        Stash,
    }

    public interface IMainMenuPanel : IDisposable
    {
        event Action Cancelled;
    }

    // 同じ種類の登録を例外にせず置き換えるのは、ドメインの再読み込みを切った Play で登録を繰り返しても重ならないようにするため。
    // メインメニューに置かず静的に持つのは、画面を足す側がメニューのシーンを読む前 (RuntimeInitializeOnLoadMethod など) に登録できるようにするため。
    public static class MainMenuPanels
    {
        private static readonly Dictionary<MainMenuPanelKind, Func<VisualElement, IMainMenuPanel>> _factories =
            new Dictionary<MainMenuPanelKind, Func<VisualElement, IMainMenuPanel>>();

        private static readonly Dictionary<MainMenuPanelKind, Func<VisualElement, IMainMenuPanel>> _defaults =
            new Dictionary<MainMenuPanelKind, Func<VisualElement, IMainMenuPanel>>();

        public static readonly IReadOnlyList<MainMenuPanelKind> Kinds = new[]
        {
            MainMenuPanelKind.Stats,
            MainMenuPanelKind.Leaderboard,
            MainMenuPanelKind.Replays,
            MainMenuPanelKind.Cosmetics,
            MainMenuPanelKind.Stash,
        };

        public static event Action<MainMenuPanelKind> Changed;

        public static void Register(MainMenuPanelKind kind, Func<VisualElement, IMainMenuPanel> open)
        {
            Validate(kind);
            _factories[kind] = open ?? throw new ArgumentNullException(nameof(open));
            Changed?.Invoke(kind);
        }

        // 既定の登録をゲームの登録と別に持つのは、サンプルの既定の登録が、ゲームが先に登録した画面を上書きしないようにするため。
        public static void UseDefault(MainMenuPanelKind kind, Func<VisualElement, IMainMenuPanel> open)
        {
            Validate(kind);
            _defaults[kind] = open ?? throw new ArgumentNullException(nameof(open));
            Changed?.Invoke(kind);
        }

        public static bool Unregister(MainMenuPanelKind kind)
        {
            Validate(kind);
            if (!_factories.Remove(kind))
            {
                return false;
            }

            Changed?.Invoke(kind);
            return true;
        }

        public static bool IsRegistered(MainMenuPanelKind kind)
        {
            return _factories.ContainsKey(kind) || _defaults.ContainsKey(kind);
        }

        public static bool TryGet(MainMenuPanelKind kind, out Func<VisualElement, IMainMenuPanel> open)
        {
            return _factories.TryGetValue(kind, out open) || _defaults.TryGetValue(kind, out open);
        }

        public static void Clear()
        {
            foreach (var kind in Kinds)
            {
                var removed = _defaults.Remove(kind);
                if (!Unregister(kind) && removed)
                {
                    Changed?.Invoke(kind);
                }
            }
        }

        private static void Validate(MainMenuPanelKind kind)
        {
            if (!Enum.IsDefined(typeof(MainMenuPanelKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "unknown main menu screen");
            }
        }
    }
}
