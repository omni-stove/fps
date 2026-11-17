using System;
using Shooter.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    /// <summary>
    /// 能力の枠ごとの回数と待ち時間 (<see cref="HudText.Ability"/>) を、体力の上に 1 枠 1 行で出す。今押すと何ができるか (<see cref="AbilityHost.TryGetFollowUp"/>、置いた C4 の起爆など) があれば、
    /// その案内 (<see cref="FollowUpText"/>) を行に添える。Hud.uxml の <see cref="UxmlName"/> に行を入れる。
    /// <see cref="Hud.AddViews"/> が最初から付け、出すものが来るまで隠しておく。要素を持っている間は <see cref="GameSignals.LocalPlayer"/> を購読し、
    /// 自分のプレイヤーの <see cref="AbilityHost"/> を出し、プレイヤーが消されたら隠す。要素を受けたときは今のプレイヤーに合わせる。
    /// 出している枠は <see cref="AbilityHost.Changed"/> で読み直す。
    /// ルールで能力を使わない間と、枠がすべて空の間は隠し、空の枠の行も隠す。枠だけで見て使えない (待ち時間・使用中・回数切れ) 枠の行には
    /// <see cref="WaitingRowClass"/> を付ける。
    /// HUD の量では弾数と同じ扱いで、弾数を出さない量 (<see cref="HudVisibility.Ammo"/> が false) では隠す (<see cref="Hud"/> が決める)。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AbilitySlotsView : HudElementView
    {
        /// <summary>Hud.uxml の能力の枠の VisualElement。</summary>
        public const string UxmlName = "ability-slots";

        /// <summary>枠 1 つ分の行の Label に付ける USS クラス。</summary>
        public const string RowClass = "ability-slots__row";

        /// <summary>今は使えない枠の行に足す USS クラス。</summary>
        public const string WaitingRowClass = "ability-slots__row--waiting";

        /// <summary>枠の行に今押すと何ができるかの案内 (<see cref="FollowUpText"/>) を添えるときの書式。{0} が枠の行、{1} が案内。</summary>
        public const string WithHintKey = "hud.ability.withHint";

        /// <summary>置いた物を起爆できるときの案内 (<see cref="AbilityFollowUpKind.Detonate"/>)。{0} が置いた物の名前、{1} が置いている数。</summary>
        public const string DetonateKey = "hud.remoteCharge.detonate";

        private readonly string[] _texts = new string[AbilityHost.SlotCount];
        private readonly bool[] _waiting = new bool[AbilityHost.SlotCount];
        private readonly Label[] _rows = new Label[AbilityHost.SlotCount];
        private AbilityHost _source;
        private bool _subscribed;

        // 出している枠が GameSignals から来たか。来ていれば、要素を持っていない間にプレイヤーが消されたことを、受けたときに隠して合わせる
        private bool _showsSignal;

        public override string ElementName => UxmlName;

        /// <summary>出している枠の持ち主。出していなければ null。</summary>
        public AbilityHost Source => _source;

        /// <summary>何か出しているか (ルールで能力を使い、空でない枠がある)。</summary>
        public bool IsShown { get; private set; }

        /// <summary>
        /// hud に (無ければ) このビューを付けて返す。付けたばかりなら Hud に木から要素を渡し直させる。
        /// UXML に要素が無ければエラーをログに出し、要素を持たないまま返す (文字列は <see cref="Text"/> で読める)。
        /// </summary>
        public static AbilitySlotsView AttachTo(Hud hud)
        {
            if (hud == null)
            {
                throw new ArgumentNullException(nameof(hud));
            }

            var view = hud.GetComponent<AbilitySlotsView>();
            if (view != null)
            {
                return view;
            }

            view = hud.gameObject.AddComponent<AbilitySlotsView>();
            try
            {
                hud.RebuildView();
            }
            catch (InvalidOperationException e)
            {
                Debug.LogError($"{Hud.LogTag} could not show the ability slots on the HUD: {e.Message}", hud);
            }

            return view;
        }

        /// <summary>index の枠の行に出している文字列。出していなければ空文字。</summary>
        /// <exception cref="ArgumentOutOfRangeException">index が 0 以上 <see cref="AbilityHost.SlotCount"/> 未満でない。</exception>
        public string Text(int index)
        {
            RequireIndex(index);
            return _texts[index] ?? string.Empty;
        }

        /// <summary>index の枠を今は使えない (行に <see cref="WaitingRowClass"/> を付けている) か。</summary>
        /// <exception cref="ArgumentOutOfRangeException">index が 0 以上 <see cref="AbilityHost.SlotCount"/> 未満でない。</exception>
        public bool IsWaiting(int index)
        {
            RequireIndex(index);
            return _waiting[index];
        }

        /// <summary>host の枠を出し始める。前に出していたものは手放す。null なら出すのをやめる。</summary>
        public void Show(AbilityHost host)
        {
            _showsSignal = false;
            Watch(host);
        }

        /// <summary>
        /// followUp の案内。置いた物を呼ぶ名前は <see cref="AbilityFollowUp.TargetNameKey"/> の表から引き、キーが空なら <see cref="AbilityFollowUp.TargetId"/> にする。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">followUp の種類に案内が無い。</exception>
        public static LocalizedText FollowUpText(AbilityFollowUp followUp)
        {
            switch (followUp.Kind)
            {
                case AbilityFollowUpKind.Detonate:
                    return new LocalizedText(DetonateKey, TargetName(followUp), followUp.Count);
                default:
                    throw new ArgumentOutOfRangeException(nameof(followUp), followUp.Kind, "no hint for this follow-up kind");
            }
        }

        private static LocalizedText TargetName(AbilityFollowUp followUp)
        {
            var key = followUp.TargetNameKey ?? string.Empty;
            return key.Length > 0 ? new LocalizedText(key) : LocalizedText.Literal(followUp.TargetId ?? string.Empty);
        }

        /// <summary>出している枠の今の回数と待ち時間を読み直して行に写す。</summary>
        public void Refresh()
        {
            // 破棄された持ち主は Unity の == null で拾い、出していないものとして扱う。
            var host = _source != null ? _source : null;
            var shown = false;
            for (var i = 0; i < AbilityHost.SlotCount; i++)
            {
                var slot = host != null && host.UsesAbilities ? host.Slot(i) : null;
                _texts[i] = slot != null ? Localizer.Resolve(Describe(host, i, slot)) : string.Empty;
                // 能力ごとの CanUse は毎フレーム聞かない (重い判定やログを出すものがあり得る)。枠の回数と時間だけで決める。
                _waiting[i] = slot != null && !slot.CanStart;
                shown |= slot != null;
                WriteRow(i, slot != null);
            }

            IsShown = shown;
            if (Element != null)
            {
                Element.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        protected override void OnBound(VisualElement element)
        {
            element.Clear();
            for (var i = 0; i < AbilityHost.SlotCount; i++)
            {
                var row = new Label { pickingMode = PickingMode.Ignore };
                row.AddToClassList(RowClass);
                element.Add(row);
                _rows[i] = row;
            }

            Subscribe();
            var player = GameSignals.LocalPlayer.Value;
            if (player != null || _showsSignal)
            {
                ShowPlayer(player);
                return;
            }

            Refresh();
        }

        protected override void OnLanguageChanged()
        {
            Refresh();
        }

        protected override void OnUnbinding(VisualElement element)
        {
            Unsubscribe();
            element.Clear();
            element.style.display = DisplayStyle.None;
            Array.Clear(_rows, 0, _rows.Length);
        }

        private void OnDestroy()
        {
            Unsubscribe();
            if (!ReferenceEquals(_source, null))
            {
                _source.Changed -= HandleHostChanged;
                _source = null;
            }
        }

        private void Watch(AbilityHost host)
        {
            if (!ReferenceEquals(_source, null))
            {
                _source.Changed -= HandleHostChanged;
            }

            _source = host;
            if (!ReferenceEquals(_source, null))
            {
                _source.Changed += HandleHostChanged;
            }

            Refresh();
        }

        private void ShowPlayer(LocalPlayerSignal player)
        {
            Watch(player?.Abilities);
            _showsSignal = true;
        }

        private void Subscribe()
        {
            if (!_subscribed)
            {
                GameSignals.LocalPlayer.Changed += HandleLocalPlayerChanged;
                _subscribed = true;
            }
        }

        private void Unsubscribe()
        {
            if (_subscribed)
            {
                GameSignals.LocalPlayer.Changed -= HandleLocalPlayerChanged;
                _subscribed = false;
            }
        }

        private void HandleLocalPlayerChanged(LocalPlayerSignal player)
        {
            // Edit Mode では OnDestroy が呼ばれずに消えることがあるので、消えた後に届いた通知で購読を外す
            if (this == null)
            {
                GameSignals.LocalPlayer.Changed -= HandleLocalPlayerChanged;
                return;
            }

            ShowPlayer(player);
        }

        private void HandleHostChanged(AbilityHost host)
        {
            if (this == null)
            {
                host.Changed -= HandleHostChanged;
                return;
            }

            Refresh();
        }

        private static LocalizedText Describe(AbilityHost host, int index, AbilitySlot slot)
        {
            var line = HudText.Ability(index + 1, slot);
            return host.TryGetFollowUp(index, out var followUp) ? new LocalizedText(WithHintKey, line, FollowUpText(followUp)) : line;
        }

        private void WriteRow(int index, bool visible)
        {
            var row = _rows[index];
            if (row == null)
            {
                return;
            }

            if (row.text != _texts[index])
            {
                row.text = _texts[index];
            }

            row.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            row.EnableInClassList(WaitingRowClass, _waiting[index]);
        }

        private static void RequireIndex(int index)
        {
            if (index < 0 || index >= AbilityHost.SlotCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, $"index must be between 0 and {AbilityHost.SlotCount - 1}");
            }
        }
    }
}
