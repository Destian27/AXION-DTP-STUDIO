using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Tombol gerak layar sentuh (Android/iOS) untuk scene Gameplay.
/// Dibuat lewat kode, jadi tidak perlu mengubah scene/prefab.
/// Kiri = mundur (setara tombol A), kanan = maju (setara tombol D).
/// Hanya aktif di perangkat mobile (Application.isMobilePlatform, termasuk Device Simulator).
/// </summary>
public class MobileControls : MonoBehaviour
{
    private const int SortingOrder = 100;      // di bawah Loading Screen (999)
    private const float ButtonSize = 220f;     // satuan canvas 1920x1080
    private const float EdgeMargin = 60f;
    private const float VerticalOffset = -90f; // dari tengah layar, di atas baris slot ally/ability
    private const float IdleAlpha = 0.75f;
    private const float PressedAlpha = 1f;

    private static MobileControls _instance;

    private CanvasGroup _group;
    private RectTransform _safeArea;
    private Rect _lastSafeArea;
    private Vector2Int _lastScreenSize;
    private Sprite _leftSprite;
    private Sprite _rightSprite;

    public static void Create()
    {
        if (!Application.isMobilePlatform) return;
        if (_instance != null) return;

        if (EventSystem.current == null)
            Debug.LogWarning("MobileControls: tidak ada EventSystem di scene, tombol sentuh tidak akan merespons.");

        new GameObject(
            "Mobile Controls",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
            typeof(GraphicRaycaster), typeof(MobileControls));
    }

    private void Awake()
    {
        _instance = this;

        var canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;

        var scaler = GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(UIUtils.ScreenWidth, UIUtils.ScreenHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        var safeGo = new GameObject("Safe Area", typeof(RectTransform), typeof(CanvasGroup));
        safeGo.transform.SetParent(transform, false);
        _safeArea = safeGo.GetComponent<RectTransform>();
        _group = safeGo.GetComponent<CanvasGroup>();
        ApplySafeArea();

        _leftSprite = MakeButtonSprite(pointRight: false);
        _rightSprite = MakeButtonSprite(pointRight: true);

        CreateButton("Backward Button", _leftSprite, left: true, held => GameInput.TouchBackwardHeld = held);
        CreateButton("Forward Button", _rightSprite, left: false, held => GameInput.TouchForwardHeld = held);

        SetVisible(false);
    }

    private void OnDestroy()
    {
        GameInput.ResetTouch();
        if (_leftSprite != null) { Destroy(_leftSprite.texture); Destroy(_leftSprite); }
        if (_rightSprite != null) { Destroy(_rightSprite.texture); Destroy(_rightSprite); }
        if (_instance == this) _instance = null;
    }

    private void OnApplicationPause(bool paused)
    {
        // Jari yang masih menempel saat app pindah ke background tidak boleh bikin hero jalan terus.
        if (paused) GameInput.ResetTouch();
    }

    private void Update()
    {
        if (Screen.safeArea != _lastSafeArea || Screen.width != _lastScreenSize.x || Screen.height != _lastScreenSize.y)
            ApplySafeArea();

        var gm = GameplayManager.instance;
        bool show = SceneLoader.hasFinishedLoading && gm != null && gm.waveStarted && !gm.paused;
        SetVisible(show);
    }

    private void SetVisible(bool visible)
    {
        _group.alpha = visible ? 1f : 0f;
        _group.blocksRaycasts = visible;
        _group.interactable = visible;
        if (!visible) GameInput.ResetTouch();
    }

    private void ApplySafeArea()
    {
        _lastSafeArea = Screen.safeArea;
        _lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        if (Screen.width <= 0 || Screen.height <= 0) return;

        Vector2 min = _lastSafeArea.position;
        Vector2 max = _lastSafeArea.position + _lastSafeArea.size;
        min.x /= Screen.width; min.y /= Screen.height;
        max.x /= Screen.width; max.y /= Screen.height;

        _safeArea.anchorMin = min;
        _safeArea.anchorMax = max;
        _safeArea.offsetMin = Vector2.zero;
        _safeArea.offsetMax = Vector2.zero;
    }

    private void CreateButton(string objectName, Sprite sprite, bool left, System.Action<bool> onHeldChanged)
    {
        var go = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(HoldButton));
        go.transform.SetParent(_safeArea, false);

        var rect = go.GetComponent<RectTransform>();
        float anchorX = left ? 0f : 1f;
        rect.anchorMin = new Vector2(anchorX, 0.5f);
        rect.anchorMax = new Vector2(anchorX, 0.5f);
        rect.pivot = new Vector2(anchorX, 0.5f);
        rect.sizeDelta = new Vector2(ButtonSize, ButtonSize);
        rect.anchoredPosition = new Vector2(left ? EdgeMargin : -EdgeMargin, VerticalOffset);

        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = new Color(1f, 1f, 1f, IdleAlpha);
        image.raycastTarget = true;

        go.GetComponent<HoldButton>().Init(image, onHeldChanged);
    }

