using System;

namespace Fps.Core
{
    /// <summary>
    /// メニューから (またはメニューへ) の 1 回の遷移。読むシーンと、ネット対戦のシーンに渡すロビーの設定を持つ。
    /// 作るのは <see cref="MenuRoute"/>。
    /// </summary>
    public sealed class MenuTransition
    {
        private MenuTransition(MenuTransitionKind kind, string scenePath, LobbyLaunchOptions lobby)
        {
            Kind = kind;
            ScenePath = scenePath;
            Lobby = lobby;
        }

        public MenuTransitionKind Kind { get; }

        /// <summary>読むシーンのパス。<see cref="MenuTransitionKind.Quit"/> では null。</summary>
        public string ScenePath { get; }

        /// <summary>読んだシーンで使うロビーの設定。ロビーを使わなければ <see cref="LobbyLaunchOptions.None"/>。</summary>
        public LobbyLaunchOptions Lobby { get; }

        public static MenuTransition Quit { get; } = new MenuTransition(MenuTransitionKind.Quit, null, LobbyLaunchOptions.None);

        /// <summary>scenePath のシーンを読む遷移。</summary>
        /// <param name="lobby">読んだシーンで使うロビーの設定。null なら <see cref="LobbyLaunchOptions.None"/>。</param>
        public static MenuTransition Load(string scenePath, LobbyLaunchOptions lobby = null)
        {
            if (string.IsNullOrWhiteSpace(scenePath))
            {
                throw new ArgumentException("scene path must not be empty", nameof(scenePath));
            }

            return new MenuTransition(MenuTransitionKind.LoadScene, scenePath, lobby ?? LobbyLaunchOptions.None);
        }

        public override string ToString()
        {
            if (Kind == MenuTransitionKind.Quit)
            {
                return "quit";
            }

            return Lobby.IsRequested ? "load " + ScenePath + " lobby=" + Lobby : "load " + ScenePath;
        }
    }
}
