#if UNITY_EDITOR
using System.Linq;
using ChaseGame.Match;
using ChaseGame.Menu;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ChaseGame.Editor
{
    /// <summary>
    /// One-click, re-runnable setup for the Loading → Menu → Game flow:
    /// builds Assets/Scenes/Menu.unity, adds the role-reveal card to the Game HUD,
    /// relabels the match "Thoát" buttons to "Về Menu", and fixes the Build Settings order.
    /// </summary>
    public static class MenuAndRoleRevealSetup
    {
        private const string LoadingScenePath = "Assets/Scenes/Loading.unity";
        private const string MenuScenePath = "Assets/Scenes/Menu.unity";
        private const string GameScenePath = "Assets/Scenes/Game.unity";
        private const string RoleRevealCardName = "RoleRevealCard";
        private const string BackToMenuLabel = "Về Menu";

        private const string GuideText =
            "<b>HAI PHE</b>\n" +
            "Mỗi trận có 4 Gà (phe đuổi) và 4 Người (phe trốn). Bạn được chia ngẫu nhiên vào một phe, các thành viên còn lại do máy điều khiển.\n\n" +
            "<b>DIỄN BIẾN TRẬN</b>\n" +
            "• 10 giây đầu: Người chạy trước tìm chỗ trốn, Gà bị khóa tại chỗ (vẫn xoay camera quan sát được).\n" +
            "• Sau 3-2-1-READY, Gà được thả và trận đấu kéo dài 90 giây.\n" +
            "• Người trúng trứng sẽ bị nhốt vào lồng tại chỗ đến hết trận — không có giải cứu.\n\n" +
            "<b>THẮNG / THUA</b>\n" +
            "• Gà thắng nếu nhốt hết phe Người trước khi hết giờ.\n" +
            "• Người thắng nếu hết 90 giây vẫn còn người chưa bị bắt.\n\n" +
            "<b>ĐIỀU KHIỂN</b>\n" +
            "• Joystick bên trái: di chuyển.\n" +
            "• Vuốt nửa màn hình bên phải: xoay camera.\n" +
            "• Gà: nút Bắn trứng và Tăng tốc. Người: nút Tăng tốc và Phân thân.";

        private static readonly Color BgColor = new Color(0.07f, 0.09f, 0.12f, 1f);
        private static readonly Color PanelColor = new Color(0.10f, 0.13f, 0.18f, 0.96f);
        private static readonly Color ButtonColor = new Color(0.20f, 0.50f, 0.85f, 1f);
        private static readonly Color QuitButtonColor = new Color(0.55f, 0.20f, 0.20f, 1f);
        private static readonly Color TitleColor = new Color(1f, 0.84f, 0.25f, 1f);

        [MenuItem("Tools/ChaseGame/Setup Main Menu & Role Reveal UI")]
        public static void Run()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[MenuSetup] Exit Play mode first.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.LogWarning("[MenuSetup] Cancelled: unsaved scene changes were kept.");
                return;
            }

            BuildMenuScene();
            SetupGameScene();
            SetBuildSettings();
            Debug.Log("[MenuSetup] Done. Flow: Loading → Menu → Game.");
        }

        // ---------- Menu scene ----------

        private static void BuildMenuScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = BgColor;

            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<InputSystemUIInputModule>();

            var canvas = CreateCanvas("MenuCanvas");
            var root = canvas.transform;

            var bg = CreateImage("Background", root, BgColor);
            Stretch(bg.rectTransform);

            var title = CreateText("Title", root, "CHASE GAME", 120, TitleColor, FontStyle.Bold);
            Place(title.rectTransform, new Vector2(0.5f, 0.78f), new Vector2(1400, 200));

            var play = CreateButton("PlayButton", root, "Chơi", ButtonColor);
            Place((RectTransform)play.transform, new Vector2(0.5f, 0.52f), new Vector2(520, 120));
            var guide = CreateButton("GuideButton", root, "Hướng dẫn", ButtonColor);
            Place((RectTransform)guide.transform, new Vector2(0.5f, 0.38f), new Vector2(520, 120));
            var quit = CreateButton("QuitButton", root, "Thoát", QuitButtonColor);
            Place((RectTransform)quit.transform, new Vector2(0.5f, 0.24f), new Vector2(520, 120));

            var guidePanel = CreateImage("GuidePanel", root, PanelColor);
            Stretch(guidePanel.rectTransform);
            var guideTitle = CreateText("GuideTitle", guidePanel.transform, "HƯỚNG DẪN", 72, TitleColor, FontStyle.Bold);
            Place(guideTitle.rectTransform, new Vector2(0.5f, 0.9f), new Vector2(1200, 110));
            var guideBody = CreateText("GuideBody", guidePanel.transform, GuideText, 34, Color.white, FontStyle.Normal);
            guideBody.alignment = TextAnchor.UpperLeft;
            guideBody.supportRichText = true;
            guideBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            guideBody.verticalOverflow = VerticalWrapMode.Truncate;
            guideBody.resizeTextForBestFit = true;
            guideBody.resizeTextMinSize = 20;
            guideBody.resizeTextMaxSize = 34;
            var bodyRect = guideBody.rectTransform;
            bodyRect.anchorMin = new Vector2(0.08f, 0.17f);
            bodyRect.anchorMax = new Vector2(0.92f, 0.82f);
            bodyRect.offsetMin = Vector2.zero;
            bodyRect.offsetMax = Vector2.zero;
            var close = CreateButton("GuideCloseButton", guidePanel.transform, "Đóng", ButtonColor);
            Place((RectTransform)close.transform, new Vector2(0.5f, 0.08f), new Vector2(420, 110));

            var view = canvas.gameObject.AddComponent<MainMenuView>();
            var so = new SerializedObject(view);
            so.FindProperty("playButton").objectReferenceValue = play;
            so.FindProperty("guideButton").objectReferenceValue = guide;
            so.FindProperty("quitButton").objectReferenceValue = quit;
            so.FindProperty("guidePanel").objectReferenceValue = guidePanel.gameObject;
            so.FindProperty("guideCloseButton").objectReferenceValue = close;
            so.ApplyModifiedPropertiesWithoutUndo();
            guidePanel.gameObject.SetActive(false);

            EditorSceneManager.SaveScene(scene, MenuScenePath);
            Debug.Log("[MenuSetup] Saved " + MenuScenePath);
        }

        // ---------- Game scene ----------

        private static void SetupGameScene()
        {
            var scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);

            var hud = Object.FindObjectsByType<MatchHudView>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            if (hud == null)
            {
                Debug.LogError("[MenuSetup] No MatchHudView in " + GameScenePath + "; role-reveal card not added.");
                return;
            }

            var hudSo = new SerializedObject(hud);

            // The card must render BELOW the pause dialog (pausing during the reveal is allowed),
            // so it goes right before PausePanel among its siblings. Without a pause panel,
            // fall back to the top of the root canvas.
            var pausePanel = hudSo.FindProperty("pausePanel").objectReferenceValue as GameObject;
            var canvas = hud.GetComponentInParent<Canvas>(true);
            Transform parent = pausePanel != null && pausePanel.transform.parent != null
                ? pausePanel.transform.parent
                : canvas != null ? canvas.rootCanvas.transform : hud.transform;

            // Remove any card from a previous run, wherever it was placed.
            foreach (var old in hud.transform.root.GetComponentsInChildren<Transform>(true)
                         .Where(t => t.name == RoleRevealCardName).ToArray())
            {
                Object.DestroyImmediate(old.gameObject);
            }

            var card = CreateImage(RoleRevealCardName, parent, new Color(0.06f, 0.18f, 0.24f, 0.82f));
            card.raycastTarget = false;
            Place(card.rectTransform, new Vector2(0.5f, 0.55f), new Vector2(900, 320));
            if (pausePanel != null && pausePanel.transform.parent == parent)
            {
                card.transform.SetSiblingIndex(pausePanel.transform.GetSiblingIndex()); // pause panel shifts up, stays on top
            }
            else
            {
                card.transform.SetAsLastSibling();
            }

            var title = CreateText("Title", card.transform, "BẠN LÀ NGƯỜI", 96, Color.white, FontStyle.Bold);
            title.raycastTarget = false;
            Place(title.rectTransform, new Vector2(0.5f, 0.64f), new Vector2(860, 140));
            var subtitle = CreateText("Subtitle", card.transform, "Trốn đến hết giờ!", 44, Color.white, FontStyle.Normal);
            subtitle.raycastTarget = false;
            Place(subtitle.rectTransform, new Vector2(0.5f, 0.27f), new Vector2(860, 90));
            card.gameObject.SetActive(false);

            hudSo.Update();
            hudSo.FindProperty("roleRevealPanel").objectReferenceValue = card.gameObject;
            hudSo.FindProperty("roleRevealBg").objectReferenceValue = card;
            hudSo.FindProperty("roleRevealTitleText").objectReferenceValue = title;
            hudSo.FindProperty("roleRevealSubtitleText").objectReferenceValue = subtitle;
            hudSo.ApplyModifiedPropertiesWithoutUndo();

            Relabel(hudSo.FindProperty("pauseQuitButton").objectReferenceValue as Button, "pause menu");

            var endView = Object.FindObjectsByType<MatchEndView>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            if (endView != null)
            {
                var endSo = new SerializedObject(endView);
                Relabel(endSo.FindProperty("quitButton").objectReferenceValue as Button, "end screen");
            }
            else
            {
                Debug.LogWarning("[MenuSetup] No MatchEndView found; end-screen label unchanged.");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[MenuSetup] Added " + RoleRevealCardName + " under '" + parent.name + "' and saved " + GameScenePath);
        }

        private static void Relabel(Button button, string where)
        {
            var label = button != null ? button.GetComponentInChildren<Text>(true) : null;
            if (label == null)
            {
                Debug.LogWarning("[MenuSetup] No label found on the " + where + " quit button.");
                return;
            }

            Undo.RecordObject(label, "Relabel");
            label.text = BackToMenuLabel;
            EditorUtility.SetDirty(label);
        }

        // ---------- Build settings ----------

        private static void SetBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(LoadingScenePath, true),
                new EditorBuildSettingsScene(MenuScenePath, true),
                new EditorBuildSettingsScene(GameScenePath, true),
            };
            Debug.Log("[MenuSetup] Build Settings: Loading (0) → Menu (1) → Game (2)");
        }

        // ---------- UI helpers ----------

        private static Font UiFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        private static Canvas CreateCanvas(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            return img;
        }

        private static Text CreateText(string name, Transform parent, string text, int size, Color color, FontStyle style)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = UiFont;
            t.text = text;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        private static Button CreateButton(string name, Transform parent, string label, Color color)
        {
            var img = CreateImage(name, parent, color);
            var button = img.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            var text = CreateText("Label", img.transform, label, 52, Color.white, FontStyle.Bold);
            text.raycastTarget = false;
            Stretch(text.rectTransform);
            return button;
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
#endif
