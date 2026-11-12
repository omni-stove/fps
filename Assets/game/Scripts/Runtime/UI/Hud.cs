using System;
using Fps.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Fps.Runtime
{
    /// <summary>
    /// HUD (左下に体力、右下に弾倉/予備、上中央に残り時間とその下に一時的な案内、中央に照準)。同じ GameObject の <see cref="UIDocument"/> に出す。
    /// 値の源 (プレイヤーの <see cref="Damageable"/>・<see cref="WeaponController"/>・<see cref="Fps.Core.Match"/>) は
    /// <see cref="Initialize"/> でコードから渡す。値はそれぞれのイベントで <see cref="HudModel"/> が更新し、
    /// <see cref="HudView"/> が Label に写す。毎フレームは読みに行かない。
    /// Health や Weapon が作り直されたとき (<see cref="Damageable.HealthReset"/> / <see cref="WeaponController.WeaponChanged"/>) は購読し直す。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class Hud : MonoBehaviour
    {
        public const string LogTag = "[HUD]";

        /// <summary>Resources からの HUD のレイアウト (Assets/game/UI/Resources/Hud/Hud.uxml)。</summary>
        public const string LayoutResourcePath = "Hud/Hud";

        /// <summary>Resources からのランタイムテーマ (Assets/game/UI/Resources/Hud/HudTheme.tss)。</summary>
        public const string ThemeResourcePath = "Hud/HudTheme";

        private readonly HudModel _model = new HudModel();
        private HudView _view;
        private Damageable _player;
        private WeaponController _weapon;
        private PanelSettings _ownedPanelSettings;

        /// <summary>表示中の文字列。</summary>
        public HudModel Model => _model;

        /// <summary>Label に写している View。UIDocument の木がまだ無ければ null。</summary>
        public HudView View => _view;

        /// <summary>
        /// 値の源を渡して購読する。どれも null 可 (その項目は仮の表示になる)。前に渡した源の購読は外す。
        /// UIDocument の木が既にあればすぐ Label に反映する。
        /// </summary>
        /// <param name="player">プレイヤーの体力。</param>
        /// <param name="weapon">プレイヤーの武器。未初期化 (Edit Mode など) なら初期化されたときに購読する。</param>
        /// <param name="match">残り時間を出す試合。null なら後で <see cref="BindMatch"/> で渡す。</param>
        /// <param name="ownedPanelSettings">この HUD 用に作った PanelSettings。渡すと破棄時に一緒に消す。null 可。</param>
        public void Initialize(Damageable player, WeaponController weapon, Match match, PanelSettings ownedPanelSettings = null)
        {
            UnsubscribeSources();

            _player = player;
            _weapon = weapon;
            _ownedPanelSettings = ownedPanelSettings;

            if (_player != null)
            {
                _player.HealthReset += _model.BindHealth;
            }

            if (_weapon != null)
            {
                _weapon.WeaponChanged += _model.BindWeapon;
            }

            _model.BindHealth(_player != null ? _player.Health : null);
            _model.BindWeapon(_weapon != null ? _weapon.Weapon : null);
            _model.BindMatch(match);

            RebuildView();
        }

        /// <summary>残り時間を出す試合を差し替える。null なら仮の表示に戻す。<see cref="MatchRunner"/> が試合を始めたときに呼ぶ。</summary>
        public void BindMatch(Match match)
        {
            _model.BindMatch(match);
        }

        /// <summary>
        /// 一時的な案内の源を差し替える。null なら案内を消す。<see cref="CampaignRunner"/> がキャンペーンを始めたときに呼び、
        /// 止めるときに null で外す。
        /// </summary>
        public void BindNotice(CampaignNotice notice)
        {
            _model.BindNotice(notice);
        }

        /// <summary>
        /// UIDocument の今の木から Label を探し直して View を作る。UIDocument は有効にし直すたびに木を作り直すので、
        /// そのたびに呼ぶ (Play 中は OnEnable で呼ぶ)。木がまだ無ければ何もせず false。
        /// UXML に必要な要素が無ければ <see cref="InvalidOperationException"/> を投げる。
        /// </summary>
        public bool RebuildView()
        {
            DisposeView();

            var document = GetComponent<UIDocument>();
            var root = document != null ? document.rootVisualElement : null;
            if (root == null || root.childCount == 0)
            {
                return false;
            }

            _view = new HudView(root, _model);
            return true;
        }

        private void OnEnable()
        {
            TryRebuildViewAndLog();
        }

        private void Start()
        {
            // OnEnable の時点で UIDocument の木がまだ無かった場合の取りこぼしを拾う。
            if (_view == null)
            {
                TryRebuildViewAndLog();
            }

            if (_view == null)
            {
                Debug.LogError($"{LogTag} the UIDocument has no visual tree; check its PanelSettings and source asset", this);
            }
        }

        private void TryRebuildViewAndLog()
        {
            try
            {
                if (RebuildView())
                {
                    Debug.Log($"{LogTag} ready hp={_model.HealthText} ammo={_model.AmmoText} time={_model.TimeText}", this);
                }
            }
            catch (InvalidOperationException e)
            {
                Debug.LogError($"{LogTag} could not bind the HUD layout: {e.Message}", this);
            }
        }

        private void OnDisable()
        {
            DisposeView();
        }

        private void OnDestroy()
        {
            DisposeView();
            UnsubscribeSources();
            _model.UnbindAll();

            if (_ownedPanelSettings != null)
            {
                Destroy(_ownedPanelSettings);
                _ownedPanelSettings = null;
            }
        }

        private void DisposeView()
        {
            if (_view == null)
            {
                return;
            }

            _view.Dispose();
            _view = null;
        }

        private void UnsubscribeSources()
        {
            if (_player != null)
            {
                _player.HealthReset -= _model.BindHealth;
            }

            if (_weapon != null)
            {
                _weapon.WeaponChanged -= _model.BindWeapon;
            }

            _player = null;
            _weapon = null;
        }
    }
}
