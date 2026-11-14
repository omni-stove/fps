using System;
using Fps.Core;
using Fps.Runtime;
using UnityEngine;
using UnityEngine.UIElements;

namespace Fps.Net
{
    /// <summary>
    /// オンラインの HUD の右上に、サーバーとの回線の Ping・パケットロス・tick (<see cref="NetStatsFormat.Text"/>) を出す。
    /// Hud.uxml の <see cref="UxmlName"/> を使う。中身と出し入れは <see cref="NetHud"/> が決める。
    /// Ping の段階 (<see cref="NetPingLevel"/>) で <see cref="WarningClass"/> (黄) か <see cref="BadClass"/> (赤) を付ける。
    /// 受け取った文は今の言語 (<see cref="Localizer.Resolve"/>) で文にし、言語が変わったら文にし直す。
    /// </summary>
    /// <remarks>
    /// 遊ぶ人が自分で出した表示なので、HUD の量 (<see cref="HudVisibility"/>) では隠さない。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class NetStatsView : HudElementView
    {
        /// <summary>Hud.uxml の回線の表示の Label。</summary>
        public const string UxmlName = "net-stats";

        /// <summary>Ping が <see cref="NetStatsFormat.WarningPingMs"/> 以上のときに付ける USS クラス。</summary>
        public const string WarningClass = "net-stats--warning";

        /// <summary>Ping が <see cref="NetStatsFormat.BadPingMs"/> 以上のときに付ける USS クラス。</summary>
        public const string BadClass = "net-stats--bad";

        public override string ElementName => UxmlName;

        /// <summary>出しているか。</summary>
        public bool IsShown { get; private set; }

        /// <summary>出している文字列 (今の言語の文)。隠している間は空文字。</summary>
        public string Text { get; private set; } = string.Empty;

        /// <summary>出している文。隠している間は <c>default</c>。</summary>
        public LocalizedText Source { get; private set; }

        /// <summary>出している Ping の段階。隠している間は <see cref="NetPingLevel.Good"/>。</summary>
        public NetPingLevel Level { get; private set; }

        /// <summary>
        /// hud に (無ければ) このビューを付けて返す。付けたばかりなら Hud に木から要素を渡し直させる。
        /// UXML に要素が無ければエラーをログに出し、要素を持たないまま返す (文字列は <see cref="Text"/> で読める)。
        /// </summary>
        public static NetStatsView AttachTo(Hud hud)
        {
            if (hud == null)
            {
                throw new ArgumentNullException(nameof(hud));
            }

            var view = hud.GetComponent<NetStatsView>();
            if (view != null)
            {
                return view;
            }

            view = hud.gameObject.AddComponent<NetStatsView>();
            try
            {
                hud.RebuildView();
            }
            catch (InvalidOperationException e)
            {
                Debug.LogError($"{Hud.LogTag} could not show the network stats on the HUD: {e.Message}", hud);
            }

            return view;
        }

        /// <summary>text を level の色で出す。</summary>
        public void Show(LocalizedText text, NetPingLevel level)
        {
            IsShown = true;
            Source = text;
            Text = Localizer.Resolve(text);
            Level = level;
            Refresh();
        }

        /// <summary>隠す。</summary>
        public void Hide()
        {
            IsShown = false;
            Source = default;
            Text = string.Empty;
            Level = NetPingLevel.Good;
            Refresh();
        }

        protected override void OnBound(VisualElement element)
        {
            // 要素を持っていない間の言語の変更は届かないので、受けたときに今の言語で文にし直す
            Text = Localizer.Resolve(Source);
            Refresh();
        }

        protected override void OnLanguageChanged()
        {
            Text = Localizer.Resolve(Source);
            Refresh();
        }

        protected override void OnUnbinding(VisualElement element)
        {
            element.style.display = DisplayStyle.None;
        }

        private void Refresh()
        {
            var element = Element;
            if (element == null)
            {
                return;
            }

            if (element is Label label)
            {
                label.text = Text;
            }

            element.EnableInClassList(WarningClass, IsShown && Level == NetPingLevel.Warning);
            element.EnableInClassList(BadClass, IsShown && Level == NetPingLevel.Bad);
            element.style.display = IsShown ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