    // ---- Tombol tahan (multi-touch aman: tiap tombol melacak jarinya sendiri) ----
    private class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private Image _image;
        private System.Action<bool> _onHeldChanged;
        private bool _held;
        private int _pointerId;

        public void Init(Image image, System.Action<bool> onHeldChanged)
        {
            _image = image;
            _onHeldChanged = onHeldChanged;
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (_held) return;
            _pointerId = e.pointerId;
            SetHeld(true);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (_held && e.pointerId == _pointerId) SetHeld(false);
        }

        public void OnPointerExit(PointerEventData e)
        {
            // Jari digeser keluar dari tombol = lepas.
            if (_held && e.pointerId == _pointerId) SetHeld(false);
        }

        private void OnDisable()
        {
            if (_held) SetHeld(false);
        }

        private void SetHeld(bool held)
        {
            _held = held;
            _onHeldChanged?.Invoke(held);
            if (_image != null)
            {
                _image.color = new Color(1f, 1f, 1f, held ? PressedAlpha : IdleAlpha);
                transform.localScale = held ? new Vector3(0.92f, 0.92f, 1f) : Vector3.one;
            }
        }
    }

    // ---- Sprite panah dibuat lewat kode (tanpa aset/font tambahan) ----
    private static Sprite MakeButtonSprite(bool pointRight)
    {
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        var pixels = new Color32[size * size];

        float c = (size - 1) / 2f;
        float radius = size / 2f - 2f;
        float dir = pointRight ? 1f : -1f;

        // Segitiga panah (koordinat relatif terhadap pusat).
        Vector2 tip = new Vector2(dir * size * 0.26f, 0f);
        Vector2 b1 = new Vector2(-dir * size * 0.16f, size * 0.24f);
        Vector2 b2 = new Vector2(-dir * size * 0.16f, -size * 0.24f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - c, dy = y - c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);

                float discA = Mathf.Clamp01(radius - d + 0.5f) * 0.45f;           // isi gelap transparan
                float ringA = Mathf.Clamp01(radius - d + 0.5f) *
                              Mathf.Clamp01(d - (radius - 6f) + 0.5f) * 0.9f;     // cincin putih
                float arrowA = TriangleCoverage(dx, dy, tip, b1, b2) * 0.95f;     // panah putih

                // Susun: isi gelap -> cincin putih -> panah putih (alpha-over).
                float a = discA;
                float col = 0f;
                a = Over(ref col, a, 1f, ringA);
                a = Over(ref col, a, 1f, arrowA);

                byte v = (byte)Mathf.RoundToInt(Mathf.Clamp01(col) * 255f);
                pixels[y * size + x] = new Color32(v, v, v, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    // Alpha-over untuk warna abu (satu kanal). Mengembalikan alpha baru, memperbarui warna.
    private static float Over(ref float dstCol, float dstA, float srcCol, float srcA)
    {
        float outA = srcA + dstA * (1f - srcA);
        if (outA <= 0f) { dstCol = 0f; return 0f; }
        dstCol = (srcCol * srcA + dstCol * dstA * (1f - srcA)) / outA;
        return outA;
    }

    // Anti-alias sederhana: 3x3 sampel per piksel.
    private static float TriangleCoverage(float px, float py, Vector2 a, Vector2 b, Vector2 c)
    {
        int hits = 0;
        for (int sy = 0; sy < 3; sy++)
        {
            for (int sx = 0; sx < 3; sx++)
            {
                var p = new Vector2(px + (sx - 1) / 3f, py + (sy - 1) / 3f);
                if (PointInTriangle(p, a, b, c)) hits++;
            }
        }
        return hits / 9f;
    }

    private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Sign(p, a, b), d2 = Sign(p, b, c), d3 = Sign(p, c, a);
        bool hasNeg = d1 < 0 || d2 < 0 || d3 < 0;
        bool hasPos = d1 > 0 || d2 > 0 || d3 > 0;
        return !(hasNeg && hasPos);
    }

    private static float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
        => (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
}
