using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    // 字幕は聞こえない人の代わりの情報なので、HUD の量 (HudVisibility) では隠さない。
    [DisallowMultipleComponent]
    public sealed class SubtitleView : HudElementView
    {
        public const string UxmlName = "subtitles";

        public const string LineClass = "subtitles__line";

        private readonly SubtitleLines _lines = new SubtitleLines();
        private readonly List<Label> _labels = new List<Label>();
        private bool _subscribed;
        private float _clock;

        public override string ElementName => UxmlName;

        // 未設定なら Camera.main を基準にするのは、オフラインの PlayerCamera もオンラインの ViewCamera も MainCamera だから。
        public Transform Viewer { get; set; }

        public bool ShowsSubtitles => AppliedControlSettings.Current.ShowSubtitles;

        public IReadOnlyList<SubtitleLine> Lines => _lines.Lines;

        private void OnEnable()
        {
            if (NetStartup.ShouldDisablePresentation(NetStartup.IsDedicatedServerBuild))
            {
                return;
            }

            SubtitleFeed.CuePlayed += Show;
            SubtitleFeed.Spoken += Show;
            SubtitleFeed.SquadReplied += Show;
            AppliedControlSettings.Changed += HandleSettingsChanged;
            _subscribed = true;
        }

        private void OnDisable()
        {
            if (_subscribed)
            {
                SubtitleFeed.CuePlayed -= Show;
                SubtitleFeed.Spoken -= Show;
                SubtitleFeed.SquadReplied -= Show;
                AppliedControlSettings.Changed -= HandleSettingsChanged;
                _subscribed = false;
            }

            Clear();
        }

        private void LateUpdate()
        {
            // ポーズ中に行が残り続けないよう、時間の止まらない経過で消す。
            Tick(Time.unscaledDeltaTime);
        }

        public void Show(SoundCueSource source)
        {
            if (!ShowsSubtitles || !TryLocate(source, out var cue))
            {
                return;
            }

            if (_lines.Show(cue, _clock))
            {
                Refresh();
            }
        }

        // 話した内容は声の素材が無いと字幕でしか伝わらないので、音の字幕の設定 (ShowsSubtitles) が Off でも出す。
        public void Show(SubtitleSpeech speech)
        {
            if (speech.Speaker == null || speech.Text == null)
            {
                return;
            }

            if (_lines.Say(speech.Speaker, speech.Text, _clock))
            {
                Refresh();
            }
        }

        public void Show(SquadOrderReply reply)
        {
            if (reply.Speaker == null)
            {
                return;
            }

            Show(new SubtitleSpeech(reply.Speaker, SquadOrderText.Reply(reply)));
        }

        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds > 0f && !float.IsInfinity(deltaSeconds))
            {
                _clock += deltaSeconds;
            }

            if (_lines.Expire(_clock))
            {
                Refresh();
            }
        }

        public void Clear()
        {
            _lines.Clear();
            Refresh();
        }

        protected override void OnBound(VisualElement element)
        {
            // 前の木の Label は木と一緒に捨てられているので、新しい木に作り直す。
            _labels.Clear();
            Refresh();
        }

        protected override void OnUnbinding(VisualElement element)
        {
            foreach (var label in _labels)
            {
                label.RemoveFromHierarchy();
            }

            _labels.Clear();
        }

        private void HandleSettingsChanged(ControlSettings settings)
        {
            // 破棄の後に届いた通知では消えた要素を触らない。
            if (this != null && !settings.ShowSubtitles)
            {
                Clear();
            }
        }

        private bool TryLocate(SoundCueSource source, out SoundCue cue)
        {
            var viewer = ResolveViewer();
            if (viewer == null)
            {
                cue = new SoundCue(source.Kind, SoundCueDirection.None, float.NaN);
                return true;
            }

            return SoundCue.TryLocate(
                source.Kind,
                viewer.position.ToNumerics(),
                viewer.eulerAngles.y,
                source.Position.HasValue ? source.Position.Value.ToNumerics() : default(System.Numerics.Vector3?),
                source.AudibleDistance,
                out cue);
        }

        private Transform ResolveViewer()
        {
            if (Viewer != null)
            {
                return Viewer;
            }

            var main = Camera.main;
            return main != null ? main.transform : null;
        }

        private void Refresh()
        {
            var element = Element;
            if (element == null)
            {
                return;
            }

            var lines = _lines.Lines;
            while (_labels.Count > lines.Count)
            {
                _labels[_labels.Count - 1].RemoveFromHierarchy();
                _labels.RemoveAt(_labels.Count - 1);
            }

            while (_labels.Count < lines.Count)
            {
                var label = new Label { pickingMode = PickingMode.Ignore };
                label.AddToClassList(LineClass);
                element.Add(label);
                _labels.Add(label);
            }

            for (var i = 0; i < lines.Count; i++)
            {
                _labels[i].text = lines[i].Text;
            }

            element.style.display = lines.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
