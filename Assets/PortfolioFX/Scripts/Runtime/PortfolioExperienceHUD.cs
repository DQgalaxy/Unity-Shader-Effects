using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace PortfolioFX
{
    /// Minimal presentation overlay for a portfolio capture. It communicates
    /// the authored route and exposes the artist controls without covering the
    /// village or the effect silhouettes.
    [DisallowMultipleComponent]
    public sealed class PortfolioExperienceHUD : MonoBehaviour
    {
        public PortfolioShowcaseDirector director;
        Canvas canvas;
        Text phaseText;
        Text stackText;
        Text controlsText;
        Image progressFill;
        Image phaseDot;
        float fade;

        static readonly Color Cyan = new Color(.28f, .9f, 1f, 1f);
        static readonly Color Warm = new Color(1f, .42f, .16f, 1f);
        static readonly Color Panel = new Color(.015f, .035f, .05f, .74f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void InstallForPortfolioScene()
        {
            if (SceneManager.GetActiveScene().name != "TA_Portfolio_Experience") return;
            if (FindFirstObjectByType<PortfolioExperienceHUD>() != null) return;
            var director = FindFirstObjectByType<PortfolioShowcaseDirector>();
            if (director == null) return;
            var go = new GameObject("Portfolio_Experience_Presentation");
            var hud = go.AddComponent<PortfolioExperienceHUD>();
            hud.director = director;
        }

        void Awake()
        {
            if (!director) director = FindFirstObjectByType<PortfolioShowcaseDirector>();
            Build();
        }

        void Build()
        {
            var root = new GameObject("Portfolio_Experience_HUD");
            root.transform.SetParent(transform, false);
            canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            root.AddComponent<GraphicRaycaster>();

            var top = PanelObject("HUD_TopBar", root.transform, new Vector2(0, 1), new Vector2(1, 0), new Vector2(0, -76), new Vector2(0, 0));
            AddText(top.transform, "TA / VIKING THRESHOLD", 22, Cyan, TextAnchor.MiddleLeft, new Vector2(32, 0), new Vector2(520, 60));
            AddText(top.transform, "PORTFOLIO EXPERIENCE  //  REALTIME FX STUDY", 12, new Color(.65f, .8f, .84f, 1), TextAnchor.MiddleLeft, new Vector2(34, -26), new Vector2(720, 34));
            stackText = AddText(top.transform, "", 12, new Color(.72f, .9f, .88f, .95f), TextAnchor.MiddleRight, new Vector2(-36, 0), new Vector2(1040, 54));

            var left = PanelObject("HUD_Phase", root.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -116), new Vector2(0, 0));
            phaseDot = ImageObject(left.transform, "Phase_Dot", Cyan, new Vector2(0, .5f), new Vector2(12, 12), new Vector2(0, 0));
            phaseText = AddText(left.transform, "AUTHORED ROUTE", 16, Color.white, TextAnchor.MiddleLeft, new Vector2(28, 0), new Vector2(360, 34));

            var bottom = PanelObject("HUD_BottomBar", root.transform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 22), new Vector2(0, 0));
            controlsText = AddText(bottom.transform, "", 13, new Color(.84f, .92f, .94f, .95f), TextAnchor.MiddleLeft, new Vector2(32, 0), new Vector2(1100, 44));
            var progressBg = ImageObject(bottom.transform, "Progress_Back", new Color(.09f, .16f, .19f, .88f), new Vector2(1, .5f), new Vector2(520, 5), new Vector2(-290, 0));
            progressFill = ImageObject(progressBg.transform, "Progress_Fill", Cyan, new Vector2(0, .5f), new Vector2(0, 5), Vector2.zero);
            progressFill.rectTransform.sizeDelta = new Vector2(520, 5);
            progressFill.rectTransform.pivot = new Vector2(0, .5f);
            progressFill.type = Image.Type.Filled;
            progressFill.fillMethod = Image.FillMethod.Horizontal;
            progressFill.fillAmount = 0;
            AddText(bottom.transform, "F1  MANUAL", 11, new Color(.55f, .74f, .78f, 1), TextAnchor.MiddleRight, new Vector2(-32, 0), new Vector2(140, 38));
        }

        GameObject PanelObject(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin; rect.anchorMax = anchorMax; rect.pivot = new Vector2(anchorMin.x, anchorMin.y);
            rect.anchoredPosition = anchoredPosition; rect.sizeDelta = size == Vector2.zero ? new Vector2(0, 64) : size;
            var image = go.AddComponent<Image>(); image.color = Panel;
            return go;
        }

        Text AddText(Transform parent, string value, int size, Color color, TextAnchor anchor, Vector2 position, Vector2 dimensions)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(position.x < 0 ? 1 : 0, .5f); rect.anchorMax = rect.anchorMin;
            rect.pivot = new Vector2(position.x < 0 ? 1 : 0, .5f); rect.anchoredPosition = position; rect.sizeDelta = dimensions;
            var text = go.AddComponent<Text>();
            text.text = value; text.font = Resources.GetBuiltinResource<Font>("Arial.ttf"); text.fontSize = size; text.color = color; text.alignment = anchor; text.horizontalOverflow = HorizontalWrapMode.Overflow; text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        Image ImageObject(Transform parent, string name, Color color, Vector2 anchor, Vector2 dimensions, Vector2 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchor; rect.anchorMax = anchor; rect.pivot = anchor; rect.anchoredPosition = position; rect.sizeDelta = dimensions;
            var image = go.AddComponent<Image>(); image.color = color;
            return image;
        }

        void Update()
        {
            if (!director) director = FindFirstObjectByType<PortfolioShowcaseDirector>();
            if (!director || phaseText == null) return;
            fade = Mathf.MoveTowards(fade, 1f, Time.deltaTime * 3f);
            float normalized = Mathf.Clamp01(director.Timeline / 44f);
            progressFill.fillAmount = normalized;
            string mode = director.IsManual ? "MANUAL / LIVE" : "CINEMATIC / LOOP";
            phaseText.text = mode + "    //    " + director.phase.ToString().ToUpperInvariant();
            phaseText.color = director.IsManual ? Warm : Color.white;
            phaseDot.color = director.IsManual ? Warm : Cyan;
            stackText.text = "PORTAL VORTEX     +     HOLOGRAM PROJECTION     +     COMPOSITE COMBAT     +     WATER INTERACTION";
            controlsText.text = director.IsManual
                ? "WASD MOVE   ·   MOUSE LOOK   ·   Q/E/R/T OR 1/2/3/4 FX   ·   MOUSE MMB BURST   ·   TAB / XBUTTON CAMERA   ·   L LIGHT"
                : "F1 ENTER MANUAL INSPECTION   ·   F2 REPLAY AUTHORED ROUTE   ·   F3 / L COOL LIGHTING   ·   ROUTE 44 SEC LOOP";
            var alpha = .72f + Mathf.Sin(Time.time * 2.1f) * .05f;
            progressFill.color = Color.Lerp(Cyan, Warm, director.IsManual ? .2f : 0) * new Color(1, 1, 1, alpha);
        }
    }
}
