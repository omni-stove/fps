using System;

namespace Shooter.Core
{
    public sealed class MenuTransition
    {
        private MenuTransition(MenuTransitionKind kind, string scenePath, bool startsTutorial)
        {
            Kind = kind;
            ScenePath = scenePath;
            StartsTutorial = startsTutorial;
        }

        public MenuTransitionKind Kind { get; }

        public string ScenePath { get; }

        public bool StartsTutorial { get; }

        public static MenuTransition Quit { get; } = new MenuTransition(MenuTransitionKind.Quit, null, false);

        public static MenuTransition Load(string scenePath, bool startsTutorial = false)
        {
            if (string.IsNullOrWhiteSpace(scenePath))
            {
                throw new ArgumentException("scene path must not be empty", nameof(scenePath));
            }

            return new MenuTransition(MenuTransitionKind.LoadScene, scenePath, startsTutorial);
        }

        public override string ToString()
        {
            if (Kind == MenuTransitionKind.Quit)
            {
                return "quit";
            }

            var text = "load " + ScenePath;
            return StartsTutorial ? text + " tutorial" : text;
        }
    }
}
