using System;
using Fps.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Fps.Runtime
{
    /// <summary>
    /// 能力の枠ごとの回数と待ち時間 (<see cref="HudFormat.Ability"/>) を、体力の上に 1 枠 1 行で出す。Hud.uxml の <see cref="UxmlName"/> に行を入れる。
    /// <see cref="Hud.AddViews"/> では付けず、オフラインの <see cref="GameBootstrap"/> が <see cref="IHud.AttachAbilitySlots"/> で付けて
    /// プレイヤーの <see cref="AbilityHost"/> を <see cref="Show"/> する。待ち時間は時間で減りイベントが無いので、毎フレーム読み直す。
    /// ルールで能力を使わない間と、枠がすべて空の間は隠し、空の枠の行も隠す。枠だけで見て使えない (待ち時間・使用中・回数切れ) 枠の行には
    /// <see cref="WaitingRowClass"/> を付ける。
    /// HUD の量では弾数と同じ扱いで、弾数を出さない量 (<see cref="HudVisibility.Ammo"/> が false) では隠す (<see cref="Hud"/> が決める)。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AbilitySlotsView : HudElementView, IAbilitySlots
    {
        /// <summary>Hud.uxml の能力の枠の VisualElement。</summary>
        public const string UxmlName = "ability-slots";

        /// <summary>枠 1 つ分の行の Label に付ける USS クラス。</summary>
        public const string RowClass = "ability-slots__row";

        /// <summary>今は使えない枠の行に足す USS クラス。</summary>
        public const string WaitingRowClass = "ability-slots__row--waiting";

        private readonly string[] _texts = new string[AbilityHost.SlotCount];
        private readonly bool[] _waiting = new bool[AbilityHost.SlotCount];
        private readonly Label[] _rows = new Label[AbilityHost.SlotCount];
        private AbilityHost _source;

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
            _source = host;
            Refresh();
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
                _texts[i] = slot != null ? Localizer.Resolve(HudFormat.Ability(i + 1, slot)) : string.Empty;
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

            Refresh();
        }

        protected override void OnLanguageChanged()
        {
            Refresh();
        }

        protected override void OnUnbinding(VisualElement element)
        {
            element.Clear();
            element.style.display = DisplayStyle.None;
            Array.Clear(_rows, 0, _rows.Length);
        }

        private void LateUpdate()
        {
            if (_source != null)
            {
                Refresh();
            }
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
