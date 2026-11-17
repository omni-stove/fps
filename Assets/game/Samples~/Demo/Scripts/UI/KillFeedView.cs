using Shooter.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed class KillFeedView : HudElementView
    {
        public const string UxmlName = "kill-feed";

        public const string LogTag = "[KillFeed]";

        public const string RowClass = "kill-feed__row";

        public const string LocalRowClass = "kill-feed__row--local";

        public const string NameClass = "kill-feed__name";

        public const string WeaponClass = "kill-feed__weapon";

        public const string HeadshotClass = "kill-feed__weapon--headshot";

        public const string FriendlyClass = "kill-feed__friendly";

        private readonly KillFeed _feed = new KillFeed();
        private IKillFeedNames _names;
        private OfflineKillFeedNames _offlineNames;
        private ParticipantKillFeedNames _participantNames;
        private bool _subscribed;

        public override string ElementName => UxmlName;

        public KillFeed Feed => _feed;

        public IKillFeedNames Names
        {
            get
            {
                if (_names != null)
                {
                    return _names;
                }

                var participants = GameSignals.Participants.Value;
                if (participants == null)
                {
                    return _offlineNames ??= new OfflineKillFeedNames();
                }

                if (_participantNames == null || !ReferenceEquals(_participantNames.Participants, participants))
                {
                    _participantNames = new ParticipantKillFeedNames(participants);
                }

                return _participantNames;
            }
            set => _names = value;
        }

        // 行をログにも出すのは、画面を見ずにネット対戦の両クライアントのフィードを突き合わせられるようにするため。
        public void Show(KillFeedEntry entry)
        {
            var line = Names.Describe(entry);
            _feed.Add(line);
            Debug.Log($"{LogTag} {line} local={line.Highlighted}", this);
            Render();
        }

        public void Advance(float deltaSeconds)
        {
            var before = _feed.Count;
            _feed.Tick(deltaSeconds);
            if (_feed.Count != before)
            {
                Render();
            }
        }

        public void RefreshTeamColors()
        {
            Render();
        }

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
                row.Add(CreateLabel(Localizer.Resolve(KillFeedText.FriendlyMark), FriendlyClass));
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
