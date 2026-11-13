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
    /// <see cref="HudView"/> が Label に写す。毎フレーム読みに行くのは、時間で進みイベントの無いリロードの進み
    /// (<see cref="HudModel.RefreshReloadProgress"/>) と照準の広がり・表示 (<see cref="RefreshCrosshair"/>) だけ。
    /// Health や Weapon が作り直されたとき (<see cref="Damageable.HealthReset"/> / <see cref="WeaponController.WeaponChanged"/>) は購読し直す。
    /// オンライン (Fps.Net の NetHud) は源を渡さずに作り、<see cref="Model"/> の Set 系で値を書く。チームの点数 (左上) と
    /// 試合結果 (中央上) はそのときだけ出る。
    /// ヒットマーカーなど HUD の部分ごとのビュー (<see cref="HudElementView"/>) は <see cref="Create"/> が同じ GameObject に付け
    /// (<see cref="AddViews"/>)、View を作り直すたびに Hud.uxml の要素を渡し直す。
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

        /// <summary><see cref="Create"/> が作る PanelSettings の名前。</summary>
        public const string PanelSettingsName = "HudPanelSettings";

        /// <summary>HUD の基準解像度。Hud.uss の寸法はこの解像度で決めてあり、画面の大きさに合わせて拡縮する。</summary>
        public static readonly Vector2Int ReferenceResolution = new Vector2Int(1920, 1080);

        private readonly HudModel _model = new HudModel();
        private HudView _view;
        private Damageable _player;
        private WeaponController _weapon;
        private AimDownSights _ads;
        private PlayerController _mover;
        private PanelSettings _ownedPanelSettings;
        private Color _alphaColor = Color.white;
        private Color _bravoColor = Color.white;
        private bool _followsPalette;
        private Team? _paletteViewer;
        private ColorPalette _paintedPalette;
        private HudVisibility _visibility = HudVisibility.Full;

        /// <summary>表示中の文字列。</summary>
        public HudModel Model => _model;

        /// <summary>Label に写している View。UIDocument の木がまだ無ければ null。</summary>
        public HudView View => _view;

        /// <summary>この HUD 用に作り、破棄時に一緒に消す PanelSettings。無ければ null。</summary>
        public PanelSettings OwnedPanelSettings => _ownedPanelSettings;

        /// <summary>
        /// HUD 用の PanelSettings と UIDocument を持つ GameObject を objectName で作り、Resources の Hud.uxml とテーマを読み込んで
        /// <see cref="Initialize"/> で源を渡す。源はどれも null 可 (Set 系で <see cref="Model"/> に直接書く場合)。
        /// UIDocument が有効になる前に参照をすべて設定するため、GameObject は無効で作ってから有効にする。
        /// 失敗したら作りかけのものは消して例外を投げる。
        /// </summary>
        /// <exception cref="InvalidOperationException">Resources にレイアウトかテーマが無い。</exception>
        public static Hud Create(string objectName, Damageable player, WeaponController weapon, Match match)
        {
            var layout = Resources.Load<VisualTreeAsset>(LayoutResourcePath);
            if (layout == null)
            {
                throw new InvalidOperationException(
                    $"HUD layout 'Resources/{LayoutResourcePath}.uxml' was not found (expected under Assets/game/UI/Resources)");
            }

            var theme = Resources.Load<ThemeStyleSheet>(ThemeResourcePath);
            if (theme == null)
            {
                throw new InvalidOperationException(
                    $"HUD theme 'Resources/{ThemeResourcePath}.tss' was not found (expected under Assets/game/UI/Resources)");
            }

            PanelSettings panelSettings = null;
            GameObject hudObject = null;
            try
            {
                panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                panelSettings.name = PanelSettingsName;
                panelSettings.themeStyleSheet = theme;
                panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
                panelSettings.referenceResolution = ReferenceResolution;
                panelSettings.match = 0.5f;

                hudObject = new GameObject(objectName);
                hudObject.SetActive(false);
                var document = hudObject.AddComponent<UIDocument>();
                document.panelSettings = panelSettings;
                document.visualTreeAsset = layout;

                var hud = hudObject.AddComponent<Hud>();
                AddViews(hudObject);
                hud.Initialize(player, weapon, match, panelSettings);

                // 有効にすると UIDocument が木を作り、Play 中は Hud の OnEnable が Label を探す。
                // Edit Mode では Hud の OnEnable が走らないので、木ができていればここで探す。
                hudObject.SetActive(true);
                if (!Application.isPlaying)
                {
                    hud.RebuildView();
                }

                return hud;
            }
            catch
            {
                DestroySafely(hudObject);
                DestroySafely(panelSettings);
                throw;
            }
        }

        /// <summary>
        /// HUD の部分ごとのビュー (<see cref="HitMarkerView"/>・<see cref="DamageDirectionView"/>・<see cref="KillFeedView"/>・
        /// <see cref="SubtitleView"/>・<see cref="ScoreboardView"/>・<see cref="LoadoutSelectView"/>・<see cref="TutorialPromptView"/>・<see cref="ModeHudView"/>) と <see cref="PauseMenu"/>・その設定画面 (<see cref="PauseSettingsView"/>) を
        /// hudObject に付ける。
        /// オフライン (<see cref="Create"/>) とオンライン (Fps.Net の NetHud は <see cref="Create"/> を通る) で同じものを付ける。
        /// 既に付いているものは付け直さない。
        /// </summary>
        public static void AddViews(GameObject hudObject)
        {
            if (hudObject == null)
            {
                throw new ArgumentNullException(nameof(hudObject));
            }

            AddIfMissing<HitMarkerView>(hudObject);
            AddIfMissing<DamageDirectionView>(hudObject);
            AddIfMissing<KillFeedView>(hudObject);
            AddIfMissing<SubtitleView>(hudObject);
            AddIfMissing<ScoreboardView>(hudObject);
            AddIfMissing<LoadoutSelectView>(hudObject);
            AddIfMissing<TutorialPromptView>(hudObject);
            AddIfMissing<ModeHudView>(hudObject);
            AddIfMissing<PauseMenu>(hudObject);
            // PauseMenu の後に付けると、木を作り直したときも PauseMenu が「設定」のボタンを持ってから開く処理を渡せる。
            AddIfMissing<PauseSettingsView>(hudObject);
        }

        /// <summary>同じ GameObject のヒットマーカー。<see cref="AddViews"/> で付けていなければ null。</summary>
        public HitMarkerView HitMarker => GetComponent<HitMarkerView>();

        /// <summary>同じ GameObject の被弾方向。<see cref="AddViews"/> で付けていなければ null。</summary>
        public DamageDirectionView DamageDirection => GetComponent<DamageDirectionView>();

        /// <summary>同じ GameObject のキルフィード。<see cref="AddViews"/> で付けていなければ null。</summary>
        public KillFeedView KillFeed => GetComponent<KillFeedView>();

        /// <summary>同じ GameObject の音の合図の字幕。<see cref="AddViews"/> で付けていなければ null。</summary>
        public SubtitleView Subtitles => GetComponent<SubtitleView>();

        /// <summary>同じ GameObject のスコアボード。<see cref="AddViews"/> で付けていなければ null。</summary>
        public ScoreboardView Scoreboard => GetComponent<ScoreboardView>();

        /// <summary>同じ GameObject のロードアウト選択。<see cref="AddViews"/> で付けていなければ null。</summary>
        public LoadoutSelectView LoadoutSelect => GetComponent<LoadoutSelectView>();

        /// <summary>同じ GameObject のチュートリアルの案内。<see cref="AddViews"/> で付けていなければ null。</summary>
        public TutorialPromptView TutorialPrompt => GetComponent<TutorialPromptView>();

        /// <summary>同じ GameObject のオフラインのモードの表示。<see cref="AddViews"/> で付けていなければ null。</summary>
        public ModeHudView ModeHud => GetComponent<ModeHudView>();

        /// <summary>同じ GameObject のポーズメニュー。<see cref="AddViews"/> で付けていなければ null。</summary>
        public PauseMenu PauseMenu => GetComponent<PauseMenu>();

        /// <summary>同じ GameObject のポーズ画面の設定画面。<see cref="AddViews"/> で付けていなければ null。</summary>
        public PauseSettingsView PauseSettings => GetComponent<PauseSettingsView>();

        /// <summary>
        /// チームの点数・スコアボードの見出し・キルログの名前・拠点制圧の欄を塗る色を決める。View を作り直しても引き継ぐ。
        /// 色の組に従わせていた (<see cref="UseTeamPalette"/>) ときはやめ、この色のままにする。
        /// </summary>
        public void SetTeamColors(Color alpha, Color bravo)
        {
            StopFollowingPalette();
            ApplyTeamColors(alpha, bravo);
        }

        /// <summary>
        /// viewer のチームから見た敵味方の色 (<see cref="TeamColors"/>) で塗り、設定の色の組が変わったら塗り直す。
        /// viewer が null (自分のチームがまだ無い) なら Alpha を味方の色にする。チームが変わったら呼び直す。
        /// </summary>
        public void UseTeamPalette(Team? viewer)
        {
            _paletteViewer = viewer;
            if (!_followsPalette)
            {
                AppliedControlSettings.Changed += HandleSettingsChanged;
                _followsPalette = true;
            }

            ApplyPaletteColors();
        }

        /// <summary>色の組に従っている (<see cref="UseTeamPalette"/>) か。</summary>
        public bool FollowsTeamPalette => _followsPalette;

        /// <summary>team の点数を塗る色。<see cref="SetTeamColors"/> の前は白。</summary>
        public Color TeamColor(Team team)
        {
            return team == Team.Bravo ? _bravoColor : _alphaColor;
        }

        private void ApplyTeamColors(Color alpha, Color bravo)
        {
            _alphaColor = alpha;
            _bravoColor = bravo;
            _view?.SetTeamColors(alpha, bravo);

            var scoreboard = Scoreboard;
            if (scoreboard != null)
            {
                scoreboard.RefreshTeamColors();
            }

            var killFeed = KillFeed;
            if (killFeed != null)
            {
                killFeed.RefreshTeamColors();
            }

            var domination = GetComponent<DominationView>();
            if (domination != null)
            {
                domination.RefreshTeamColors();
            }
        }

        private void ApplyPaletteColors()
        {
            var palette = TeamColors.Current;
            _paintedPalette = palette.Kind;
            ApplyTeamColors(
                TeamColors.Of(palette, Team.Alpha, _paletteViewer),
                TeamColors.Of(palette, Team.Bravo, _paletteViewer));
        }

        private void HandleSettingsChanged(ControlSettings settings)
        {
            // 破棄の後に届いた通知では消えた要素を塗らない。感度などほかの設定だけが変わったときも、キルログを作り直さない
            if (this == null || settings.Palette == _paintedPalette)
            {
                return;
            }

            ApplyPaletteColors();
        }

        private void StopFollowingPalette()
        {
            if (_followsPalette)
            {
                AppliedControlSettings.Changed -= HandleSettingsChanged;
                _followsPalette = false;
            }
        }

        /// <summary>今出している項目。<see cref="ApplyDensity"/> の前は <see cref="HudVisibility.Full"/>。</summary>
        public HudVisibility Visibility => _visibility;

        /// <summary>
        /// HUD の量 (<see cref="GameRules.HudDensity"/>) に合わせて出す項目を切り替える (<see cref="HudVisibility.For"/>)。
        /// オフラインは GameBootstrap とキャンペーンがルールを決めたときに、オンラインは NetHud がサーバーのルールが届いたときに呼ぶ。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">density が定義済みの値でない。</exception>
        public void ApplyDensity(HudDensity density)
        {
            ApplyVisibility(HudVisibility.For(density));
        }

        /// <summary>
        /// 出す項目を visibility に替える。弾数の書式 (<see cref="HudModel.SetReserveAmmoShown"/>)、Label の欄 (<see cref="HudView"/>)、
        /// 部分ごとのビュー (<see cref="HitMarkerView"/>・<see cref="DamageDirectionView"/>・<see cref="KillFeedView"/>) に効かせる。
        /// View を作り直しても引き継ぐ。
        /// </summary>
        public void ApplyVisibility(HudVisibility visibility)
        {
            if (visibility == null)
            {
                throw new ArgumentNullException(nameof(visibility));
            }

            var changed = visibility != _visibility;
            _visibility = visibility;
            _model.SetReserveAmmoShown(visibility.ReserveAmmo);
            _view?.ApplyVisibility(visibility);
            ApplyVisibilityToElementViews();

            if (changed)
            {
                Debug.Log($"{LogTag} density={visibility.Density}", this);
            }
        }

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
            _ads = weapon != null ? weapon.GetComponentInParent<AimDownSights>(true) : null;
            _mover = weapon != null ? weapon.GetComponentInParent<PlayerController>(true) : null;
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
            RefreshCrosshair();

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

            _view = new HudView(root, _model, _visibility);
            _view.SetTeamColors(_alphaColor, _bravoColor);
            ApplyVisibilityToElementViews();
            foreach (var view in GetComponents<HudElementView>())
            {
                view.Bind(root);
            }

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

        private void LateUpdate()
        {
            // WeaponController を渡さない HUD (NetHud) は Set 系で進みや照準を書くので、ここで読み直して消さない。
            if (_weapon != null)
            {
                _model.RefreshReloadProgress();
                RefreshCrosshair();
            }
        }

        /// <summary>
        /// 武器の今のばらつき (<see cref="WeaponController.SpreadFraction"/>) を照準の広がりにし、ADS 中
        /// (<see cref="AimDownSights.IsAiming"/>) とダッシュ中 (<see cref="PlayerController.IsSprinting"/>) は照準を隠す。
        /// 武器を渡していなければ広がり 0 で出す。値の源は <see cref="Initialize"/> の weapon と、その親の階層の ADS とプレイヤー。
        /// </summary>
        public void RefreshCrosshair()
        {
            if (_weapon == null)
            {
                _model.ResetCrosshair();
                return;
            }

            var aiming = _ads != null && _ads.IsAiming;
            var sprinting = _mover != null && _mover.IsSprinting;
            _model.SetCrosshair(Mathf.Clamp01(_weapon.SpreadFraction), !aiming && !sprinting);
        }

        private void TryRebuildViewAndLog()
        {
            try
            {
                if (RebuildView())
                {
                    Debug.Log($"{LogTag} ready hp={_model.HealthText} weapon={_model.WeaponNameText} ammo={_model.AmmoText} time={_model.TimeText}", this);
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
            StopFollowingPalette();
            DisposeView();
            UnsubscribeSources();
            _model.UnbindAll();

            if (_ownedPanelSettings != null)
            {
                Destroy(_ownedPanelSettings);
                _ownedPanelSettings = null;
            }
        }

        private static void DestroySafely(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private void DisposeView()
        {
            foreach (var view in GetComponents<HudElementView>())
            {
                // 破棄の途中では先に消えたビューが null として返ることがある。
                if (view != null)
                {
                    view.Unbind();
                }
            }

            if (_view == null)
            {
                return;
            }

            _view.Dispose();
            _view = null;
        }

        // スコアボード・ロードアウト選択・ポーズは自分で開くものなので、HUD の量では隠さない
        private void ApplyVisibilityToElementViews()
        {
            var hitMarker = HitMarker;
            if (hitMarker != null)
            {
                hitMarker.SetAllowed(_visibility.HitMarker);
            }

            var damageDirection = DamageDirection;
            if (damageDirection != null)
            {
                damageDirection.SetAllowed(_visibility.DamageDirection);
            }

            var killFeed = KillFeed;
            if (killFeed != null)
            {
                killFeed.SetAllowed(_visibility.KillFeed);
            }

            // グレネードの数は持っている弾の一種なので、弾数と一緒に出し入れする。
            var grenades = GetComponent<GrenadeCountView>();
            if (grenades != null)
            {
                grenades.SetAllowed(_visibility.Ammo);
            }
        }

        private static void AddIfMissing<T>(GameObject target)
            where T : Component
        {
            if (target.GetComponent<T>() == null)
            {
                target.AddComponent<T>();
            }
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
            _ads = null;
            _mover = null;
        }
    }
}
