using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Affiche toutes les informations dans l'image de score du backglass
/// (score, meilleurs scores, joueur, billes et météo). Aucun HUD additionnel :
/// seuls les retours d'impact et la modale de fin de partie restent en surimpression.
/// La typographie et les ressorts suivent les principes Apple (hiérarchie,
/// mouvement fluide, retenue).
/// </summary>
public sealed class ScoreboardUI : MonoBehaviour
{
    private const float ReferenceWidth = 1205f;
    private const float ReferenceHeight = 678f;

    private static readonly Color Gold = new Color(0.92f, 0.76f, 0.43f, 1f);
    private static readonly Color GoldMuted = new Color(0.74f, 0.63f, 0.45f, 1f);
    private static readonly Color WarmWhite = new Color(1f, 0.97f, 0.9f, 1f);
    private static readonly Color CoolBlue = new Color(0.62f, 0.82f, 1f, 1f);
    private static readonly Color Muted = new Color(0.66f, 0.6f, 0.5f, 1f);
    private static readonly Color PanelTop = new Color(0.085f, 0.07f, 0.055f, 0.985f);
    private static readonly Color PanelBottom = new Color(0.032f, 0.027f, 0.022f, 0.985f);
    private static readonly Color PanelStroke = new Color(0.95f, 0.8f, 0.5f, 0.55f);

    private PinballGameManager manager;
    private Canvas canvas;

    private TMP_FontAsset displayFont;
    private TMP_FontAsset labelFont;
    private TMP_FontAsset bodyFont;

    private Sprite panelSprite;
    private Sprite pillSprite;
    private Sprite circleSprite;

    private TextMeshProUGUI plaqueScore;
    private TextMeshProUGUI plaquePlayer;
    private TextMeshProUGUI plaqueTop1;
    private TextMeshProUGUI plaqueTop2;
    private TextMeshProUGUI plaqueStatus;

    private RectTransform popupLayer;
    private readonly List<TextMeshProUGUI> popupPool = new List<TextMeshProUGUI>();
    private int popupCursor;

    private RectTransform modalRoot;
    private CanvasGroup modalGroup;
    private Image modalDim;
    private RectTransform modalPanel;
    private Image modalPanelImage;
    private TextMeshProUGUI modalTitle;
    private TextMeshProUGUI modalSubtitle;
    private TextMeshProUGUI modalFinalLabel;
    private TextMeshProUGUI modalFinalValue;
    private TextMeshProUGUI modalTopLines;
    private Image modalPromptShadow;
    private Image modalPromptPill;
    private TextMeshProUGUI modalPromptText;
    private TextMeshProUGUI modalPromptHint;
    private RectTransform flakeLayer;
    private readonly List<RectTransform> flakes = new List<RectTransform>();
    private readonly List<float> flakeSpeeds = new List<float>();
    private readonly List<float> flakePhases = new List<float>();

    private RectTransform nameRoot;
    private CanvasGroup nameGroup;
    private Image nameDim;
    private RectTransform namePanel;
    private TMP_InputField nameInput;
    private Toggle funToggle;
    private Image funToggleBox;
    private GameObject funToggleCheckmark;
    private TextMeshProUGUI funToggleLabel;
    private float namePanelScale = 0.94f;
    private float namePanelScaleVelocity;
    private float nameDimAlpha;
    private float nameDimAlphaVelocity;
    private float nameElapsed;
    private float nameCloseTimer;
    private bool nameVisible;
    private bool nameClosing;
    private bool nameFocusPending;

    private bool built;
    private bool gameOverVisible;
    private float restartEnabledAt;
    private float modalElapsed;
    private int modalTargetScore;

    private float panelScale = 0.94f;
    private float panelScaleVelocity;
    private float panelOffset = -10f;
    private float panelOffsetVelocity;
    private float titleFade;
    private float titleFadeVelocity;
    private float dimAlpha;
    private float dimAlphaVelocity;

    private int lastScore;
    private float scoreFlash;
    private bool lastComboActive;
    private int lastComboValue;
    private string plaqueMessage;
    private float plaqueMessageUntil;

    private struct Plaque
    {
        public float u0;
        public float v0;
        public float u1;
        public float v1;
        public string id;

        public Plaque(string id, float u0, float v0, float u1, float v1)
        {
            this.id = id;
            this.u0 = u0;
            this.v0 = v0;
            this.u1 = u1;
            this.v1 = v1;
        }
    }

    private static readonly Plaque[] Plaques =
    {
        new Plaque("top1", 0.049f, 0.727f, 0.254f, 0.840f),
        new Plaque("score", 0.291f, 0.656f, 0.713f, 0.891f),
        new Plaque("top2", 0.746f, 0.727f, 0.953f, 0.840f),
        new Plaque("player", 0.049f, 0.098f, 0.254f, 0.211f),
        new Plaque("status", 0.746f, 0.098f, 0.953f, 0.211f)
    };

    public void Bind(PinballGameManager gameManager)
    {
        if (manager == gameManager)
        {
            return;
        }

        if (manager != null)
        {
            manager.ScoreChanged -= RefreshScore;
            manager.StateChanged -= Refresh;
            manager.ToastRequested -= ShowPlaqueMessage;
        }

        manager = gameManager;

        if (manager != null)
        {
            manager.ScoreChanged += RefreshScore;
            manager.StateChanged += Refresh;
            manager.ToastRequested += ShowPlaqueMessage;
        }
    }

    private void OnDestroy()
    {
        if (manager != null)
        {
            manager.ScoreChanged -= RefreshScore;
            manager.StateChanged -= Refresh;
            manager.ToastRequested -= ShowPlaqueMessage;
        }
    }

    private void Start()
    {
        Build();
        Refresh();
        ShowNameEntry();
    }

