using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// HUD moderne du flipper : score, billes, combo, événement météo, tableau
/// du backglass, popups de points, modale de fin de partie animée.
/// L'interface est générée au lancement pour rester indépendante de la scène.
/// </summary>
public sealed class ScoreboardUI : MonoBehaviour
{
    private const float ReferenceWidth = 1205f;
    private const float ReferenceHeight = 678f;
    private const float CardReference = 100f;

    private static readonly Color Gold = new Color(1f, 0.82f, 0.42f, 1f);
    private static readonly Color GoldSoft = new Color(0.94f, 0.74f, 0.36f, 1f);
    private static readonly Color WarmWhite = new Color(1f, 0.97f, 0.9f, 1f);
    private static readonly Color Muted = new Color(0.78f, 0.7f, 0.56f, 1f);
    private static readonly Color StormBlue = new Color(0.6f, 0.82f, 1f, 1f);
    private static readonly Color PanelDark = new Color(0.06f, 0.05f, 0.04f, 0.92f);

    private PinballGameManager manager;
    private Canvas canvas;
    private TMP_FontAsset titleFont;
    private TMP_FontAsset labelFont;

    private Sprite roundedPanel;
    private Sprite roundedPill;
    private Sprite circleSprite;

    private TextMeshProUGUI plaqueScore;
    private TextMeshProUGUI plaquePlayer;
    private TextMeshProUGUI plaqueTop1;
    private TextMeshProUGUI plaqueTop2;
    private TextMeshProUGUI plaqueEvent;

    private TextMeshProUGUI hudPlayer;
    private TextMeshProUGUI hudScore;
    private TextMeshProUGUI hudBallsLabel;
    private TextMeshProUGUI hudCombo;
    private readonly Image[] ballPips = new Image[3];

    private Image stormPill;
    private TextMeshProUGUI stormText;
    private Image hintPill;
    private TextMeshProUGUI hintText;

    private TextMeshProUGUI comboPop;
    private Coroutine comboPopRoutine;

    private RectTransform popupLayer;
    private readonly List<TextMeshProUGUI> popupPool = new List<TextMeshProUGUI>();
    private int popupCursor;

    private RectTransform modalRoot;
    private CanvasGroup modalGroup;
    private Image modalDim;
    private RectTransform modalPanel;
    private TextMeshProUGUI modalTitle;
    private TextMeshProUGUI modalSubtitle;
    private TextMeshProUGUI modalFinalValue;
    private TextMeshProUGUI modalTopLines;
    private TextMeshProUGUI modalPrompt;
    private RectTransform flakeLayer;
    private readonly List<RectTransform> flakes = new List<RectTransform>();
    private readonly List<float> flakeSpeeds = new List<float>();
    private readonly List<float> flakePhases = new List<float>();

