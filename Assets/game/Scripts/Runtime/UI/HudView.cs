using System;
using Fps.Core;
using UnityEngine.UIElements;

namespace Fps.Runtime
{
    /// <summary>
    /// <see cref="HudModel"/> の文字列を Hud.uxml の Label に写す。<see cref="HudModel.Changed"/> が来たときだけ書き換え、
    /// 毎フレームは何もしない。表示の判断 (書式・いつ変わるか) はすべて HudModel が持つ。
    /// </summary>
    public sealed class HudView : IDisposable
    {
        /// <summary>Hud.uxml の体力の Label。</summary>
        public const string HealthLabelName = "hud-health";

        /// <summary>Hud.uxml の弾数の Label。</summary>
        public const string AmmoLabelName = "hud-ammo";

        /// <summary>Hud.uxml の残り時間の Label。</summary>
        public const string TimeLabelName = "hud-time";

        /// <summary>Hud.uxml の照準。</summary>
        public const string CrosshairName = "hud-crosshair";

        private readonly HudModel _model;
        private bool _disposed;

        public Label HealthLabel { get; }
        public Label AmmoLabel { get; }
        public Label TimeLabel { get; }
        public VisualElement Crosshair { get; }

        /// <summary>
        /// root の下から Label と照準を名前で探し、今のモデルの文字列を入れてから変更を購読する。
        /// 見つからなければ <see cref="InvalidOperationException"/> を投げる (UXML と名前がずれている)。
        /// </summary>
        public HudView(VisualElement root, HudModel model)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            _model = model ?? throw new ArgumentNullException(nameof(model));
            HealthLabel = Require<Label>(root, HealthLabelName);
            AmmoLabel = Require<Label>(root, AmmoLabelName);
            TimeLabel = Require<Label>(root, TimeLabelName);
            Crosshair = Require<VisualElement>(root, CrosshairName);

            HealthLabel.text = _model.HealthText;
            AmmoLabel.text = _model.AmmoText;
            TimeLabel.text = _model.TimeText;
            _model.Changed += OnChanged;
        }

        /// <summary>モデルの購読を外す。Label の文字列はそのまま残る。</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _model.Changed -= OnChanged;
            _disposed = true;
        }

        private void OnChanged(HudField field)
        {
            switch (field)
            {
                case HudField.Health:
                    HealthLabel.text = _model.HealthText;
                    break;
                case HudField.Ammo:
                    AmmoLabel.text = _model.AmmoText;
                    break;
                case HudField.Time:
                    TimeLabel.text = _model.TimeText;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(field), field, "field is not a valid HudField");
            }
        }

        private static T Require<T>(VisualElement root, string elementName)
            where T : VisualElement
        {
            var element = root.Q<T>(elementName);
            if (element == null)
            {
                throw new InvalidOperationException(
                    $"the HUD layout has no {typeof(T).Name} named '{elementName}'; check Assets/game/UI/Resources/Hud/Hud.uxml");
            }

            return element;
        }
    }
}
