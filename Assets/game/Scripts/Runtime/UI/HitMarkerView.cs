using Fps.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Fps.Runtime
{
    /// <summary>
    /// 弾が当たったことを照準の周りに出すビュー。Hud.uxml の <see cref="UxmlName"/> を使う。
    /// <see cref="CombatEvents.HitConfirmed"/> を受けるたびに <see cref="DisplaySeconds"/> 秒だけ印を出し、
    /// 頭に当てたら <see cref="HeadClass"/>、倒したら <see cref="KillClass"/> を付ける (大きさは Hud.uss が決め、頭の線の色は設定の色の組の強調の色)。
    /// <see cref="PresentationCatalog"/> に音があれば小さく鳴らし、無ければ見た目だけ出す。
    /// オフラインとオンラインで同じ HUD (<see cref="Hud.AddViews"/>) に付き、どちらも同じイベントを受ける。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HitMarkerView : HudElementView, IHitMarker
    {
        /// <summary>Hud.uxml のヒットマーカーの要素。</summary>
        public const string UxmlName = "hit-marker";

        /// <summary>頭に当てたときに要素に付けるクラス。</summary>
        public const string HeadClass = "hit-marker--head";

        /// <summary>倒したときに要素に付けるクラス。</summary>
        public const string KillClass = "hit-marker--kill";

        /// <summary>印の 4 本の線の USS クラス。頭に当てたときはこの線を色の組の強調の色で塗る。</summary>
        public const string LineClass = "hit-marker__line";

        /// <summary>1 回の命中で印を出しておく秒数。</summary>
        public const float DisplaySeconds = 0.15f;

        private float _remaining;
        private bool _headshot;
        private bool _killed;
        private PresentationCatalog _catalog;
        private bool _catalogResolved;
        private AudioSource _audio;

        public override string ElementName => UxmlName;

        /// <summary>今印を出しているか。</summary>
        public bool IsShowing => _remaining > 0f;

        /// <summary>印を消すまでの残りの秒数。出していなければ 0。</summary>
        public float Remaining => _remaining;

        /// <summary>最後の命中が頭だったか。</summary>
        public bool IsHeadshot => _headshot;

        /// <summary>最後の命中で倒したか。</summary>
        public bool IsKill => _killed;

        /// <summary>
        /// 音を引く設定。初めて読んだときに <see cref="PresentationCatalog.Load"/> で探し、無ければ null (音を鳴らさない)。
        /// 代入すると探さずにそれを使う (null で音を止める)。
        /// </summary>
        public PresentationCatalog Catalog
        {
            get
            {
                if (!_catalogResolved)
                {
                    _catalog = PresentationCatalog.Load();
                    _catalogResolved = true;
                }

                return _catalog;
            }
            set
            {
                _catalog = value;
                _catalogResolved = true;
            }
        }

        private void OnEnable()
        {
            CombatEvents.HitConfirmed += Show;
        }

        private void OnDisable()
        {
            CombatEvents.HitConfirmed -= Show;
            _remaining = 0f;
            Apply();
        }

        private void Update()
        {
            // ポーズ中に出ていた印が残り続けないよう、時間の止まらない経過で消す。
            Tick(Time.unscaledDeltaTime);
        }

        /// <summary>
        /// confirm の命中の印を出し直し、音があれば鳴らす。出している途中でも残りの秒数を戻す。
        /// HUD の量で出さない (<see cref="HudElementView.IsAllowed"/> が false) ときは、音も含めて何もしない。
        /// </summary>
        public void Show(HitConfirm confirm)
        {
            // 音だけ鳴らすと、ハードコアで隠した命中の手応えが音で伝わってしまうため
            if (!IsAllowed)
            {
                return;
            }

            _remaining = DisplaySeconds;
            _headshot = confirm.Zone == HitZone.Head;
            _killed = confirm.Killed;
            Apply();
            PlaySound(confirm.Killed);
        }

        /// <summary>deltaSeconds 秒進め、残りが無くなったら印を消す。0 以下か有限でない値では進めない。</summary>
        public void Tick(float deltaSeconds)
        {
            if (_remaining <= 0f || !(deltaSeconds > 0f) || float.IsInfinity(deltaSeconds))
            {
                return;
            }

            _remaining -= deltaSeconds;
            if (_remaining <= 0f)
            {
                _remaining = 0f;
                Apply();
            }
        }

        protected override void OnBound(VisualElement element)
        {
            Apply();
        }

        protected override void OnAllowedChanged(bool allowed)
        {
            if (!allowed)
            {
                _remaining = 0f;
                Apply();
            }
        }

        private void Apply()
        {
            var element = Element;
            if (element == null)
            {
                return;
            }

            element.EnableInClassList(HeadClass, _headshot);
            element.EnableInClassList(KillClass, _killed);
            ApplyLineColor(element);
            element.style.display = IsShowing ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>
        /// 頭に当てた印の線を、今の色の組の強調の色 (<see cref="TeamColors.Highlight"/>) で塗る。頭でなければ Hud.uss の色に戻す。
        /// 出している間に組を変えても、次の命中で塗り直す。
        /// </summary>
        private void ApplyLineColor(VisualElement element)
        {
            // 消すときに設定を読みに行かない
            if (_headshot && !IsShowing)
            {
                return;
            }

            StyleColor color = _headshot ? new StyleColor(TeamColors.Highlight) : new StyleColor(StyleKeyword.Null);
            element.Query<VisualElement>(className: LineClass).ForEach(line => line.style.backgroundColor = color);
        }

        private void PlaySound(bool killed)
        {
            var catalog = Catalog;
            if (catalog == null)
            {
                return;
            }

            var clip = catalog.HitMarkerSoundFor(killed);
            if (clip == null)
            {
                return;
            }

            if (_audio == null)
            {
                _audio = gameObject.AddComponent<AudioSource>();
                _audio.playOnAwake = false;
                _audio.spatialBlend = 0f;
            }

            _audio.PlayOneShot(clip, catalog.HitMarkerVolume * AppliedControlSettings.Current.EffectsVolume);
        }
    }
}
