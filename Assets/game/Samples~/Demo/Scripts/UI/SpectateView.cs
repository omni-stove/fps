using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    /// <summary>
    /// ネット対戦で味方を見ている間、見ている味方の名前・前と次の味方へ替える操作・復活までの秒数を 1 行ずつ出す (<see cref="SpectateText.Lines"/>)。
    /// Hud.uxml の <see cref="UxmlName"/> に行を入れ、見た目は Hud.uss の spectate のクラスで決める。
    /// 要素を持っている間は <see cref="GameSignals.Spectate"/>・<see cref="GameSignals.Participants"/>・<see cref="GameSignals.HudModel"/> (その案内) を購読し、
    /// <see cref="GameSignals.Spectate"/> に値が無い間は隠す。名前は参加者の表 (<see cref="IParticipants"/>) から、秒数は HUD のリスポーンの案内 (<see cref="HudNotice"/>) から引く。
    /// <see cref="Hud.AddViews"/> は付けないので、使うときは HUD の GameObject に付ける。木を持つ HUD に後から付けたときは、Start でその木を受ける。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpectateView : HudElementView
    {
        /// <summary>Hud.uxml の観戦の VisualElement。</summary>
        public const string UxmlName = "spectate";

        /// <summary>1 行分の Label に付ける USS クラス。</summary>
        public const string RowClass = "spectate__row";

        /// <summary>見ている味方の行に足す USS クラス。</summary>
        public const string TargetRowClass = "spectate__row--target";

        private readonly List<string> _lines = new List<string>();
        private readonly List<Label> _rows = new List<Label>();
        private SpectateStatus? _status;
        private HudModel _model;
        private string _shownName;
        private bool _subscribed;

        public override string ElementName => UxmlName;

        /// <summary>出している観戦の状態。出していなければ null。</summary>
        public SpectateStatus? Status => _status;

        public bool IsShown => _status.HasValue;

        /// <summary>出している行の文。出していなければ空。</summary>
        public IReadOnlyList<string> Lines => _lines;

        /// <summary>status を出す。null なら隠す。要素を持っている間は <see cref="GameSignals.Spectate"/> が出されるたびに呼ばれる。</summary>
        public void Show(SpectateStatus? status)
        {
            _status = status;
            Refresh();
        }

        /// <summary>出している状態を、今の参加者の名前・案内・言語で文にし直して行に写す。</summary>
        public void Refresh()
        {
            _lines.Clear();
            _shownName = null;
            if (_status.HasValue)
            {
                var status = _status.Value;
                var participants = GameSignals.Participants.Value;
                _shownName = CurrentName(status, participants);
                var notice = _model != null ? _model.Notice : HudNotice.None;
                foreach (var line in SpectateText.Lines(status, participants, notice))
                {
                    _lines.Add(Localizer.Resolve(line));
                }
            }

            WriteRows();
        }

        protected override void OnBound(VisualElement element)
        {
            element.Clear();
            _rows.Clear();
            Subscribe();
            WatchModel(GameSignals.HudModel.Value);
            Show(GameSignals.Spectate.Value);
        }

        protected override void OnLanguageChanged()
        {
            Refresh();
        }

        protected override void OnUnbinding(VisualElement element)
        {
            Unsubscribe();
            WatchModel(null);
            element.Clear();
            _rows.Clear();
            element.style.display = DisplayStyle.None;
        }

        private void Start()
        {
            if (Element != null)
            {
                return;
            }

            var document = GetComponent<UIDocument>();
            var root = document != null ? document.rootVisualElement : null;
            if (root != null && root.childCount > 0)
            {
                Bind(root);
            }
        }

        // 参加者の表は出し直さずに中身だけ変わる (名前が後から届く) ので、見ている間は名前だけ毎フレーム読み直す
        private void Update()
        {
            if (Element == null || !_status.HasValue)
            {
                return;
            }

            if (CurrentName(_status.Value, GameSignals.Participants.Value) != _shownName)
            {
                Refresh();
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();
            WatchModel(null);
        }

        private static string CurrentName(SpectateStatus status, IParticipants participants)
        {
            return status.TargetId.HasValue && participants != null && participants.TryGet(status.TargetId.Value, out var participant)
                ? participant.Name
                : null;
        }

        private void WriteRows()
        {
            var element = Element;
            if (element == null)
            {
                return;
            }

            while (_rows.Count < _lines.Count)
            {
                var row = new Label { pickingMode = PickingMode.Ignore };
                row.AddToClassList(RowClass);
                element.Add(row);
                _rows.Add(row);
            }

            while (_rows.Count > _lines.Count)
            {
                var last = _rows.Count - 1;
                _rows[last].RemoveFromHierarchy();
                _rows.RemoveAt(last);
            }

            for (var i = 0; i < _rows.Count; i++)
            {
                if (_rows[i].text != _lines[i])
                {
                    _rows[i].text = _lines[i];
                }

                _rows[i].EnableInClassList(TargetRowClass, i == 0);
            }

            element.style.display = IsShown ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void WatchModel(HudModel model)
        {
            if (_model == model)
            {
                return;
            }

            if (_model != null)
            {
                _model.Changed -= HandleModelChanged;
            }

            _model = model;
            if (_model != null)
            {
                _model.Changed += HandleModelChanged;
            }
        }

        private void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            GameSignals.Spectate.Changed += HandleSpectateChanged;
            GameSignals.Participants.Changed += HandleParticipantsChanged;
            GameSignals.HudModel.Changed += HandleHudModelChanged;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            GameSignals.Spectate.Changed -= HandleSpectateChanged;
            GameSignals.Participants.Changed -= HandleParticipantsChanged;
            GameSignals.HudModel.Changed -= HandleHudModelChanged;
            _subscribed = false;
        }

        // Edit Mode では OnDestroy が呼ばれずに消えることがあるので、消えた後に届いた通知で購読を外す
        private bool ForgetIfDestroyed()
        {
            if (this != null)
            {
                return false;
            }

            Unsubscribe();
            WatchModel(null);
            return true;
        }

        private void HandleSpectateChanged(SpectateStatus? status)
        {
            if (!ForgetIfDestroyed())
            {
                Show(status);
            }
        }

        private void HandleParticipantsChanged(IParticipants participants)
        {
            if (!ForgetIfDestroyed() && _status.HasValue)
            {
                Refresh();
            }
        }

        private void HandleHudModelChanged(HudModel model)
        {
            if (ForgetIfDestroyed())
            {
                return;
            }

            WatchModel(model);
            if (_status.HasValue)
            {
                Refresh();
            }
        }

        private void HandleModelChanged(HudField field)
        {
            if (!ForgetIfDestroyed() && field == HudField.Notice && _status.HasValue)
            {
                Refresh();
            }
        }
    }
}
