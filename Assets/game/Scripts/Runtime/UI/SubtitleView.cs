using System.Collections.Generic;
using Fps.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Fps.Runtime
{
    /// <summary>
    /// 音の合図の字幕を画面の下に出すビュー。Hud.uxml の <see cref="UxmlName"/> を使う。
    /// <see cref="SubtitleFeed.CuePlayed"/> を受けるたびに、今のカメラ (<see cref="Viewer"/>) から見た向きと距離で
    /// 「[銃声 左後ろ]」の形の行を出し (<see cref="SubtitleLines"/>)、最後の合図から <see cref="SubtitleLines.DefaultSeconds"/> 秒で消す。
    /// 同じ種類の合図は 1 行にまとめる。設定 (<see cref="ControlSettings.ShowSubtitles"/>) が Off の間は何も出さず、
    /// Off にしたら出している行を消す。向きは合図を受けたときに決め、後から振り向いても書き換えない。
    /// 字幕は聞こえない人の代わりの情報なので、HUD の量 (<see cref="HudVisibility"/>) では隠さない。
    /// オフラインとオンラインで同じ HUD (<see cref="Hud.AddViews"/>) に付く。専用サーバーでは購読しない。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SubtitleView : HudElementView
    {
        /// <summary>Hud.uxml の字幕の要素。</summary>
        public const string UxmlName = "subtitles";

        /// <summary>1 行の Label に付けるクラス。</summary>
        public const string LineClass = "subtitles__line";

        private readonly SubtitleLines _lines = new SubtitleLines();
        private readonly List<Label> _labels = new List<Label>();
        private bool _subscribed;
        private float _clock;

        public override string ElementName => UxmlName;

        /// <summary>
        /// 向きの基準にするカメラ。null なら <see cref="Camera.main"/> を使う (オフラインの PlayerCamera とオンラインの ViewCamera は MainCamera)。
        /// </summary>
        public Transform Viewer { get; set; }

        /// <summary>設定 (<see cref="AppliedControlSettings.Current"/>) で字幕を出すことになっているか。</summary>
        public bool ShowsSubtitles => AppliedControlSettings.Current.ShowSubtitles;

        /// <summary>出している行 (最初に出た順)。</summary>
        public IReadOnlyList<SubtitleLine> Lines => _lines.Lines;

        private void OnEnable()
        {
            if (NetStartup.ShouldDisablePresentation(NetStartup.IsDedicatedServerBuild))
            {
                return;
            }

            SubtitleFeed.CuePlayed += Show;
            AppliedControlSettings.Changed += HandleSettingsChanged;
            _subscribed = true;
        }

        private void OnDisable()
        {
            if (_subscribed)
            {
                SubtitleFeed.CuePlayed -= Show;
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

        /// <summary>
        /// source の合図を、今のカメラから見た向きと距離で字幕にする。字幕が Off か、カメラから聞こえない距離なら何もしない。
        /// カメラが無ければ向きを書かずに出す。
        /// </summary>
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

        /// <summary>deltaSeconds 秒進め、出し終えた行を消す。負か有限でない値では進めない。</summary>
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

        /// <summary>行をすべて消す。</summary>
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
