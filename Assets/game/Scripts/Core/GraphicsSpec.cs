using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;

namespace Shooter.Core
{
    // グラフィック設定の、ゲームが決める部分 (初期値・選べる fps 上限・画面の無いクライアントの fps)。プレイヤーが選んだ値は GraphicsSettings が持つ。
    public sealed class GraphicsSpec
    {
        // NetV2Client が画面の無いクライアントに入れていた値。
        public const int DefaultHeadlessFrameRate = 60;

        // Default より前に置くのは、静的な初期化が書いた順に走り、後ろに置くと Default が null の一覧で作られるため。
        private static readonly int[] _defaultFrameRateChoices = { 30, 60, 120, 144, 240 };

        public GraphicsSpec(
            GraphicsSettings defaults = null,
            IReadOnlyList<int> frameRateChoices = null,
            int headlessFrameRate = DefaultHeadlessFrameRate)
        {
            if (defaults != null && !defaults.Resolution.IsNative)
            {
                throw new ArgumentException(
                    "the default resolution must be the display's own (0x0); a fixed size does not fit every display", nameof(defaults));
            }

            var choices = frameRateChoices ?? _defaultFrameRateChoices;
            var copied = new int[choices.Count];
            for (var i = 0; i < copied.Length; i++)
            {
                var choice = choices[i];
                if (choice == GraphicsSettings.NoFrameRateLimit || !GraphicsSettings.IsValidFrameRateLimit(choice))
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(frameRateChoices),
                        choice,
                        $"every frame rate choice must be within {Format(GraphicsSettings.MinFrameRateLimit)} to {Format(GraphicsSettings.MaxFrameRateLimit)} (no limit is always offered)");
                }

                // 設定画面が小さい順に巡るので、並びが崩れていると同じ値へ戻れない選び方になる。
                if (i > 0 && choice <= copied[i - 1])
                {
                    throw new ArgumentException(
                        $"frame rate choices must be in increasing order without repeats, but {Format(choice)} follows {Format(copied[i - 1])}",
                        nameof(frameRateChoices));
                }

                copied[i] = choice;
            }

            if (headlessFrameRate < GraphicsSettings.MinFrameRateLimit || headlessFrameRate > GraphicsSettings.MaxFrameRateLimit)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(headlessFrameRate),
                    headlessFrameRate,
                    $"the headless frame rate must be within {Format(GraphicsSettings.MinFrameRateLimit)} to {Format(GraphicsSettings.MaxFrameRateLimit)}");
            }

            Defaults = defaults ?? GraphicsSettings.Default;
            FrameRateChoices = new ReadOnlyCollection<int>(copied);
            HeadlessFrameRate = headlessFrameRate;
        }

        public static GraphicsSpec Default { get; } = new GraphicsSpec();

        public static IReadOnlyList<int> DefaultFrameRateChoices { get; } = new ReadOnlyCollection<int>(_defaultFrameRateChoices);

        public GraphicsSettings Defaults { get; }

        // 設定画面で「無制限」の次に巡る上限。小さい順。
        public IReadOnlyList<int> FrameRateChoices { get; }

        // 画面の無いクライアント (並べて動かすボット) の fps。プレイヤーの上限がこれより低ければそちらに従う。
        public int HeadlessFrameRate { get; }

        private static string Format(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