    private TextMeshProUGUI toastPillText;
    private Image toastPill;
    private Coroutine toastRoutine;
    private bool built;
    private bool gameOverVisible;
    private bool modalIntroDone;
    private float restartEnabledAt;
    private int lastCombo;
    private float hintPulse;

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
        new Plaque("top1", 0.746f, 0.727f, 0.953f, 0.840f),
        new Plaque("score", 0.291f, 0.656f, 0.713f, 0.891f),
        new Plaque("top2", 0.049f, 0.727f, 0.254f, 0.840f),
        new Plaque("player", 0.049f, 0.098f, 0.254f, 0.211f),
        new Plaque("event", 0.746f, 0.098f, 0.953f, 0.211f)
    };

    public void Bind(PinballGameManager gameManager)
    {
        if (manager == gameManager)
        {
            return;
        }

        if (manager != null)
        {
            manager.ScoreChanged -= Refresh;
            manager.StateChanged -= Refresh;
            manager.ToastRequested -= ShowToast;
        }

        manager = gameManager;

        if (manager != null)
        {
            manager.ScoreChanged += Refresh;
            manager.StateChanged += Refresh;
            manager.ToastRequested += ShowToast;
        }
    }

    private void OnDestroy()
    {
        if (manager != null)
        {
            manager.ScoreChanged -= Refresh;
            manager.StateChanged -= Refresh;
            manager.ToastRequested -= ShowToast;
        }
    }

    private void Start()
    {
        Build();
        Refresh();
    }

    private void Update()
    {
        if (!built)
        {
            return;
        }

        if (manager != null && manager.IsGameOver)
        {
            bool restart = (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                || (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);

            if (restart && gameOverVisible && Time.unscaledTime >= restartEnabledAt)
            {
                manager.RestartGame();
            }
        }

        AnimateHint();
        AnimateStorm();
        AnimateComboPop();

        if (gameOverVisible)
        {
            AnimateFlakes();
            AnimatePrompt();
        }
    }

    private void Build()
    {
        canvas = FindFirstObjectByType<Canvas>();
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

        BuildHud();
        BuildToast();
        BuildComboPop();
        BuildModal();

        built = true;
    }

    // ------------------------------------------------------------------ fonts
    private void LoadFonts()
    {
        TMP_FontAsset fallback = FindLegacyFont();
        titleFont = CreateRuntimeFont("Fonts/RussoOne", fallback);
        labelFont = CreateRuntimeFont("Fonts/Rajdhani-SemiBold", fallback);
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
        roundedPanel = CreateRoundedSprite("PanelRounded", 14, 3, new Color(0.05f, 0.04f, 0.03f, 0.92f), new Color(0.95f, 0.76f, 0.38f, 0.95f));
        roundedPill = CreateRoundedSprite("PillRounded", 16, 0, Color.white, Color.white);
        circleSprite = CreateCircleSprite("PipCircle", 32);
    }

    private static Sprite CreateRoundedSprite(string name, int radius, int border, Color fill, Color stroke)
    {
        const int size = 64;
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

                bool isBorder = distance > -border;
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
            new Vector4(18f, 18f, 18f, 18f));
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

        TextMeshProUGUI text = CreateText("UI_" + plaque.id, canvas.transform, string.Empty, 16f, labelFont, Gold);
        RectTransform rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(
            screenRect.center.x / Mathf.Max(1f, camera != null ? camera.pixelWidth : ReferenceWidth),
            screenRect.center.y / Mathf.Max(1f, camera != null ? camera.pixelHeight : ReferenceHeight));
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(height / scale, width / scale);
        rect.localEulerAngles = new Vector3(0f, 0f, -90f);

        bool big = plaque.id == "score";
        text.font = big ? titleFont : labelFont;
        text.fontSize = big ? referenceWidth * 0.36f : referenceWidth * 0.47f;
        text.alignment = TextAlignmentOptions.Center;
        text.overflowMode = TextOverflowModes.Overflow;
        AddOutline(text, new Color(0f, 0f, 0f, 0.85f), 0.16f);

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
            case "event":
                plaqueEvent = text;
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

    // -------------------------------------------------------------------- HUD
    private void BuildHud()
    {
        // Carte joueur / score, en haut à gauche du plateau.
        RectTransform scoreCard = CreateCard("HUD_ScoreCard", new Vector2(0f, 1f), new Vector2(20f, -20f), new Vector2(370f, 136f), new Vector2(0f, 1f));
        AddAccent(scoreCard, new Vector2(0f, 1f), new Vector2(0f, -14f), new Vector2(4f, 104f));

        TextMeshProUGUI playerLabel = CreateLabel("HUD_PlayerLabel", scoreCard, "JOUEUR", 13f, Muted);
        Place(playerLabel.rectTransform, new Vector2(18f, -10f), new Vector2(320f, 16f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        playerLabel.alignment = TextAlignmentOptions.Left;

        hudPlayer = CreateLabel("HUD_Player", scoreCard, "AXEL L", 22f, WarmWhite);
        Place(hudPlayer.rectTransform, new Vector2(18f, -26f), new Vector2(330f, 28f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        hudPlayer.alignment = TextAlignmentOptions.Left;

        TextMeshProUGUI scoreLabel = CreateLabel("HUD_ScoreLabel", scoreCard, "SCORE", 12f, Muted);
        Place(scoreLabel.rectTransform, new Vector2(18f, -58f), new Vector2(140f, 14f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        scoreLabel.alignment = TextAlignmentOptions.Left;

        hudScore = CreateText("HUD_Score", scoreCard, "00000000", 42f, titleFont, Gold);
        Place(hudScore.rectTransform, new Vector2(16f, -72f), new Vector2(330f, 52f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        hudScore.alignment = TextAlignmentOptions.Left;
        AddOutline(hudScore, new Color(0.18f, 0.08f, 0f, 0.9f), 0.18f);

        // Carte billes / combo / météo, juste à gauche du backglass.
        RectTransform statusCard = CreateCard("HUD_StatusCard", new Vector2(1f, 1f), new Vector2(-278f, -20f), new Vector2(250f, 120f), new Vector2(1f, 1f));

        hudBallsLabel = CreateLabel("HUD_BallsLabel", statusCard, "BILLES  3 / 3", 13f, Muted);
        Place(hudBallsLabel.rectTransform, new Vector2(16f, -10f), new Vector2(160f, 16f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        hudBallsLabel.alignment = TextAlignmentOptions.Left;

        for (int i = 0; i < ballPips.Length; i++)
        {
            Image pip = CreateImage("HUD_Ball" + i, statusCard, circleSprite, Gold);
            pip.rectTransform.anchorMin = pip.rectTransform.anchorMax = new Vector2(0f, 1f);
            pip.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            pip.rectTransform.sizeDelta = new Vector2(17f, 17f);
            pip.rectTransform.anchoredPosition = new Vector2(172f + i * 26f, -19f);
            ballPips[i] = pip;
        }

        hudCombo = CreateLabel("HUD_Combo", statusCard, "COMBO  -", 18f, Muted);
        Place(hudCombo.rectTransform, new Vector2(16f, -46f), new Vector2(220f, 24f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        hudCombo.alignment = TextAlignmentOptions.Left;

        stormPill = CreateImage("HUD_StormPill", statusCard, roundedPill, new Color(0.45f, 0.65f, 0.92f, 0.75f));
        stormPill.type = Image.Type.Sliced;
        stormPill.rectTransform.anchorMin = stormPill.rectTransform.anchorMax = new Vector2(0f, 1f);
        stormPill.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        stormPill.rectTransform.sizeDelta = new Vector2(214f, 28f);
        stormPill.rectTransform.anchoredPosition = new Vector2(125f, -90f);

        stormText = CreateText("HUD_StormText", statusCard, "TEMPÊTE  -  x2", 12f, labelFont, Color.white);
        stormText.rectTransform.anchorMin = stormText.rectTransform.anchorMax = new Vector2(0f, 1f);
        stormText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        stormText.rectTransform.sizeDelta = new Vector2(210f, 24f);
        stormText.rectTransform.anchoredPosition = new Vector2(125f, -90f);

        stormPill.gameObject.SetActive(false);
        stormText.gameObject.SetActive(false);

        // Indication contextuelle en bas.
        hintPill = CreateImage("HUD_HintPill", canvas.transform, roundedPill, new Color(0.04f, 0.03f, 0.02f, 0.72f));
        hintPill.type = Image.Type.Sliced;
        hintPill.rectTransform.anchorMin = hintPill.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        hintPill.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        hintPill.rectTransform.sizeDelta = new Vector2(560f, 44f);
        hintPill.rectTransform.anchoredPosition = new Vector2(0f, 26f);

        hintText = CreateText("HUD_Hint", canvas.transform, "ESPACE / BOUTON  -  LANCER LA BILLE", 17f, labelFont, WarmWhite);
        hintText.rectTransform.anchorMin = hintText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        hintText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        hintText.rectTransform.sizeDelta = new Vector2(550f, 40f);
        hintText.rectTransform.anchoredPosition = new Vector2(0f, 26f);
    }

    private void BuildComboPop()
    {
        comboPop = CreateText("HUD_ComboPop", canvas.transform, "COMBO x2", 52f, titleFont, Gold);
        comboPop.rectTransform.anchorMin = comboPop.rectTransform.anchorMax = new Vector2(0.5f, 0.62f);
        comboPop.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        comboPop.rectTransform.sizeDelta = new Vector2(420f, 74f);
        comboPop.rectTransform.anchoredPosition = Vector2.zero;
        comboPop.alpha = 0f;
        AddOutline(comboPop, new Color(0.15f, 0.05f, 0f, 0.9f), 0.22f);
    }

    private void BuildToast()
    {
        toastPill = CreateImage("HUD_ToastPill", canvas.transform, roundedPill, new Color(0.04f, 0.03f, 0.02f, 0.78f));
        toastPill.type = Image.Type.Sliced;
        toastPill.rectTransform.anchorMin = toastPill.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        toastPill.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        toastPill.rectTransform.sizeDelta = new Vector2(520f, 42f);
        toastPill.rectTransform.anchoredPosition = new Vector2(0f, -72f);
        toastPill.gameObject.SetActive(false);

        toastPillText = CreateText("HUD_Toast", canvas.transform, string.Empty, 18f, labelFont, Gold);
        toastPillText.rectTransform.anchorMin = toastPillText.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        toastPillText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        toastPillText.rectTransform.sizeDelta = new Vector2(510f, 38f);
        toastPillText.rectTransform.anchoredPosition = new Vector2(0f, -72f);
        toastPillText.gameObject.SetActive(false);
    }

    // ------------------------------------------------------------------- modal
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
        modalPanel.sizeDelta = new Vector2(680f, 430f);
        modalPanel.anchoredPosition = Vector2.zero;

        Image panelBackground = modalPanel.gameObject.AddComponent<Image>();
        panelBackground.sprite = roundedPanel;
        panelBackground.type = Image.Type.Sliced;
        panelBackground.raycastTarget = false;

        Image innerPanel = CreateImage("Inner", modalPanel, null, new Color(0.055f, 0.045f, 0.035f, 0.97f));
        Place(innerPanel.rectTransform, Vector2.zero, new Vector2(650f, 400f));

        modalTitle = CreateText("Title", modalPanel, "PERDU", 98f, titleFont, Gold);
        Place(modalTitle.rectTransform, new Vector2(0f, 118f), new Vector2(620f, 130f));
        AddOutline(modalTitle, new Color(0.22f, 0.07f, 0f, 1f), 0.28f);

        modalSubtitle = CreateText("Subtitle", modalPanel, "PARTIE TERMINÉE", 18f, labelFont, Muted);
        Place(modalSubtitle.rectTransform, new Vector2(0f, 52f), new Vector2(600f, 30f));
        modalSubtitle.characterSpacing = 6f;

        Image divider = CreateImage("Divider", modalPanel, null, new Color(0.85f, 0.66f, 0.3f, 0.55f));
        Place(divider.rectTransform, new Vector2(0f, 28f), new Vector2(480f, 2f));

        TextMeshProUGUI finalLabel = CreateText("FinalLabel", modalPanel, "SCORE FINAL", 14f, labelFont, Muted);
        Place(finalLabel.rectTransform, new Vector2(0f, 6f), new Vector2(400f, 22f));

        modalFinalValue = CreateText("FinalValue", modalPanel, "00000000", 54f, titleFont, WarmWhite);
        Place(modalFinalValue.rectTransform, new Vector2(0f, -44f), new Vector2(600f, 64f));
        AddOutline(modalFinalValue, Color.black, 0.18f);

        modalTopLines = CreateText("TopLines", modalPanel, string.Empty, 17f, labelFont, new Color(0.92f, 0.8f, 0.54f, 1f));
        Place(modalTopLines.rectTransform, new Vector2(0f, -112f), new Vector2(600f, 66f));
        modalTopLines.lineSpacing = 6f;

        Image replayPill = CreateImage("ReplayPill", modalPanel, roundedPill, new Color(0.85f, 0.66f, 0.3f, 0.9f));
        replayPill.type = Image.Type.Sliced;
        Place(replayPill.rectTransform, new Vector2(0f, -178f), new Vector2(330f, 48f));

        modalPrompt = CreateText("Prompt", modalPanel, "CLIC / ESPACE  -  REJOUER", 18f, titleFont, new Color(0.16f, 0.09f, 0.02f, 1f));
        Place(modalPrompt.rectTransform, new Vector2(0f, -178f), new Vector2(320f, 44f));

        flakeLayer = CreateRect("Flakes", modalRoot);
        Stretch(flakeLayer);
        CreateFlakes();

        modalRoot.gameObject.SetActive(false);
    }

    private void CreateFlakes()
    {
        for (int i = 0; i < 24; i++)
        {
            Image flake = CreateImage("Flake", flakeLayer, circleSprite, new Color(1f, 0.89f, 0.56f, 0.7f));
            RectTransform rect = flake.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(Random.Range(4f, 8f), Random.Range(9f, 16f));
            rect.anchoredPosition = new Vector2(Random.Range(-620f, 620f), Random.Range(-400f, 420f));
            rect.localEulerAngles = new Vector3(0f, 0f, Random.Range(0f, 360f));
            flakes.Add(rect);
            flakeSpeeds.Add(Random.Range(55f, 165f));
            flakePhases.Add(Random.Range(0f, Mathf.PI * 2f));
        }
    }

    // ----------------------------------------------------------------- refresh
    private void Refresh()
    {
        if (!built || manager == null)
        {
            return;
        }

        if (plaqueScore != null)
        {
            plaqueScore.text = "SCORE\n" + FormatScore(manager.Score);
        }

        if (plaquePlayer != null)
        {
            plaquePlayer.text = "JOUEUR\n" + ShortName(manager.PlayerName);
        }

        if (plaqueTop1 != null)
        {
            plaqueTop1.text = "TOP 1\n" + ShortName(manager.Top1.name) + " " + FormatScore(manager.Top1.score);
        }

        if (plaqueTop2 != null)
        {
            plaqueTop2.text = "TOP 2\n" + ShortName(manager.Top2.name) + " " + FormatScore(manager.Top2.score);
        }

        if (plaqueEvent != null)
        {
            plaqueEvent.color = manager.StormActive ? StormBlue : Muted;
            plaqueEvent.text = manager.StormActive ? "TEMPÊTE\nx2" : "CALME";
        }

        if (hudScore != null)
        {
            hudScore.text = FormatScore(manager.Score);
        }

        if (hudPlayer != null)
        {
            hudPlayer.text = ShortName(manager.PlayerName);
        }

        if (hudBallsLabel != null)
        {
            hudBallsLabel.text = "BILLES  " + manager.Balls + " / " + manager.startingBalls;
        }

        for (int i = 0; i < ballPips.Length; i++)
        {
            if (ballPips[i] == null)
            {
                continue;
            }

            bool alive = i < manager.Balls;
            ballPips[i].color = alive ? Gold : new Color(0.25f, 0.21f, 0.15f, 0.6f);
            ballPips[i].rectTransform.localScale = alive ? Vector3.one : Vector3.one * 0.82f;
        }

        if (hudCombo != null)
        {
            bool comboActive = manager.ComboActive;
            hudCombo.text = comboActive ? "COMBO  x" + manager.BumperCombo : "COMBO  -";
            hudCombo.color = comboActive ? Gold : Muted;
        }

        if (stormPill != null)
        {
            bool storm = manager.StormActive;
            stormPill.gameObject.SetActive(storm);
            stormText.gameObject.SetActive(storm);
        }

        if (hintPill != null)
        {
            bool showHint = !manager.IsGameOver;
            hintPill.gameObject.SetActive(showHint);
            hintText.gameObject.SetActive(showHint);

            if (!manager.HasLaunched)
            {
                hintText.text = "ESPACE / BOUTON  -  LANCER LA BILLE";
            }
            else
            {
                int currentBall = Mathf.Clamp(manager.startingBalls - manager.Balls + 1, 1, manager.startingBalls);
                hintText.text = "BILLE " + currentBall + " / " + manager.startingBalls;
            }
        }

        if (manager.BumperCombo > lastCombo && manager.BumperCombo >= 2)
        {
            PlayComboPop(manager.BumperCombo);
        }

        lastCombo = manager.BumperCombo;
    }

    private void AnimateHint()
    {
        if (hintText == null || hintPill == null || manager == null)
        {
            return;
        }

        if (!manager.HasLaunched && !manager.IsGameOver)
        {
            hintPulse += Time.unscaledDeltaTime * 3f;
            float pulse = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(hintPulse));
            Color color = hintText.color;
            color.a = pulse;
            hintText.color = color;
        }
        else
        {
            Color color = hintText.color;
            color.a = 0.75f;
            hintText.color = color;
        }
    }

    private void AnimateStorm()
    {
        if (stormText == null || manager == null || !manager.StormActive)
        {
            return;
        }

        float pulse = 0.7f + 0.3f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 2.1f));
        Color color = stormText.color;
        color.a = pulse;
        stormText.color = color;
    }

    private void PlayComboPop(int combo)
    {
        if (comboPop == null)
        {
            return;
        }

        if (comboPopRoutine != null)
        {
            StopCoroutine(comboPopRoutine);
        }

        comboPopRoutine = StartCoroutine(AnimateComboPop(combo));
    }

    private IEnumerator AnimateComboPop(int combo)
    {
        comboPop.text = "COMBO x" + combo;
        comboPop.alpha = 1f;
        comboPop.rectTransform.localScale = Vector3.one * 0.7f;

        float time = 0f;
        const float duration = 0.65f;

        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / duration);
            float punch = 1f + 0.18f * Mathf.Sin(t * Mathf.PI);
            comboPop.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.7f, 1f, t) * punch;
            comboPop.alpha = 1f - Mathf.Pow(t, 2f);
            yield return null;
        }

        comboPop.alpha = 0f;
        comboPopRoutine = null;
    }

    private void AnimateComboPop()
    {
        if (comboPop == null)
        {
            return;
        }

        comboPop.rectTransform.anchoredPosition = new Vector2(0f, 18f * Mathf.Sin(Time.unscaledTime * 1.4f));
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
        popup.color = manager != null && manager.StormActive ? StormBlue : Gold;

        RectTransform rect = popup.rectTransform;
        Vector2 reference = new Vector2(
            (screen.x / Mathf.Max(1f, camera.pixelWidth) - 0.5f) * ReferenceWidth,
            (screen.y / Mathf.Max(1f, camera.pixelHeight) - 0.5f) * ReferenceHeight);
        rect.anchoredPosition = reference;
        rect.localScale = Vector3.one;

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
            popup.rectTransform.anchoredPosition = start + new Vector2(0f, 70f * t);
            popup.alpha = 1f - Mathf.Pow(t, 2f);
            popup.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.12f, 0.9f, t);
            yield return null;
        }

        popup.gameObject.SetActive(false);
    }

    private TextMeshProUGUI GetPopup()
    {
        if (popupPool.Count < 16)
        {
            TextMeshProUGUI popup = CreateText("Popup", popupLayer, string.Empty, 24f, titleFont, Gold);
            popup.rectTransform.anchorMin = popup.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            popup.rectTransform.sizeDelta = new Vector2(230f, 66f);
            popup.alignment = TextAlignmentOptions.Center;
            AddOutline(popup, Color.black, 0.2f);
            popup.gameObject.SetActive(false);
            popupPool.Add(popup);
            return popup;
        }

        TextMeshProUGUI reused = popupPool[popupCursor];
        popupCursor = (popupCursor + 1) % popupPool.Count;
        return reused;
    }

    // ------------------------------------------------------------------ modal
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
        modalIntroDone = false;
        restartEnabledAt = Time.unscaledTime + 1.2f;

        foreach (TextMeshProUGUI popup in popupPool)
        {
            popup.gameObject.SetActive(false);
        }

        modalGroup.alpha = 1f;
        modalDim.color = new Color(0f, 0f, 0f, 0f);
        modalPanel.localScale = Vector3.one * 0.6f;
        modalTitle.alpha = 0f;
        modalTitle.rectTransform.localScale = Vector3.one * 2.3f;
        modalSubtitle.alpha = 0f;
        modalFinalValue.alpha = 0f;
        modalFinalValue.text = "00000000";
        modalTopLines.alpha = 0f;
        modalPrompt.alpha = 0f;

        hintPill.gameObject.SetActive(false);
        hintText.gameObject.SetActive(false);

        StartCoroutine(AnimateGameOver());
    }

    private IEnumerator AnimateGameOver()
    {
        float time = 0f;

        while (time < 0.42f)
        {
            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / 0.42f);
            modalDim.color = new Color(0f, 0f, 0f, 0.88f * t);
            yield return null;
        }

        time = 0f;
        while (time < 0.55f)
        {
            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / 0.55f);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            float overshoot = Mathf.Sin(t * Mathf.PI) * 0.07f;
            modalPanel.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f + overshoot, eased);
            modalTitle.alpha = t;
            modalTitle.rectTransform.localScale = Vector3.one * Mathf.Lerp(2.3f, 1f, eased);
            modalSubtitle.alpha = t;
            yield return null;
        }

        modalPanel.localScale = Vector3.one;
        modalTitle.rectTransform.localScale = Vector3.one;

        int target = manager != null ? manager.Score : 0;
        time = 0f;
        while (time < 1.05f)
        {
            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / 1.05f);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            modalFinalValue.text = FormatScore(Mathf.RoundToInt(target * eased));
            modalFinalValue.alpha = t;
            yield return null;
        }

        modalFinalValue.text = FormatScore(target);

        string top1Name = manager != null ? ShortName(manager.Top1.name) : "---";
        int top1Score = manager != null ? manager.Top1.score : 0;
        string top2Name = manager != null ? ShortName(manager.Top2.name) : "---";
        int top2Score = manager != null ? manager.Top2.score : 0;

        modalTopLines.text = $"TOP 1   {top1Name}   {FormatScore(top1Score)}\nTOP 2   {top2Name}   {FormatScore(top2Score)}";

        time = 0f;
        while (time < 0.4f)
        {
            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / 0.4f);
            modalTopLines.alpha = t;
            yield return null;
        }

        modalTopLines.alpha = 1f;
        modalPrompt.alpha = 1f;
        modalIntroDone = true;
    }

    public void HideGameOver()
    {
        if (!built)
        {
            return;
        }

        StopAllCoroutines();
        gameOverVisible = false;
        modalIntroDone = false;
        StartCoroutine(FadeOutModal());
    }

    private IEnumerator FadeOutModal()
    {
        float time = 0f;
        float startAlpha = modalGroup.alpha;

        while (time < 0.22f)
        {
            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / 0.22f);
            modalGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
            yield return null;
        }

        modalGroup.alpha = 1f;
        modalRoot.gameObject.SetActive(false);
    }

    private void AnimateFlakes()
    {
        float dt = Time.unscaledDeltaTime;

        for (int i = 0; i < flakes.Count; i++)
        {
            RectTransform flake = flakes[i];
            if (flake == null)
            {
                continue;
            }

            Vector2 position = flake.anchoredPosition;
            position.y -= flakeSpeeds[i] * dt;
            flakePhases[i] += dt * 1.6f;
            position.x += Mathf.Sin(flakePhases[i]) * 22f * dt;

            if (position.y < -560f)
            {
                position.y = 560f;
                position.x = Random.Range(-620f, 620f);
            }

            flake.anchoredPosition = position;
            flake.Rotate(0f, 0f, 150f * dt);
        }
    }

    private void AnimatePrompt()
    {
        if (!modalIntroDone || modalPrompt == null)
        {
            return;
        }

        float pulse = 0.65f + 0.35f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 2.2f));
        modalPrompt.alpha = pulse;
    }

    private void ShowToast(string message)
    {
        if (!built || toastPillText == null || string.IsNullOrEmpty(message))
        {
            return;
        }

        if (toastRoutine != null)
        {
            StopCoroutine(toastRoutine);
        }

        toastRoutine = StartCoroutine(AnimateToast(message));
    }

    private IEnumerator AnimateToast(string message)
    {
        toastPillText.text = message;
        toastPillText.gameObject.SetActive(true);
        toastPill.gameObject.SetActive(true);

        RectTransform rect = toastPillText.rectTransform;
        float time = 0f;
        const float duration = 1.7f;

        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / duration);
            float alpha = t < 0.15f ? t / 0.15f : 1f - Mathf.Pow(Mathf.Clamp01((t - 0.7f) / 0.3f), 2f);
            toastPillText.alpha = alpha;
            toastPill.color = new Color(0.04f, 0.03f, 0.02f, 0.78f * alpha);
            rect.anchoredPosition = new Vector2(0f, Mathf.Lerp(-80f, -64f, t));
            yield return null;
        }

        toastPillText.gameObject.SetActive(false);
        toastPill.gameObject.SetActive(false);
        toastRoutine = null;
    }

    // ----------------------------------------------------------------- helpers
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
        foreach (MeshFilter filter in FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
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
        return name.Length > 9 ? name.Substring(0, 9) : name;
    }

    private RectTransform CreateCard(string name, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot)
    {
        RectTransform rect = CreateRect(name, canvas.transform);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        Image background = rect.gameObject.AddComponent<Image>();
        background.sprite = roundedPanel;
        background.type = Image.Type.Sliced;
        background.raycastTarget = false;
        return rect;
    }

    private void AddAccent(RectTransform parent, Vector2 anchor, Vector2 position, Vector2 size)
    {
        Image accent = CreateImage("Accent", parent, null, new Color(0.95f, 0.76f, 0.38f, 0.85f));
        accent.rectTransform.anchorMin = accent.rectTransform.anchorMax = anchor;
        accent.rectTransform.pivot = new Vector2(0f, 1f);
        accent.rectTransform.sizeDelta = size;
        accent.rectTransform.anchoredPosition = position;
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

    private TextMeshProUGUI CreateLabel(string name, Transform parent, string content, float size, Color color)
    {
        return CreateText(name, parent, content, size, labelFont, color);
    }

    private static void AddOutline(TextMeshProUGUI text, Color color, float width)
    {
        if (text == null || text.font == null)
        {
            return;
        }

        Material material = new Material(text.font.material);
        material.EnableKeyword("OUTLINE_ON");
        material.SetColor("_OutlineColor", color);
        material.SetFloat("_OutlineWidth", width);
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

    private static void Place(RectTransform rect, Vector2 position, Vector2 size, Vector2 anchor, Vector2 pivot)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}
