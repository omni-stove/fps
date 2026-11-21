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
        Playlists,
        ServerBrowser,
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

        private static readonly HashSet<MainMenuPanelKind> _hidden = new HashSet<MainMenuPanelKind>();

        public static readonly IReadOnlyList<MainMenuPanelKind> Kinds = new[]
        {
            MainMenuPanelKind.Stats,
            MainMenuPanelKind.Leaderboard,
            MainMenuPanelKind.Replays,
            MainMenuPanelKind.Cosmetics,
            MainMenuPanelKind.Stash,
            MainMenuPanelKind.Playlists,
            MainMenuPanelKind.ServerBrowser,
        };

        public static event Action<MainMenuPanelKind> Changed;

        public static void Register(MainMenuPanelKind kind, Func<VisualElement, IMainMenuPanel> open)
        {
            Validate(kind);
            _factories[kind] = open ?? throw new ArgumentNullException(nameof(open));
            _hidden.Remove(kind);
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
            var removed = _factories.Remove(kind);
            // 隠す印は既定が入っているかに関わらず残すのは、既定が後から (UseDefault で) 入っても、ゲームが先に消したボタンを戻さないため。
            if (_hidden.Add(kind) && _defaults.ContainsKey(kind))
            {
                removed = true;
            }

            if (!removed)
            {
                return false;
            }

            Changed?.Invoke(kind);
            return true;
        }

        public static bool IsRegistered(MainMenuPanelKind kind)
        {
            return TryGet(kind, out _);
        }

        public static bool TryGet(MainMenuPanelKind kind, out Func<VisualElement, IMainMenuPanel> open)
        {
            if (_factories.TryGetValue(kind, out open))
            {
                return true;
            }

            if (_hidden.Contains(kind))
            {
                open = null;
                return false;
            }

            return _defaults.TryGetValue(kind, out open);
        }

        public static void Clear()
        {
            foreach (var kind in Kinds)
            {
                var changed = IsRegistered(kind);
                _factories.Remove(kind);
                _defaults.Remove(kind);
                _hidden.Remove(kind);
                if (changed)
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
