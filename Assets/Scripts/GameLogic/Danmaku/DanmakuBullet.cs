using UnityEngine;

namespace DemoFrameWork.GameLogic.Danmaku
{
    /// <summary>
    /// 弹幕子弹（敌方）MonoBehaviour 组件，挂在子弹预制体上。
    /// 对应参考 Bullet 基类：Update/Kill、位置/速度、碰撞圆、图层。
    /// </summary>
    public class DanmakuBullet : MonoBehaviour
    {
        [SerializeField] private string _bulletSpritePath = "Demos/Danmaku/Img/bullet/0/0";

        [Tooltip("预制体根节点 scale 过大时（如旧预制体 108），压到合理尺寸以便在相机内可见")]
        [SerializeField] private float _maxAcceptableRootScale = 10f;

        [Tooltip("当根 scale 超过上限时，使用此统一缩放（世界单位下约可见）")]
        [SerializeField] private float _normalizedRootScale = 0.35f;

        [Tooltip("敌弹 Sprite 视觉缩放（相对世界单位）；长条形弹幕用；圆形弹幕由「屏幕像素边长」决定")]
        [SerializeField] private float _bulletVisualScale = 2.05f;

        [Tooltip(
            "圆形弹幕（宽高比≤下方阈值）在屏幕上的目标最大边长（像素）。长条形弹幕不受本值直接换算，但会按本值÷基准(32)比例放大/缩小显示。")]
        [SerializeField] private float _roundBulletMaxScreenPixels = 50f;

        [Tooltip("宽高比(长/宽)超过此值视为长条形：主用「敌弹 Sprite 视觉缩放」，并乘上 (圆形像素目标÷32) 系数")]
        [SerializeField] private float _elongatedAspectThreshold = 1.5f;

        /// <summary>与「圆形弹」像素归一化对齐的基准；改 _roundBulletMaxScreenPixels 时长条形也会同比变化。</summary>
        private const float ReferenceRoundBulletScreenPixels = 32f;

        /// <summary>与关卡/敌机默认弹径一致；<see cref="HitRadius"/> 相对本值的比会乘到根缩放，使大图与大判定一致。</summary>
        private const float ReferenceHitRadiusForVisualScale = 0.06f;

        // ---- 运行时状态 ----
        [HideInInspector] public bool IsAlive;

        /// <summary>速度方向（归一化）。</summary>
        [HideInInspector] public Vector2 Direction;

        /// <summary>速度大小（Unity 单位/秒）。</summary>
        [HideInInspector] public float Speed;

        /// <summary>碰撞圆半径（Unity 单位）。</summary>
        [HideInInspector] public float HitRadius = 0.05f;

        /// <summary>图层（对应参考 layer），用于区分是否被炸弹清除。</summary>
        [HideInInspector] public int Layer;

        /// <summary>屏幕外额外存活帧数（对应 minLiveOutScreen），0 = 出界即销毁。</summary>
        [HideInInspector] public int MinLiveOutScreen;

        [Header("判定可视化（弹幕极多时建议关；也可开 BulletManager 上「绘制敌弹」）")]
        [SerializeField] private bool _drawHitRadiusGizmo;
        [SerializeField] private Color _hitRadiusGizmoColor = new Color(1f, 0.92f, 0.2f, 0.75f);

        private int _outOfBoundsFrames;
        private BulletManager _manager;
        private SpriteRenderer _spriteRenderer;
        private Vector2 _lastVisualUpDir;

        private static Camera _cachedOrthoCam;
        private static int _cachedCamFrame = -1;
        private static int _cachedScreenHeight;
        private static float _cachedOrthoSize;

        /// <summary>本弹是否已贡献过擦弹（每弹最多一次）。</summary>
        public bool GrazeConsumed { get; private set; }