    private void Update()
    {
        if (!built)
        {
            return;
        }

        if (manager != null && manager.IsGameOver && gameOverVisible && !nameVisible)
        {
            bool restart = (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                || (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);

            if (restart && Time.unscaledTime >= restartEnabledAt)
            {
                manager.RestartGame();
                ShowNameEntry();
            }
        }

        UpdateScoreFlash();
        UpdateComboState();
        UpdatePlaqueMessage();
        UpdateModal();

        if (nameVisible)
        {
            UpdateNameModal();
        }
    }

    private void Build()
    {
        canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        LoadFonts();
        BuildSprites();
        HideLegacyTexts();

        Camera camera = Camera.main;
        MeshFilter scoreMesh = FindScoreMesh();
        float scale = Mathf.Max(0.001f, canvas.scaleFactor);

        foreach (Plaque plaque in Plaques)
        {
            BuildPlaque(plaque, camera, scoreMesh, scale);
        }

        popupLayer = CreateRect("Popups", canvas.transform);
        Stretch(popupLayer);

        BuildModal();
        BuildNameModal();
        built = true;
    }

    // ------------------------------------------------------------------ fonts
    private void LoadFonts()
    {
        TMP_FontAsset fallback = FindLegacyFont();
        displayFont = CreateRuntimeFont("Fonts/Barlow-Bold", fallback);
        labelFont = CreateRuntimeFont("Fonts/Barlow-SemiBold", fallback);
        bodyFont = CreateRuntimeFont("Fonts/Barlow-Regular", fallback);
    }

    private static TMP_FontAsset CreateRuntimeFont(string resourcePath, TMP_FontAsset fallback)
    {
        Font font = Resources.Load<Font>(resourcePath);
        if (font == null)
        {
            return fallback;
        }

        TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(font);
        if (asset == null)
        {
            return fallback;
        }

        asset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
        asset.name = resourcePath + " Runtime";
        return asset;
    }

    private TMP_FontAsset FindLegacyFont()
    {
        GameObject sample = GameObject.Find("Score Actuel");
        if (sample != null)
        {
            TMP_Text text = sample.GetComponent<TMP_Text>();
            if (text != null && text.font != null)
            {
                return text.font;
            }
        }

        return TMP_Settings.defaultFontAsset;
    }

    // ----------------------------------------------------------------- sprites
    private void BuildSprites()
    {
        panelSprite = CreateRoundedSprite("PanelRounded", 16, 2, 18);
        pillSprite = CreateSolidRoundedSprite("PillRounded", 18, 18, Color.white);
        circleSprite = CreateCircleSprite("Circle", 32);
    }

    private static Sprite CreateRoundedSprite(string name, int radius, int border, int slice)
    {
        const int size = 72;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = name;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < size; y++)
        {
            float vertical = y / (size - 1f);
            Color fill = Color.Lerp(PanelBottom, PanelTop, vertical);
            Color stroke = PanelStroke;

            for (int x = 0; x < size; x++)
            {
                float px = x + 0.5f - size * 0.5f;
                float py = y + 0.5f - size * 0.5f;
                float half = size * 0.5f - 1f;
                float qx = Mathf.Abs(px) - (half - radius);
                float qy = Mathf.Abs(py) - (half - radius);
                float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
                float distance = Mathf.Min(Mathf.Max(qx, qy), 0f) + outside - radius;

                float alpha = Mathf.Clamp01(0.5f - distance);
                if (alpha <= 0f)
                {
                    texture.SetPixel(x, y, Color.clear);
                    continue;
                }

                bool isBorder = border > 0 && distance > -border;
                Color color = isBorder ? stroke : fill;
                color.a *= alpha;
                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply();
        return Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(slice, slice, slice, slice));
    }

    private static Sprite CreateSolidRoundedSprite(string name, int radius, int slice, Color fill)
    {
        const int size = 72;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = name;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = x + 0.5f - size * 0.5f;
                float py = y + 0.5f - size * 0.5f;
                float half = size * 0.5f - 1f;
                float qx = Mathf.Abs(px) - (half - radius);
                float qy = Mathf.Abs(py) - (half - radius);
                float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
                float distance = Mathf.Min(Mathf.Max(qx, qy), 0f) + outside - radius;
                float alpha = Mathf.Clamp01(0.5f - distance);

                if (alpha <= 0f)
                {
                    texture.SetPixel(x, y, Color.clear);
                    continue;
                }

                Color color = fill;
                color.a *= alpha;
                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply();
        return Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(slice, slice, slice, slice));
    }

