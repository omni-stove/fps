using System;

namespace Fps.Core
{
    /// <summary>
    /// メニューから (またはメニューへ) の 1 回の遷移。読むシーンと、ネット対戦のシーンに渡すロビーの設定、
    /// 射撃場でチュートリアルを出すよう求めるかを持つ。作るのは <see cref="MenuRoute"/>。
    /// </summary>
    public sealed class MenuTransition
    {
        private MenuTransition(MenuTransitionKind kind, string scenePath, LobbyLaunchOptions lobby, bool startsTutorial)
        {
            Kind = kind;
            ScenePath = scenePath;
            Lobby = lobby;
            StartsTutorial = startsTutorial;
        }

        public MenuTransitionKind Kind { get; }

        /// <summary>読むシーンのパス。<see cref="MenuTransitionKind.Quit"/> では null。</summary>
        public string ScenePath { get; }

        /// <summary>読んだシーンで使うロビーの設定。ロビーを使わなければ <see cref="LobbyLaunchOptions.None"/>。</summary>
        public LobbyLaunchOptions Lobby { get; }

        /// <summary>読んだシーンで、終えたことがあってもチュートリアルを最初から出すか。</summary>
        public bool StartsTutorial { get; }

        public static MenuTransition Quit { get; } = new MenuTransition(MenuTransitionKind.Quit, null, LobbyLaunchOptions.None, false);

        /// <summary>シーンを読む遷移。</summary>
        /// <param name="scenePath">読むシーンのパス。</param>
        /// <param name="lobby">読んだシーンで使うロビーの設定。null なら <see cref="LobbyLaunchOptions.None"/>。</param>
        /// <param name="startsTutorial">読んだシーンでチュートリアルを最初から出すか。</param>
        public static MenuTransition Load(string scenePath, LobbyLaunchOptions lobby = null, bool startsTutorial = false)
        {
            if (string.IsNullOrWhiteSpace(scenePath))
            {
                throw new ArgumentException("scene path must not be empty", nameof(scenePath));
            }

            return new MenuTransition(MenuTransitionKind.LoadScene, scenePath, lobby ?? LobbyLaunchOptions.None, startsTutorial);
        }

        public override string ToString()
        {
            if (Kind == MenuTransitionKind.Quit)
            {
                return "quit";
            }

            var text = Lobby.IsRequested ? "load " + ScenePath + " lobby=" + Lobby : "load " + ScenePath;
            return StartsTutorial ? text + " tutorial" : text;
        }
    }
}
