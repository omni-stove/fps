using Fps.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Fps.Runtime
{
    /// <summary>
    /// 誰が誰をどの武器で倒したかを画面右上に並べるビュー。Hud.uxml の <see cref="UxmlName"/> を使う。
    /// 有効な間 <see cref="CombatEvents.Killed"/> を受け、<see cref="Names"/> で名前と武器の表示名を引いた行を <see cref="Feed"/> に足す
    /// (最大 <see cref="KillFeed.DefaultCapacity"/> 行、1 行 <see cref="KillFeed.DefaultLineSeconds"/> 秒で消える)。
    /// </summary>
    /// <remarks>
    /// 名前の色はチーム (<see cref="Hud.TeamColor"/>) で、チームの無い名前は USS の既定の色のまま。この画面のプレイヤーが倒したか
    /// 倒された行 (<see cref="KillFeedLine.Highlighted"/>) は <see cref="LocalRowClass"/> で強調する。
    /// 印と武器の欄は描くときに今の言語 (<see cref="Localizer.Resolve"/>) で文にし、言語が変わったら描き直す。
    /// 足した行は <see cref="LogTag"/> でログにも出し、画面を見ずにネット対戦の両クライアントのフィードを突き合わせられるようにする。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class KillFeedView : HudElementView
    {
        /// <summary>Hud.uxml のキルフィードの要素。</summary>
        public const string UxmlName = "kill-feed";

        /// <summary>行を足したときのログの頭。</summary>
        public const string LogTag = "[KillFeed]";

        /// <summary>1 行の要素の USS クラス。</summary>
        public const string RowClass = "kill-feed__row";

        /// <summary>この画面のプレイヤーが関わる行に足す USS クラス。</summary>
        public const string LocalRowClass = "kill-feed__row--local";

        /// <summary>名前の Label の USS クラス。</summary>
        public const string NameClass = "kill-feed__name";

        /// <summary>武器の欄の Label の USS クラス。</summary>
        public const string WeaponClass = "kill-feed__weapon";

        /// <summary>頭への止めの行の武器の欄に足す USS クラス。</summary>
        public const string HeadshotClass = "kill-feed__weapon--headshot";

        /// <summary>味方撃ちの印の Label の USS クラス。</summary>
        public const string FriendlyClass = "kill-feed__friendly";

        private readonly KillFeed _feed = new KillFeed();
        private IKillFeedNames _names;
        private bool _subscribed;

        public override string ElementName => UxmlName;

        /// <summary>出している行。</summary>
        public KillFeed Feed => _feed;

        /// <summary>
        /// 1 件を行にする引き先。未設定 (null) ならオフラインの <see cref="OfflineKillFeedNames"/>。
        /// オンラインでは Fps.Net の NetHud がプレイヤー Id とチームで引くものを設定する。差し替えても出している行はそのまま。
        /// </summary>
        public IKillFeedNames Names
        {
            get => _names ??= new OfflineKillFeedNames();
            set => _names = value;
        }

        /// <summary>entry を行にして一番新しい行として足し、ログに出す。<see cref="CombatEvents.Killed"/> から呼ばれる。</summary>
        public void Show(KillFeedEntry entry)
        {
            var line = Names.Describe(entry);
            _feed.Add(line);
            Debug.Log($"{LogTag} {line} killer={entry.KillerId} victim={entry.VictimId} local={line.Highlighted}", this);
            Render();
        }

        /// <summary>行の時間を deltaSeconds 進め、消えた行があれば出し直す。Play 中は Update が呼ぶ。</summary>
        public void Advance(float deltaSeconds)
        {
            var before = _feed.Count;
            _feed.Tick(deltaSeconds);
            if (_feed.Count != before)
            {
                Render();
            }
        }

        /// <summary>名前の色 (<see cref="Hud.TeamColor"/>) が変わったときに呼ぶ。出している行をその色で塗り直す。</summary>
        public void RefreshTeamColors()
        {
            Render();
        }

        /// <summary>行をすべて消す。</summary>
        public void Clear()
        {
            _feed.Clear();
            Render();
        }

        private void OnEnable()
        {
            if (!_subscribed)
            {
                CombatEvents.Killed += Show;
                _subscribed = true;
            }
        }

        private void OnDisable()
        {
            if (_subscribed)
            {
                CombatEvents.Killed -= Show;
                _subscribed = false;
            }
        }

        private void Update()
        {
            Advance(Time.deltaTime);
        }

        protected override void OnBound(VisualElement element)
        {
            Render();
        }

        protected override void OnLanguageChanged()
        {
            Render();
        }

        protected override void OnUnbinding(VisualElement element)
        {
            element.Clear();
            element.style.display = DisplayStyle.None;
        }

        // 行は最大 5 つで、変わるのは足したときと消えたときだけなので、差分を取らずに作り直す。
        private void Render()
        {
            var element = Element;
            if (element == null)
            {
                return;
            }

            element.Clear();
            var hud = GetComponent<Hud>();
            foreach (var line in _feed.Lines)
            {
                element.Add(CreateRow(line, hud));
            }

            element.style.display = _feed.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static VisualElement CreateRow(KillFeedLine line, Hud hud)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList(RowClass);
            if (line.Highlighted)
            {
                row.AddToClassList(LocalRowClass);
            }

            if (line.Entry.Friendly)
            {
                row.Add(CreateLabel(Localizer.Resolve(KillFeedFormat.FriendlyMark), FriendlyClass));
            }

            if (line.ShowsKiller)
            {
                row.Add(CreateName(line.Killer, hud));
            }

            var weapon = CreateLabel(Localizer.Resolve(line.WeaponText), WeaponClass);
            if (line.Entry.Headshot)
            {
                weapon.AddToClassList(HeadshotClass);
            }

            row.Add(weapon);
            row.Add(CreateName(line.Victim, hud));
            return row;
        }

        private static Label CreateName(KillFeedName name, Hud hud)
        {
            var label = CreateLabel(name.Text, NameClass);
            if (name.Team.HasValue && hud != null)
            {
                label.style.color = hud.TeamColor(name.Team.Value);
            }

            return label;
        }

        private static Label CreateLabel(string text, string className)
        {
            var label = new Label(text ?? string.Empty) { pickingMode = PickingMode.Ignore };
            label.AddToClassList(className);
            return label;
        }
    }
}