    private static Sprite CreateCircleSprite(string name, int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = name;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01((1f - distance) * size * 0.5f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
    // -------------------------------------------------------------- backglass
    private void BuildPlaque(Plaque plaque, Camera camera, MeshFilter scoreMesh, float scale)
    {
        Rect screenRect = ComputePlaqueRect(plaque, camera, scoreMesh);
        if (screenRect.width <= 1f || screenRect.height <= 1f)
        {
            return;
        }

        float width = Mathf.Min(screenRect.width, screenRect.height);
        float height = Mathf.Max(screenRect.width, screenRect.height);
        float referenceWidth = width / scale;

        bool big = plaque.id == "score";
        TMP_FontAsset font = big ? displayFont : labelFont;

        TextMeshProUGUI text = CreateText("UI_" + plaque.id, canvas.transform, string.Empty, 12f, font, WarmWhite);
        RectTransform rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(
            screenRect.center.x / Mathf.Max(1f, camera != null ? camera.pixelWidth : ReferenceWidth),
            screenRect.center.y / Mathf.Max(1f, camera != null ? camera.pixelHeight : ReferenceHeight));
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(height / scale * 0.9f, width / scale * 0.9f);
        rect.localEulerAngles = new Vector3(0f, 0f, -90f);

        bool scorePlaque = plaque.id == "top1" || plaque.id == "top2";
        float maxFontSize = referenceWidth * (big ? 0.34f : scorePlaque ? 0.28f : 0.32f);
        text.fontSize = maxFontSize;
        text.fontSizeMin = Mathf.Min(maxFontSize, Mathf.Max(8f, maxFontSize * 0.35f));
        text.fontSizeMax = maxFontSize;
        text.enableAutoSizing = true;
        text.alignment = TextAlignmentOptions.Center;
        text.overflowMode = TextOverflowModes.Truncate;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.lineSpacing = big ? -8f : -4f;
        text.characterSpacing = big ? -1.5f : 1f;

        if (big)
        {
            AddUnderlay(text, new Color(0f, 0f, 0f, 0.55f), 0.22f, 2f);
        }

        switch (plaque.id)
        {
            case "score":
                plaqueScore = text;
                break;
            case "player":
                plaquePlayer = text;
                break;
            case "top1":
                plaqueTop1 = text;
                break;
            case "top2":
                plaqueTop2 = text;
                break;
            case "status":
                plaqueStatus = text;
                break;
        }
    }

    private Rect ComputePlaqueRect(Plaque plaque, Camera camera, MeshFilter scoreMesh)
    {
        if (camera == null || scoreMesh == null || scoreMesh.sharedMesh == null)
        {
            return new Rect(Vector2.zero, Vector2.zero);
        }

        Bounds bounds = scoreMesh.sharedMesh.bounds;

        Vector3 ToScreen(float u, float v)
        {
            Vector3 local = new Vector3(
                bounds.min.x + u * bounds.size.x,
                bounds.center.y,
                bounds.min.z + v * bounds.size.z);
            return camera.WorldToScreenPoint(scoreMesh.transform.TransformPoint(local));
        }

        Vector3 a = ToScreen(plaque.u0, plaque.v0);
        Vector3 b = ToScreen(plaque.u1, plaque.v0);
        Vector3 c = ToScreen(plaque.u0, plaque.v1);
        Vector3 d = ToScreen(plaque.u1, plaque.v1);

        float xMin = Mathf.Min(Mathf.Min(a.x, b.x), Mathf.Min(c.x, d.x));
        float xMax = Mathf.Max(Mathf.Max(a.x, b.x), Mathf.Max(c.x, d.x));
        float yMin = Mathf.Min(Mathf.Min(a.y, b.y), Mathf.Min(c.y, d.y));
        float yMax = Mathf.Max(Mathf.Max(a.y, b.y), Mathf.Max(c.y, d.y));

        return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
    }

    // --------------------------------------------------------------- refresh
    private void Refresh()
    {
        if (!built || manager == null)
        {
            return;
        }

        RefreshScore();

        if (plaquePlayer != null)
        {
            plaquePlayer.text = "<size=60%><color=#C9A24B>JOUEUR</color></size>\n<size=125%>"
                + ShortName(manager.PlayerName) + "</size>";
        }

        if (plaqueTop1 != null)
        {
            plaqueTop1.text = "<size=58%><color=#C9A24B>TOP 1</color></size>\n<size=125%>"
                + ShortName(manager.Top1.name) + " " + FormatScore(manager.Top1.score) + "</size>";
        }

        if (plaqueTop2 != null)
        {
            plaqueTop2.text = "<size=58%><color=#C9A24B>TOP 2</color></size>\n<size=125%>"
                + ShortName(manager.Top2.name) + " " + FormatScore(manager.Top2.score) + "</size>";
        }

        UpdateStatusPlaque();

        lastComboActive = manager.ComboActive;
        lastComboValue = manager.BumperCombo;
    }

    /// <summary>
    /// Mise à jour légère appelée à chaque point : seul le score change.
    /// Les autres plaques ne sont rafraîchies que sur changement d'état.
    /// </summary>
    private void RefreshScore()
    {
        if (!built || manager == null)
        {
            return;
        }

        if (plaqueScore != null)
        {
            plaqueScore.text = manager.HasLaunched || manager.IsGameOver
                ? "<size=55%><color=#C9A24B>SCORE</color></size>\n<size=115%>" + FormatScore(manager.Score) + "</size>"
                : "<size=60%><color=#C9A24B>ESPACE</color></size>\n<size=115%>LANCER</size>";
        }

        if (manager.Score > lastScore)
        {
            scoreFlash = 1f;
        }

        lastScore = manager.Score;
    }

    private void UpdateStatusPlaque()
    {
        if (plaqueStatus == null || manager == null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(plaqueMessage) && Time.unscaledTime < plaqueMessageUntil)
        {
            string[] lines = plaqueMessage.Split('\n');
            string first = lines.Length > 0 ? lines[0] : string.Empty;
            string second = lines.Length > 1 ? lines[1] : string.Empty;
            plaqueStatus.text = "<size=60%><color=#E0A86A>" + first + "</color></size>\n<size=125%>" + second + "</size>";
            return;
        }

        string status;
        if (manager.StormActive)
        {
            status = "<color=#8FD0FF>TEMPÊTE x2</color>";
        }
        else if (manager.ComboActive)
        {
            status = "<color=#FFD27A>COMBO x" + manager.BumperCombo + "</color>";
        }
        else
        {
            status = "<color=#B8AA90>CALME</color>";
        }

        plaqueStatus.text = "<size=58%><color=#C9A24B>BILLES " + manager.Balls + "/"
            + manager.startingBalls + "</color></size>\n<size=125%>" + status + "</size>";
    }

    private void UpdateScoreFlash()
    {
        if (plaqueScore == null)
        {
            return;
        }

        if (scoreFlash > 0f)
        {
            scoreFlash = Mathf.MoveTowards(scoreFlash, 0f, Time.unscaledDeltaTime * 3.2f);
            plaqueScore.color = Color.Lerp(WarmWhite, new Color(1f, 0.88f, 0.55f, 1f), scoreFlash);
            float scale = 1f + 0.012f * scoreFlash;
            plaqueScore.rectTransform.localScale = new Vector3(scale, scale, 1f);
        }
        else if (plaqueScore.color != WarmWhite || plaqueScore.rectTransform.localScale != Vector3.one)
        {
            plaqueScore.color = WarmWhite;
            plaqueScore.rectTransform.localScale = Vector3.one;
        }
    }

    private void UpdateComboState()
    {
        if (manager == null)
        {
            return;
        }

        if (manager.ComboActive != lastComboActive || manager.BumperCombo != lastComboValue)
        {
            lastComboActive = manager.ComboActive;
            lastComboValue = manager.BumperCombo;
            UpdateStatusPlaque();
        }
    }

    private void UpdatePlaqueMessage()
    {
        if (!string.IsNullOrEmpty(plaqueMessage) && Time.unscaledTime >= plaqueMessageUntil)
        {
            plaqueMessage = null;
            UpdateStatusPlaque();
        }
    }

    private void ShowPlaqueMessage(string message)
    {
        if (!built || string.IsNullOrEmpty(message))
        {
            return;
        }

        plaqueMessage = message;
        plaqueMessageUntil = Time.unscaledTime + 1.7f;
        UpdateStatusPlaque();
    }

    // ----------------------------------------------------------------- popups
    public void ShowPopup(int points, Vector3 worldPosition, string label)
    {
        if (!built || canvas == null)
        {
            return;
        }

        Camera camera = Camera.main;
        if (camera == null)
        {
            return;
        }

        Vector3 screen = camera.WorldToScreenPoint(worldPosition);
        if (screen.z < 0f)
        {
            return;
        }

        TextMeshProUGUI popup = GetPopup();
        popup.gameObject.SetActive(true);
        popup.alpha = 1f;
        popup.text = (string.IsNullOrEmpty(label) ? string.Empty : label + "\n") + "+" + points;
        popup.color = manager != null && manager.StormActive ? CoolBlue : Gold;

        RectTransform rect = popup.rectTransform;
        Vector2 reference = new Vector2(
            (screen.x / Mathf.Max(1f, camera.pixelWidth) - 0.5f) * ReferenceWidth,
            (screen.y / Mathf.Max(1f, camera.pixelHeight) - 0.5f) * ReferenceHeight);
        rect.anchoredPosition = reference;
        rect.localScale = Vector3.one * 0.86f;

        StartCoroutine(AnimatePopup(popup, reference));
    }

    private IEnumerator AnimatePopup(TextMeshProUGUI popup, Vector2 start)
    {
        float time = 0f;
        const float duration = 0.8f;

        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / duration);
            float pop = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / 0.18f), 3f);
            popup.rectTransform.anchoredPosition = start + new Vector2(0f, 64f * t);
            popup.alpha = 1f - Mathf.Pow(t, 2f);
            popup.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.86f, 1f, pop);
            yield return null;
        }

        popup.gameObject.SetActive(false);
    }

    private TextMeshProUGUI GetPopup()
    {
        if (popupPool.Count < 16)
        {
            TextMeshProUGUI popup = CreateText("Popup", popupLayer, string.Empty, 34f, displayFont, Gold);
            popup.rectTransform.anchorMin = popup.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            popup.rectTransform.sizeDelta = new Vector2(240f, 70f);
            popup.alignment = TextAlignmentOptions.Center;
            popup.characterSpacing = 0f;
            AddUnderlay(popup, new Color(0f, 0f, 0f, 0.5f), 0.2f, 1.5f);
            popup.gameObject.SetActive(false);
            popupPool.Add(popup);
            return popup;
        }

        TextMeshProUGUI reused = popupPool[popupCursor];
        popupCursor = (popupCursor + 1) % popupPool.Count;
        return reused;
    }

    // ------------------------------------------------------------------ modal
    private void BuildModal()
    {
        modalRoot = CreateRect("GameOver", canvas.transform);
        Stretch(modalRoot);
        modalGroup = modalRoot.gameObject.AddComponent<CanvasGroup>();
        modalGroup.interactable = false;
        modalGroup.blocksRaycasts = false;

        modalDim = CreateImage("Dim", modalRoot, null, new Color(0f, 0f, 0f, 0f));
        Stretch(modalDim.rectTransform);

        modalPanel = CreateRect("Panel", modalRoot);
        modalPanel.anchorMin = modalPanel.anchorMax = new Vector2(0.5f, 0.5f);
        modalPanel.pivot = new Vector2(0.5f, 0.5f);
        modalPanel.sizeDelta = new Vector2(640f, 430f);
        modalPanel.anchoredPosition = new Vector2(0f, -10f);
        // Même orientation que les plaques de score du backglass (rotation à droite).
        modalPanel.localEulerAngles = new Vector3(0f, 0f, -90f);

        modalPanelImage = modalPanel.gameObject.AddComponent<Image>();
        modalPanelImage.sprite = panelSprite;
        modalPanelImage.type = Image.Type.Sliced;
        modalPanelImage.raycastTarget = false;

        Image highlight = CreateImage("Highlight", modalPanel, null, new Color(1f, 0.9f, 0.65f, 0.12f));
        Place(highlight.rectTransform, new Vector2(0f, 198f), new Vector2(590f, 1f));

        modalTitle = CreateText("Title", modalPanel, "PERDU", 108f, displayFont, WarmWhite);
        Place(modalTitle.rectTransform, new Vector2(0f, 118f), new Vector2(620f, 124f));
        modalTitle.characterSpacing = -3f;
        modalTitle.lineSpacing = -10f;
        AddUnderlay(modalTitle, new Color(0f, 0f, 0f, 0.55f), 0.3f, 2.5f);

        modalSubtitle = CreateText("Subtitle", modalPanel, "PARTIE TERMINÉE", 20f, labelFont, GoldMuted);
        Place(modalSubtitle.rectTransform, new Vector2(0f, 50f), new Vector2(600f, 26f));
        modalSubtitle.characterSpacing = 10f;

        Image divider = CreateImage("Divider", modalPanel, null, new Color(0.92f, 0.76f, 0.43f, 0.22f));
        Place(divider.rectTransform, new Vector2(0f, 24f), new Vector2(480f, 1f));

        modalFinalLabel = CreateText("FinalLabel", modalPanel, "SCORE FINAL", 19f, bodyFont, Muted);
        Place(modalFinalLabel.rectTransform, new Vector2(0f, 2f), new Vector2(400f, 20f));
        modalFinalLabel.characterSpacing = 8f;

        modalFinalValue = CreateText("FinalValue", modalPanel, "00000000", 82f, displayFont, Gold);
        Place(modalFinalValue.rectTransform, new Vector2(0f, -46f), new Vector2(600f, 62f));
        modalFinalValue.characterSpacing = -2f;
        AddUnderlay(modalFinalValue, new Color(0f, 0f, 0f, 0.45f), 0.2f, 2f);

        modalTopLines = CreateText("TopLines", modalPanel, string.Empty, 28f, labelFont, new Color(0.92f, 0.84f, 0.68f, 1f));
        Place(modalTopLines.rectTransform, new Vector2(0f, -116f), new Vector2(600f, 62f));
        modalTopLines.lineSpacing = 6f;
        modalTopLines.characterSpacing = 2f;

        modalPromptShadow = CreateImage("PromptShadow", modalPanel, pillSprite, new Color(0f, 0f, 0f, 0.35f));
        modalPromptShadow.type = Image.Type.Sliced;
        Place(modalPromptShadow.rectTransform, new Vector2(0f, -176f), new Vector2(330f, 50f));

        modalPromptPill = CreateImage("PromptPill", modalPanel, pillSprite, new Color(0.86f, 0.7f, 0.4f, 0.95f));
        modalPromptPill.type = Image.Type.Sliced;
        Place(modalPromptPill.rectTransform, new Vector2(0f, -172f), new Vector2(330f, 50f));

        modalPromptText = CreateText("Prompt", modalPanel, "REJOUER", 24f, labelFont, new Color(0.12f, 0.08f, 0.03f, 1f));
        Place(modalPromptText.rectTransform, new Vector2(0f, -172f), new Vector2(320f, 42f));
        modalPromptText.characterSpacing = 8f;

        modalPromptHint = CreateText("PromptHint", modalPanel, "ESPACE / CLIC", 11f, bodyFont, Muted);
        Place(modalPromptHint.rectTransform, new Vector2(0f, -204f), new Vector2(320f, 18f));
        modalPromptHint.characterSpacing = 6f;

        flakeLayer = CreateRect("Flakes", modalRoot);
        Stretch(flakeLayer);
        CreateFlakes();

        modalRoot.gameObject.SetActive(false);
    }

    private void BuildNameModal()
    {
        nameRoot = CreateRect("NameEntry", canvas.transform);
        Stretch(nameRoot);
        nameGroup = nameRoot.gameObject.AddComponent<CanvasGroup>();
        nameGroup.interactable = true;
        nameGroup.blocksRaycasts = true;

        nameDim = CreateImage("Dim", nameRoot, null, new Color(0f, 0f, 0f, 0f));
        Stretch(nameDim.rectTransform);

        namePanel = CreateRect("Panel", nameRoot);
        namePanel.anchorMin = namePanel.anchorMax = new Vector2(0.5f, 0.5f);
        namePanel.pivot = new Vector2(0.5f, 0.5f);
        namePanel.sizeDelta = new Vector2(640f, 430f);
        namePanel.anchoredPosition = new Vector2(0f, -10f);
        // Même orientation que les plaques du backglass.
        namePanel.localEulerAngles = new Vector3(0f, 0f, -90f);

        Image panelImage = namePanel.gameObject.AddComponent<Image>();
        panelImage.sprite = panelSprite;
        panelImage.type = Image.Type.Sliced;
        panelImage.raycastTarget = true;

        Image highlight = CreateImage("Highlight", namePanel, null, new Color(1f, 0.9f, 0.65f, 0.12f));
        Place(highlight.rectTransform, new Vector2(0f, 198f), new Vector2(560f, 1f));

        TextMeshProUGUI title = CreateText("Title", namePanel, "BIENVENUE", 62f, displayFont, WarmWhite);
        Place(title.rectTransform, new Vector2(0f, 128f), new Vector2(600f, 92f));
        title.characterSpacing = -2f;
        AddUnderlay(title, new Color(0f, 0f, 0f, 0.55f), 0.3f, 2.5f);

        TextMeshProUGUI subtitle = CreateText("Subtitle", namePanel, "QUEL EST TON NOM ?", 20f, labelFont, GoldMuted);
        Place(subtitle.rectTransform, new Vector2(0f, 64f), new Vector2(600f, 26f));
        subtitle.characterSpacing = 8f;

        GameObject fieldObject = new GameObject("NameField", typeof(RectTransform));
        fieldObject.transform.SetParent(namePanel, false);
        Image fieldImage = fieldObject.AddComponent<Image>();
        fieldImage.sprite = pillSprite;
        fieldImage.type = Image.Type.Sliced;
        fieldImage.color = new Color(0.055f, 0.047f, 0.038f, 0.98f);
        fieldImage.raycastTarget = true;
        RectTransform fieldRect = (RectTransform)fieldObject.transform;
        Place(fieldRect, new Vector2(0f, -14f), new Vector2(520f, 74f));

        RectTransform viewport = CreateRect("Text Area", fieldObject.transform);
        Stretch(viewport);
        viewport.offsetMin = new Vector2(26f, 8f);
        viewport.offsetMax = new Vector2(-26f, -8f);

        TextMeshProUGUI inputText = CreateText("Text", viewport, string.Empty, 38f, displayFont, WarmWhite);
        Stretch(inputText.rectTransform);
        inputText.alignment = TextAlignmentOptions.Center;
        inputText.textWrappingMode = TextWrappingModes.NoWrap;

        TextMeshProUGUI placeholder = CreateText("Placeholder", viewport, "TON NOM", 38f, displayFont,
            new Color(0.66f, 0.6f, 0.5f, 0.45f));
        Stretch(placeholder.rectTransform);
        placeholder.alignment = TextAlignmentOptions.Center;
        placeholder.textWrappingMode = TextWrappingModes.NoWrap;

        nameInput = fieldObject.AddComponent<TMP_InputField>();
        nameInput.targetGraphic = fieldImage;
        nameInput.textViewport = viewport;
        nameInput.textComponent = inputText;
        nameInput.placeholder = placeholder;
        nameInput.lineType = TMP_InputField.LineType.SingleLine;
        nameInput.contentType = TMP_InputField.ContentType.Name;
        nameInput.characterValidation = TMP_InputField.CharacterValidation.Name;
        nameInput.characterLimit = 15;
        nameInput.caretColor = Gold;
        nameInput.selectionColor = new Color(0.92f, 0.76f, 0.43f, 0.35f);
        nameInput.caretWidth = 2;
        nameInput.onSubmit.AddListener(OnNameSubmitted);

        RectTransform toggleRow = CreateRect("FunSounds", namePanel);
        Place(toggleRow, new Vector2(0f, -70f), new Vector2(520f, 32f));

        GameObject toggleBoxObject = new GameObject("Checkbox", typeof(RectTransform));
        toggleBoxObject.transform.SetParent(toggleRow, false);
        Image toggleBoxImage = toggleBoxObject.AddComponent<Image>();
        toggleBoxImage.sprite = null;
        toggleBoxImage.type = Image.Type.Simple;
        toggleBoxImage.color = new Color(0.88f, 0.82f, 0.68f, 0.96f);
        toggleBoxImage.raycastTarget = true;
        RectTransform toggleBoxRect = (RectTransform)toggleBoxObject.transform;
        Place(toggleBoxRect, new Vector2(-232f, 0f), new Vector2(30f, 30f));

        GameObject checkmarkObject = new GameObject("Checkmark", typeof(RectTransform));
        checkmarkObject.transform.SetParent(toggleBoxObject.transform, false);
        Image checkmarkImage = checkmarkObject.AddComponent<Image>();
        checkmarkImage.sprite = null;
        checkmarkImage.type = Image.Type.Simple;
        checkmarkImage.color = new Color(0.12f, 0.08f, 0.03f, 1f);
        checkmarkImage.raycastTarget = false;
        RectTransform checkmarkRect = (RectTransform)checkmarkObject.transform;
        Stretch(checkmarkRect);
        checkmarkRect.offsetMin = new Vector2(7f, 7f);
        checkmarkRect.offsetMax = new Vector2(-7f, -7f);

        funToggleBox = toggleBoxImage;
        funToggleCheckmark = checkmarkObject;
        funToggleLabel = CreateText("Label", toggleRow, "SONS FUN (MÈMES) : OUI", 20f, labelFont, WarmWhite);
        Place(funToggleLabel.rectTransform, new Vector2(22f, 0f), new Vector2(456f, 32f));
        funToggleLabel.alignment = TextAlignmentOptions.Left;
        funToggleLabel.characterSpacing = 1f;

        funToggle = toggleBoxObject.AddComponent<Toggle>();
        funToggle.targetGraphic = toggleBoxImage;
        funToggle.transition = Selectable.Transition.None;
        funToggle.toggleTransition = Toggle.ToggleTransition.None;
        funToggle.graphic = null;
        funToggle.isOn = ArcadeAudio.FunSoundsEnabled;
        funToggle.onValueChanged.AddListener(OnFunSoundsChanged);
        UpdateFunToggleVisual(ArcadeAudio.FunSoundsEnabled);

        GameObject buttonObject = new GameObject("PlayButton", typeof(RectTransform));
        buttonObject.transform.SetParent(namePanel, false);
        Image buttonImage = buttonObject.AddComponent<Image>();
        buttonImage.sprite = pillSprite;
        buttonImage.type = Image.Type.Sliced;
        buttonImage.color = new Color(0.86f, 0.7f, 0.4f, 0.95f);
        RectTransform buttonRect = (RectTransform)buttonObject.transform;
        Place(buttonRect, new Vector2(0f, -118f), new Vector2(330f, 50f));

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(SubmitName);

        TextMeshProUGUI buttonText = CreateText("Label", buttonObject.transform, "JOUER", 24f, labelFont,
            new Color(0.12f, 0.08f, 0.03f, 1f));
        Stretch(buttonText.rectTransform);
        buttonText.alignment = TextAlignmentOptions.Center;
        buttonText.characterSpacing = 8f;

        TextMeshProUGUI hint = CreateText("Hint", namePanel, "ENTRÉE POUR VALIDER", 11f, bodyFont, Muted);
        Place(hint.rectTransform, new Vector2(0f, -158f), new Vector2(400f, 18f));
        hint.characterSpacing = 6f;

        nameRoot.gameObject.SetActive(false);
    }

    /// <summary>Ferme immédiatement la saisie du nom (ex. bouton Quit).</summary>
    public void HideNameEntry()
    {
        if (nameRoot == null)
        {
            return;
        }

        nameVisible = false;
        nameClosing = false;
        nameFocusPending = false;

        if (nameInput != null)
        {
            nameInput.DeactivateInputField();
        }

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        nameRoot.gameObject.SetActive(false);
    }

    private void ShowNameEntry()
    {
        if (!built || nameRoot == null || manager == null)
        {
            return;
        }

        nameVisible = true;
        nameClosing = false;
        nameCloseTimer = 0f;
        nameElapsed = 0f;
        nameFocusPending = true;
        namePanelScale = 0.94f;
        namePanelScaleVelocity = 0f;
        nameDimAlpha = 0f;
        nameDimAlphaVelocity = 0f;

        nameRoot.gameObject.SetActive(true);
        nameRoot.SetAsLastSibling();
        nameGroup.alpha = 1f;
        nameGroup.interactable = true;
        nameGroup.blocksRaycasts = true;
        namePanel.localScale = Vector3.one * namePanelScale;
        namePanel.anchoredPosition = new Vector2(0f, -10f);
        nameDim.color = new Color(0f, 0f, 0f, 0f);

        if (nameInput != null)
        {
            nameInput.text = manager.PlayerName;
        }

        if (funToggle != null)
        {
            funToggle.SetIsOnWithoutNotify(ArcadeAudio.FunSoundsEnabled);
        }

        UpdateFunToggleVisual(ArcadeAudio.FunSoundsEnabled);

        manager.SetLaunchLocked(true);
    }

    private void UpdateNameModal()
    {
        if (nameClosing)
        {
            nameCloseTimer += Time.unscaledDeltaTime;
            nameGroup.alpha = Mathf.Clamp01(1f - nameCloseTimer / 0.18f);

            if (nameCloseTimer >= 0.18f)
            {
                nameGroup.alpha = 1f;
                nameRoot.gameObject.SetActive(false);
                nameVisible = false;
                nameClosing = false;
            }

            return;
        }

        float dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
        nameElapsed += dt;

        // Focus différé : évite que l'Espace utilisé pour relancer la partie
        // soit inséré dans le champ de saisie.
        if (nameFocusPending && nameElapsed >= 0.05f)
        {
            nameFocusPending = false;

            if (nameInput != null)
            {
                nameInput.ActivateInputField();
                nameInput.caretPosition = nameInput.text.Length;
                nameInput.selectionAnchorPosition = 0;
                nameInput.selectionFocusPosition = nameInput.text.Length;
            }
        }

        Spring(ref namePanelScale, ref namePanelScaleVelocity, 1f, 0.82f, 3.2f, dt);
        Spring(ref nameDimAlpha, ref nameDimAlphaVelocity, 0.9f, 1f, 5f, dt);

        namePanel.localScale = new Vector3(namePanelScale, namePanelScale, 1f);
        nameDim.color = new Color(0f, 0f, 0f, nameDimAlpha);
    }

    private void OnNameSubmitted(string value)
    {
        SubmitName();
    }

    private void OnFunSoundsChanged(bool enabled)
    {
        ArcadeAudio.SetFunSounds(enabled);
        UpdateFunToggleVisual(enabled);
    }

    private void UpdateFunToggleVisual(bool enabled)
    {
        if (funToggleBox != null)
        {
            funToggleBox.color = enabled ? Gold : new Color(0.88f, 0.82f, 0.68f, 0.96f);
        }

        if (funToggleCheckmark != null)
        {
            funToggleCheckmark.SetActive(enabled);
        }

        if (funToggleLabel != null)
        {
            funToggleLabel.text = enabled ? "SONS FUN (MÈMES) : OUI" : "SONS FUN (MÈMES) : NON";
        }
    }

    private void SubmitName()
    {
        if (!nameVisible || nameClosing || manager == null)
        {
            return;
        }

        manager.SetPlayerName(nameInput != null ? nameInput.text : null);
        manager.SetLaunchLocked(false);

        if (nameInput != null)
        {
            nameInput.DeactivateInputField();
        }

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        nameClosing = true;
        nameCloseTimer = 0f;
        nameGroup.interactable = false;
        nameGroup.blocksRaycasts = false;
    }

    private void CreateFlakes()
    {
        for (int i = 0; i < 22; i++)
        {
            Image flake = CreateImage("Flake", flakeLayer, circleSprite, new Color(1f, 0.88f, 0.55f, 0.34f));
            RectTransform rect = flake.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            float size = Random.Range(3f, 7f);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = new Vector2(Random.Range(-620f, 620f), Random.Range(-400f, 420f));
            flakes.Add(rect);
            flakeSpeeds.Add(Random.Range(38f, 105f));
            flakePhases.Add(Random.Range(0f, Mathf.PI * 2f));
        }
    }

    public void ShowGameOver()
    {
        if (!built)
        {
            return;
        }

        StopAllCoroutines();
        modalRoot.gameObject.SetActive(true);
        modalRoot.SetAsLastSibling();
        gameOverVisible = true;
        restartEnabledAt = Time.unscaledTime + 1.15f;
        modalElapsed = 0f;
        modalTargetScore = manager != null ? manager.Score : 0;

        panelScale = 0.94f;
        panelScaleVelocity = 0f;
        panelOffset = -10f;
        panelOffsetVelocity = 0f;
        titleFade = 0f;
        titleFadeVelocity = 0f;
        dimAlpha = 0f;
        dimAlphaVelocity = 0f;

        // Les meilleurs scores viennent d'être mis à jour par le manager.
        string top1Name = manager != null ? ShortName(manager.Top1.name) : "---";
        int top1Score = manager != null ? manager.Top1.score : 0;
        string top2Name = manager != null ? ShortName(manager.Top2.name) : "---";
        int top2Score = manager != null ? manager.Top2.score : 0;

        modalTopLines.text = "<color=#C9A24B>TOP 1</color>   " + top1Name + "   " + FormatScore(top1Score)
            + "\n<color=#C9A24B>TOP 2</color>   " + top2Name + "   " + FormatScore(top2Score);

        modalFinalValue.text = "00000000";
        modalFinalValue.alpha = 0f;
        modalFinalLabel.alpha = 0f;
        modalTopLines.alpha = 0f;
        modalTitle.alpha = 0f;
        modalTitle.rectTransform.localScale = Vector3.one * 1.04f;
        modalSubtitle.alpha = 0f;
        modalPromptText.alpha = 0f;
        modalPromptHint.alpha = 0f;
        modalPromptPill.color = new Color(0.86f, 0.7f, 0.4f, 0f);
        modalPromptShadow.color = new Color(0f, 0f, 0f, 0f);
        modalGroup.alpha = 1f;
        modalDim.color = new Color(0f, 0f, 0f, 0f);
        modalPanel.localScale = Vector3.one * panelScale;
        modalPanel.anchoredPosition = new Vector2(0f, panelOffset);
    }

    private void UpdateModal()
    {
        if (!built || !gameOverVisible)
        {
            return;
        }

        float dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
        modalElapsed += dt;

        Spring(ref panelScale, ref panelScaleVelocity, 1f, 0.82f, 3.2f, dt);
        Spring(ref panelOffset, ref panelOffsetVelocity, 0f, 0.9f, 3f, dt);
        Spring(ref titleFade, ref titleFadeVelocity, 1f, 1f, 4.5f, dt);
        Spring(ref dimAlpha, ref dimAlphaVelocity, 0.9f, 1f, 5f, dt);

        modalPanel.localScale = new Vector3(panelScale, panelScale, 1f);
        modalPanel.anchoredPosition = new Vector2(0f, panelOffset);
        modalDim.color = new Color(0f, 0f, 0f, dimAlpha);

        modalTitle.alpha = titleFade;
        modalTitle.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.04f, 1f, titleFade);
        modalSubtitle.alpha = titleFade;

        float reveal = Mathf.Clamp01((modalElapsed - 0.22f) / 0.4f);
        modalFinalLabel.alpha = reveal * 0.9f;
        modalFinalValue.alpha = reveal;

        float countT = Mathf.Clamp01((modalElapsed - 0.32f) / 0.9f);
        float eased = 1f - Mathf.Pow(1f - countT, 3f);
        modalFinalValue.text = FormatScore(Mathf.RoundToInt(modalTargetScore * eased));

        float topReveal = Mathf.Clamp01((modalElapsed - 1.05f) / 0.4f);
        modalTopLines.alpha = topReveal;

        float promptReveal = Mathf.Clamp01((modalElapsed - 1.25f) / 0.35f);
        float breathe = 0.78f + 0.22f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 2.1f));
        modalPromptText.alpha = promptReveal;
        modalPromptHint.alpha = promptReveal * 0.7f;
        modalPromptPill.color = new Color(0.86f, 0.7f, 0.4f, 0.95f * promptReveal * breathe);
        modalPromptShadow.color = new Color(0f, 0f, 0f, 0.35f * promptReveal * breathe);

        AnimateFlakes(dt);
    }

    public void HideGameOver()
    {
        if (!built)
        {
            return;
        }

        StopAllCoroutines();
        gameOverVisible = false;
        StartCoroutine(FadeOutModal());
    }

    private IEnumerator FadeOutModal()
    {
        float time = 0f;
        float startAlpha = modalGroup.alpha;

        while (time < 0.2f)
        {
            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / 0.2f);
            modalGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
            yield return null;
        }

        modalGroup.alpha = 1f;
        modalRoot.gameObject.SetActive(false);
    }

    private void AnimateFlakes(float dt)
    {
        for (int i = 0; i < flakes.Count; i++)
        {
            RectTransform flake = flakes[i];
            if (flake == null)
            {
                continue;
            }

            Vector2 position = flake.anchoredPosition;
            position.y -= flakeSpeeds[i] * dt;
            flakePhases[i] += dt * 1.2f;
            position.x += Mathf.Sin(flakePhases[i]) * 16f * dt;

            if (position.y < -560f)
            {
                position.y = 560f;
                position.x = Random.Range(-620f, 620f);
            }

            flake.anchoredPosition = position;
        }
    }

    // ----------------------------------------------------------------- helpers
    private static void Spring(ref float value, ref float velocity, float target, float damping, float frequency, float dt)
    {
        float omega = 2f * Mathf.PI * frequency;
        float f = 1f + 2f * dt * damping * omega;
        float oo = omega * omega;
        float hoo = dt * oo;
        float hhoo = dt * hoo;
        float detInv = 1f / (f + hhoo);
        float detX = f * value + dt * velocity + hhoo * target;
        float detV = velocity + hhoo * (target - value);
        value = detX * detInv;
        velocity = detV * detInv;
    }

    private void HideLegacyTexts()
    {
        string[] names = { "Score Actuel", "Text - Score", "Premier Nom - Classement", "Premier Nom - Classement (1)" };
        foreach (string name in names)
        {
            GameObject legacy = GameObject.Find(name);
            if (legacy != null)
            {
                legacy.SetActive(false);
            }
        }
    }

    private MeshFilter FindScoreMesh()
    {
        foreach (MeshFilter filter in FindObjectsByType<MeshFilter>())
        {
            if (filter.name == "Score" && filter.sharedMesh != null)
            {
                return filter;
            }
        }

        return null;
    }

    private static string FormatScore(int score)
    {
        return Mathf.Max(0, score).ToString("D8");
    }

    private static string ShortName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return "---";
        }

        name = name.ToUpperInvariant();
        return name.Length > 15 ? name.Substring(0, 15) : name;
    }

    private TextMeshProUGUI CreateText(string name, Transform parent, string content, float size, TMP_FontAsset font, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.text = content;
        text.font = font;
        text.fontSize = size;
        text.color = color;
        text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.Center;
        text.overflowMode = TextOverflowModes.Overflow;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }

    private static void AddUnderlay(TextMeshProUGUI text, Color color, float softness, float offsetY)
    {
        if (text == null || text.font == null)
        {
            return;
        }

        Material material = new Material(text.font.material);
        material.EnableKeyword("UNDERLAY_ON");
        material.SetColor("_UnderlayColor", color);
        material.SetFloat("_UnderlaySoftness", softness);
        material.SetFloat("_UnderlayOffsetX", 0f);
        material.SetFloat("_UnderlayOffsetY", -offsetY * 0.01f);
        material.SetFloat("_UnderlayDilate", 0.1f);
        text.fontMaterial = material;
    }

    private static Image CreateImage(string name, Transform parent, Sprite sprite, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}
