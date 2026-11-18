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
            return _factories.ContainsKey(kind);
        }

        public static bool TryGet(MainMenuPanelKind kind, out Func<VisualElement, IMainMenuPanel> open)
        {
            return _factories.TryGetValue(kind, out open);
        }

        public static void Clear()
        {
            foreach (var kind in Kinds)
            {
                Unregister(kind);
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