        private void Awake()
        {
            _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        private SpriteRenderer EnsureSpriteRenderer()
        {
            if (_spriteRenderer == null)
                _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (_spriteRenderer == null)
                _spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            return _spriteRenderer;
        }

        /// <param name="resolvedBulletStyleFolderIndex">Resources 下 bullet 样式子目录编号（≥0）。</param>
        /// <param name="resolvedSpriteVariantIndex">同目录下贴图编号（0、1、2…）。</param>
        public void Init(BulletManager manager, Vector2 direction, float speed, float hitRadius, int layer,
            int minLiveOutScreen, int resolvedBulletStyleFolderIndex, int resolvedSpriteVariantIndex)
        {
            _manager = manager;
            Direction = direction.normalized;
            Speed = speed;
            HitRadius = hitRadius;
            Layer = layer;
            MinLiveOutScreen = minLiveOutScreen;
            IsAlive = true;
            _outOfBoundsFrames = 0;
            GrazeConsumed = false;
            _lastVisualUpDir = Vector2.zero;

            ApplyBulletSpriteFromResources(resolvedBulletStyleFolderIndex, resolvedSpriteVariantIndex);
        }

        public void MarkGrazeConsumed() => GrazeConsumed = true;

        public void LogicUpdate(float dt, Rect playArea)
        {
            if (!IsAlive) return;

            transform.position += (Vector3)(Direction * Speed * dt);

            // 旋转贴图朝向运动方向（方向未变时跳过，减轻大量同向弹的 Transform 写入）
            if (Direction != Vector2.zero)
            {
                const float rotEpsilon = 1e-4f;
                if (Vector2.Dot(_lastVisualUpDir, Direction) < 1f - rotEpsilon)
                {
                    transform.up = Direction;
                    _lastVisualUpDir = Direction;
                }
            }

            // 越界判定
            Vector2 pos = transform.position;
            if (!playArea.Contains(pos))
            {
                _outOfBoundsFrames++;
                if (_outOfBoundsFrames > MinLiveOutScreen)
                    Kill();
            }
            else
            {
                _outOfBoundsFrames = 0;
            }
        }

        public void Kill()
        {
            if (!IsAlive) return;
            IsAlive = false;
            _manager?.RecycleBullet(this);
        }

        public DanmakuCircle GetCircle()
        {
            Vector2 pos = transform.position;
            return new DanmakuCircle(pos.x, pos.y, HitRadius);
        }

        private void ApplyBulletSpriteFromResources(int styleFolder, int variantIndex)
        {
            var sprite = DanmakuBulletResourceIndex.TryGetSprite(styleFolder, variantIndex);
            if (sprite == null)
                sprite = DanmakuBulletResourceIndex.TryGetSprite(styleFolder, 0);
            if (sprite == null && styleFolder != 0)
                sprite = DanmakuBulletResourceIndex.TryGetSprite(0, 0);
            if (sprite == null && !string.IsNullOrEmpty(_bulletSpritePath))
                sprite = DanmakuSpriteUtil.TryLoadSprite(_bulletSpritePath);

            if (sprite != null)
            {
                var sr = EnsureSpriteRenderer();
                sr.sprite = sprite;
            }

            bool huge = transform.localScale.x > _maxAcceptableRootScale ||
                        transform.localScale.y > _maxAcceptableRootScale;

            float uniform;
            float pixelSizeMul = _roundBulletMaxScreenPixels / Mathf.Max(1f, ReferenceRoundBulletScreenPixels);
            if (sprite != null)
            {
                float rw = sprite.rect.width / Mathf.Max(sprite.pixelsPerUnit, 1e-4f);
                float rh = sprite.rect.height / Mathf.Max(sprite.pixelsPerUnit, 1e-4f);
                float aspect = Mathf.Max(rw, rh) / Mathf.Max(Mathf.Min(rw, rh), 1e-4f);
                bool elongated = aspect > _elongatedAspectThreshold;

                if (elongated)
                {
                    uniform = (huge ? _normalizedRootScale * _bulletVisualScale : _bulletVisualScale) * pixelSizeMul;
                }
                else
                {
                    float maxDim = Mathf.Max(rw, rh);
                    float targetWorld = GetWorldUnitsForScreenPixels(_roundBulletMaxScreenPixels);
                    uniform = targetWorld / Mathf.Max(maxDim, 1e-4f);
                }
            }
            else
            {
                uniform = (huge ? _normalizedRootScale * _bulletVisualScale : _bulletVisualScale) * pixelSizeMul;
            }

            float hitMul = Mathf.Max(0.01f, HitRadius) / Mathf.Max(1e-4f, ReferenceHitRadiusForVisualScale);
            uniform *= hitMul;

            transform.localScale = Vector3.one * uniform;
            if (_spriteRenderer != null)
                DanmakuSpriteUtil.ApplyGameplaySortingToRenderer(_spriteRenderer);
        }

        private static void RefreshOrthoCameraCacheForFrame()
        {
            int f = Time.frameCount;
            if (_cachedCamFrame == f)
                return;
            _cachedCamFrame = f;
            var cam = Camera.main;
            _cachedOrthoCam = cam;
            _cachedScreenHeight = Screen.height;
            _cachedOrthoSize = cam != null && cam.orthographic ? cam.orthographicSize : 0f;
        }

        private static float GetWorldUnitsForScreenPixels(float pixels)
        {
            RefreshOrthoCameraCacheForFrame();
            if (_cachedOrthoCam != null && _cachedOrthoCam.orthographic)
                return pixels * (2f * _cachedOrthoSize) / Mathf.Max(_cachedScreenHeight, 1);
            return pixels * 10f / 1080f;
        }

        private void OnDrawGizmos()
        {
            if (!_drawHitRadiusGizmo) return;
            if (!IsAlive) return;
            DanmakuGizmoDraw.WireCircleXY(transform.position, HitRadius, _hitRadiusGizmoColor);
        }
    }
}
