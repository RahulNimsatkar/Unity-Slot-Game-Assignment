using System.Collections.Generic;
using SlotGame;
using SlotGame.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SlotGame.EditorTools
{
    /// <summary>
    /// One-click scene builder.
    ///
    /// Menu path:  Slot Game ▸ Build Scene
    ///
    /// What it does (in order):
    ///   1. Runs SpriteConfigurator → correct import settings on every texture.
    ///   2. Runs SoundGenerator    → procedurally generates all 7 sound effects.
    ///   3. Creates a brand-new scene and populates it:
    ///        - Canvas (1080×1920 reference, Scale-With-Screen-Size)
    ///        - Background image (full canvas fill)
    ///        - Machine body overlay
    ///        - Marquee overlay
    ///        - Glass overlay (over the reel area)
    ///        - 3 masked reels with symbol strips
    ///        - Lever (clickable, swaps Up/Down sprites)
    ///        - HUD: Credits, Bet, Win readouts + message banner
    ///        - Bet Up / Bet Down / Spin buttons
    ///        - Win/Bankruptcy popups
    ///        - AudioManager with two AudioSources + all clips wired
    ///        - SlotMachineController with symbol definitions + reel refs
    ///        - GameUI wiring
    ///        - EventSystem (required for all UI interaction)
    ///   4. Saves the scene to Assets/Scenes/SlotMachine.unity
    ///
    /// All positions were measured from the source art composites so the layout
    /// matches pixel-perfectly on 1080×1920 (9:16 portrait) canvases and scales
    /// gracefully to other aspect ratios via the CanvasScaler.
    /// </summary>
    public static class SceneBuilder
    {
        // Reference resolution (portrait mobile / WebGL).
        private const float RefW = 1080f;
        private const float RefH = 1920f;

        // Reel window geometry (measured from the Machine_Body / Glass_Overlay).
        // Three reel columns, each showing 3 symbols through a masked window.
        private static readonly float[] ReelX = { 282f, 412f, 542f }; // center X per reel
        private const float ReelCenterY = 350f;  // center Y of the reel window
        private const float ReelWindowW = 108f;  // width of each reel window
        private const float ReelWindowH = 210f;  // height of the visible area
        private const float SymbolSize  = 64f;   // symbol sprite display size
        private const float SymbolSpacing = 70f;  // distance between symbol centers
        private const int   SymbolsPerReel = 5;   // enough to fill the window + recycling buffer

        // HUD positions (relative to canvas center).
        private const float HudY = -140f;
        private const float MsgY = 180f;
        private const float WinTextY = 140f;

        // Lever position (right of the machine).
        private const float LeverX = 680f;
        private const float LeverY = 350f;

        [MenuItem("Slot Game/Build Scene %#b")]
        public static void Build()
        {
            // Step 1 — sprites.
            SpriteConfigurator.ConfigureAll();

            // Step 2 — sounds.
            SoundGenerator.GenerateAll();
            AssetDatabase.Refresh();

            // Step 3 — scene.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Camera (solid dark background for the canvas).
            var camGo = new GameObject("Main Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.02f, 0.1f);
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            camGo.AddComponent<AudioListener>();
            camGo.tag = "MainCamera";

            // ──────────────────────────────────────────── Canvas
            var canvasGo = CreateUI("Canvas", null);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RefW, RefH);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            // EventSystem (so buttons and the lever respond to clicks).
            var eventGo = new GameObject("EventSystem");
            eventGo.AddComponent<EventSystem>();
            eventGo.AddComponent<StandaloneInputModule>();

            // ──────────────────────────────────────────── Background
            CreateImage("Background", canvasGo, LoadSprite(AssetPaths.Background),
                Vector2.zero, new Vector2(RefW, RefH), true);

            // ──────────────────────────────────────────── Machine body
            var machineSprite = LoadSprite(AssetPaths.MachineBody);
            float machineH = machineSprite != null ? machineSprite.rect.height : 800f;
            float machineW = machineSprite != null ? machineSprite.rect.width : 600f;
            CreateImage("Machine_Body", canvasGo, machineSprite,
                new Vector2(RefW * 0.5f - RefW * 0.5f, ReelCenterY - RefH * 0.5f + 100f),
                new Vector2(machineW, machineH), false);

            // ──────────────────────────────────────────── Marquee
            var marqueeSprite = LoadSprite(AssetPaths.Marquee);
            if (marqueeSprite != null)
            {
                CreateImage("Marquee", canvasGo, marqueeSprite,
                    new Vector2(0f, ReelCenterY - RefH * 0.5f + machineH * 0.5f + 60f),
                    new Vector2(marqueeSprite.rect.width, marqueeSprite.rect.height), false);
            }

            // ──────────────────────────────────────────── Reels
            var reelComponents = new Reel[3];
            for (int r = 0; r < 3; r++)
            {
                // Mask container — clips the reel strip to the window.
                var reelGo = CreateUI("Reel_" + r, canvasGo);
                var reelRect = reelGo.GetComponent<RectTransform>();
                SetAnchors(reelRect, 0.5f, 0.5f, 0.5f, 0.5f);
                reelRect.anchoredPosition = new Vector2(ReelX[r] - RefW * 0.5f,
                    ReelCenterY - RefH * 0.5f);
                reelRect.sizeDelta = new Vector2(ReelWindowW, ReelWindowH);

                var mask = reelGo.AddComponent<RectMask2D>();

                // Strip — the scrollable child that holds all symbol images.
                var stripGo = CreateUI("Strip", reelGo);
                var stripRect = stripGo.GetComponent<RectTransform>();
                SetAnchors(stripRect, 0.5f, 0.5f, 0.5f, 0.5f);
                stripRect.anchoredPosition = Vector2.zero;
                stripRect.sizeDelta = new Vector2(ReelWindowW, SymbolSpacing * SymbolsPerReel);

                var images = new Image[SymbolsPerReel];
                float startY = (SymbolsPerReel - 1) * 0.5f * SymbolSpacing;
                for (int s = 0; s < SymbolsPerReel; s++)
                {
                    var symGo = CreateUI("Symbol_" + s, stripGo);
                    var symRect = symGo.GetComponent<RectTransform>();
                    SetAnchors(symRect, 0.5f, 0.5f, 0.5f, 0.5f);
                    symRect.anchoredPosition = new Vector2(0f, startY - s * SymbolSpacing);
                    symRect.sizeDelta = new Vector2(SymbolSize, SymbolSize);

                    var img = symGo.AddComponent<Image>();
                    img.raycastTarget = false;
                    images[s] = img;
                }

                var reel = reelGo.AddComponent<Reel>();
                // Wire serialized fields via SerializedObject for reliability.
                var so = new SerializedObject(reel);
                so.FindProperty("_strip").objectReferenceValue = stripRect;

                var arrProp = so.FindProperty("_symbolImages");
                arrProp.arraySize = SymbolsPerReel;
                for (int s = 0; s < SymbolsPerReel; s++)
                    arrProp.GetArrayElementAtIndex(s).objectReferenceValue = images[s];

                so.FindProperty("_spacing").floatValue = SymbolSpacing;
                so.FindProperty("_windowHalfHeight").floatValue = ReelWindowH * 0.5f;
                so.ApplyModifiedPropertiesWithoutUndo();

                reelComponents[r] = reel;
            }

            // ──────────────────────────────────────────── Glass overlay (on top of reels)
            var glassSprite = LoadSprite(AssetPaths.GlassOverlay);
            if (glassSprite != null)
            {
                CreateImage("Glass_Overlay", canvasGo, glassSprite,
                    new Vector2(0f, ReelCenterY - RefH * 0.5f),
                    new Vector2(glassSprite.rect.width, glassSprite.rect.height), false);
            }

            // ──────────────────────────────────────────── Lever
            var leverGo = CreateUI("Lever", canvasGo);
            var leverRect = leverGo.GetComponent<RectTransform>();
            SetAnchors(leverRect, 0.5f, 0.5f, 0.5f, 0.5f);
            leverRect.anchoredPosition = new Vector2(LeverX - RefW * 0.5f, LeverY - RefH * 0.5f);

            var leverUpSprite = LoadSprite(AssetPaths.LeverUp);
            var leverDownSprite = LoadSprite(AssetPaths.LeverDown);
            float leverH = leverUpSprite != null ? leverUpSprite.rect.height : 200f;
            float leverW = leverUpSprite != null ? leverUpSprite.rect.width : 60f;
            leverRect.sizeDelta = new Vector2(leverW, leverH);

            // Lever Up image (visible at rest).
            var leverUpGo = CreateImage("Lever_Up", leverGo, leverUpSprite,
                Vector2.zero, new Vector2(leverW, leverH), false);

            // Lever Down image (visible when pulled).
            var leverDownGo = CreateImage("Lever_Down", leverGo, leverDownSprite,
                Vector2.zero, new Vector2(leverW, leverH), false);
            leverDownGo.SetActive(false);

            // Transparent raycast target covering the lever area for click detection.
            var leverClickImg = leverGo.AddComponent<Image>();
            leverClickImg.color = new Color(0, 0, 0, 0); // fully transparent
            leverClickImg.raycastTarget = true;

            // ──────────────────────────────────────────── HUD
            float hudLeft = -320f;
            float hudSpacing = 210f;

            // Credits display
            var creditsLabel = CreateText("Credits_Label", canvasGo,
                new Vector2(hudLeft, HudY), "CREDITS", 28, TextAnchor.MiddleCenter, Color.yellow);
            var creditsValue = CreateText("Credits_Value", canvasGo,
                new Vector2(hudLeft, HudY - 40f), "1000", 36, TextAnchor.MiddleCenter, Color.white);

            // Bet display
            var betLabel = CreateText("Bet_Label", canvasGo,
                new Vector2(hudLeft + hudSpacing, HudY), "BET", 28, TextAnchor.MiddleCenter, Color.yellow);
            var betValue = CreateText("Bet_Value", canvasGo,
                new Vector2(hudLeft + hudSpacing, HudY - 40f), "10", 36, TextAnchor.MiddleCenter, Color.white);

            // Win display
            var winLabel = CreateText("Win_Label", canvasGo,
                new Vector2(hudLeft + hudSpacing * 2f, HudY), "WIN", 28, TextAnchor.MiddleCenter, Color.yellow);
            var winValue = CreateText("Win_Value", canvasGo,
                new Vector2(hudLeft + hudSpacing * 2f, HudY - 40f), "0", 36, TextAnchor.MiddleCenter, Color.white);

            // Message banner
            var messageText = CreateText("Message", canvasGo,
                new Vector2(0f, MsgY), "PLACE YOUR BET AND PULL THE LEVER", 30,
                TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.2f));

            // Big win text (above the reels)
            var winText = CreateText("WinText", canvasGo,
                new Vector2(0f, WinTextY), "", 52,
                TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0f));
            var winPulseAnim = winText.AddComponent<Animation>();

            // ──────────────────────────────────────────── Buttons
            float btnY = HudY - 120f;
            float btnSize = 80f;

            // Bet Down (left arrow)
            var betDownBtn = CreateButton("BetDown_Btn", canvasGo,
                new Vector2(hudLeft + hudSpacing - 80f, btnY),
                new Vector2(btnSize, btnSize),
                LoadSubSprite(AssetPaths.ButtonLeftSheet, "Left_Normal"),
                LoadSubSprite(AssetPaths.ButtonLeftSheet, "Left_Highlighted"),
                LoadSubSprite(AssetPaths.ButtonLeftSheet, "Left_Pressed"),
                LoadSubSprite(AssetPaths.ButtonLeftSheet, "Left_Disabled"));

            // Bet Up (right arrow)
            var betUpBtn = CreateButton("BetUp_Btn", canvasGo,
                new Vector2(hudLeft + hudSpacing + 80f, btnY),
                new Vector2(btnSize, btnSize),
                LoadSubSprite(AssetPaths.ButtonRightSheet, "Right_Normal"),
                LoadSubSprite(AssetPaths.ButtonRightSheet, "Right_Highlighted"),
                LoadSubSprite(AssetPaths.ButtonRightSheet, "Right_Pressed"),
                LoadSubSprite(AssetPaths.ButtonRightSheet, "Right_Disabled"));

            // Spin button (uses the close sheet visual as a big round button; label "SPIN")
            var spinBtnGo = CreateButton("Spin_Btn", canvasGo,
                new Vector2(0f, btnY - 80f),
                new Vector2(160f, 60f), null, null, null, null);
            var spinLabel = CreateText("Spin_Label", spinBtnGo,
                Vector2.zero, "SPIN", 34, TextAnchor.MiddleCenter, Color.white);
            // Give the spin button a solid look.
            var spinImg = spinBtnGo.GetComponent<Image>();
            if (spinImg != null)
            {
                spinImg.color = new Color(0.1f, 0.6f, 0.2f); // green
            }

            // ──────────────────────────────────────────── Popup (Out of Credits)
            var popupGo = CreateUI("Popup", canvasGo);
            var popupRect = popupGo.GetComponent<RectTransform>();
            SetAnchors(popupRect, 0f, 0f, 1f, 1f);
            popupRect.offsetMin = Vector2.zero;
            popupRect.offsetMax = Vector2.zero;

            // Dim overlay.
            var dimImg = popupGo.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.6f);
            dimImg.raycastTarget = true;

            // Panel.
            var panelSprite = LoadSprite(AssetPaths.PopupPanel);
            var panelGo = CreateImage("Panel", popupGo, panelSprite,
                Vector2.zero, new Vector2(600f, 400f), false);

            var popupTitle = CreateText("Popup_Title", panelGo, new Vector2(0f, 120f),
                "OUT OF CREDITS", 38, TextAnchor.MiddleCenter, Color.yellow);
            var popupMessage = CreateText("Popup_Message", panelGo, new Vector2(0f, 40f),
                "Refill your balance?", 28, TextAnchor.MiddleCenter, Color.white);

            // Yes / No buttons inside popup.
            var yesBtn = CreateButton("Yes_Btn", panelGo,
                new Vector2(-100f, -80f), new Vector2(150f, 60f),
                LoadSubSprite(AssetPaths.ButtonYesNoSheet, "Yes_Normal"),
                LoadSubSprite(AssetPaths.ButtonYesNoSheet, "Yes_Highlighted"),
                LoadSubSprite(AssetPaths.ButtonYesNoSheet, "Yes_Pressed"),
                null);
            var yesLabel = CreateText("Yes_Label", yesBtn, Vector2.zero, "YES", 30,
                TextAnchor.MiddleCenter, Color.white);

            var noBtn = CreateButton("No_Btn", panelGo,
                new Vector2(100f, -80f), new Vector2(150f, 60f),
                LoadSubSprite(AssetPaths.ButtonYesNoSheet, "No_Normal"),
                LoadSubSprite(AssetPaths.ButtonYesNoSheet, "No_Highlighted"),
                LoadSubSprite(AssetPaths.ButtonYesNoSheet, "No_Pressed"),
                null);
            var noLabel = CreateText("No_Label", noBtn, Vector2.zero, "NO", 30,
                TextAnchor.MiddleCenter, Color.white);

            var closeBtn = CreateButton("Close_Btn", panelGo,
                new Vector2(260f, 160f), new Vector2(50f, 50f),
                LoadSubSprite(AssetPaths.ButtonCloseSheet, "Close_Normal"),
                LoadSubSprite(AssetPaths.ButtonCloseSheet, "Close_Highlighted"),
                LoadSubSprite(AssetPaths.ButtonCloseSheet, "Close_Pressed"),
                LoadSubSprite(AssetPaths.ButtonCloseSheet, "Close_Disabled"));

            // PopupController component.
            var popup = popupGo.AddComponent<PopupController>();
            var popupSO = new SerializedObject(popup);
            popupSO.FindProperty("_titleText").objectReferenceValue = popupTitle.GetComponent<Text>();
            popupSO.FindProperty("_messageText").objectReferenceValue = popupMessage.GetComponent<Text>();
            popupSO.FindProperty("_yesButton").objectReferenceValue = yesBtn.GetComponent<Button>();
            popupSO.FindProperty("_noButton").objectReferenceValue = noBtn.GetComponent<Button>();
            popupSO.FindProperty("_closeButton").objectReferenceValue = closeBtn.GetComponent<Button>();
            popupSO.FindProperty("_animatedTarget").objectReferenceValue = panelGo.GetComponent<RectTransform>();
            popupSO.ApplyModifiedPropertiesWithoutUndo();
            popupGo.SetActive(false); // hidden by default

            // ──────────────────────────────────────────── AudioManager
            var audioGo = new GameObject("AudioManager");
            audioGo.transform.SetParent(canvasGo.transform, false);

            var sfxSource = audioGo.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;

            var loopSource = audioGo.AddComponent<AudioSource>();
            loopSource.playOnAwake = false;
            loopSource.loop = true;

            var audioMgr = audioGo.AddComponent<AudioManager>();
            var audioSO = new SerializedObject(audioMgr);
            audioSO.FindProperty("_sfxSource").objectReferenceValue = sfxSource;
            audioSO.FindProperty("_loopSource").objectReferenceValue = loopSource;

            // Wire audio clips.
            audioSO.FindProperty("_buttonClick").objectReferenceValue    = LoadClip("click");
            audioSO.FindProperty("_leverPull").objectReferenceValue      = LoadClip("lever");
            audioSO.FindProperty("_reelStop").objectReferenceValue       = LoadClip("reel_stop");
            audioSO.FindProperty("_spinLoop").objectReferenceValue       = LoadClip("spin_loop");
            audioSO.FindProperty("_winJingle").objectReferenceValue      = LoadClip("win");
            audioSO.FindProperty("_jackpotJingle").objectReferenceValue  = LoadClip("jackpot");
            audioSO.FindProperty("_loseSting").objectReferenceValue      = LoadClip("lose");
            audioSO.ApplyModifiedPropertiesWithoutUndo();

            // ──────────────────────────────────────────── SlotMachineController
            var controllerGo = new GameObject("SlotMachineController");
            controllerGo.transform.SetParent(canvasGo.transform, false);
            var controller = controllerGo.AddComponent<SlotMachineController>();

            // Build SymbolDefinition list with sprites.
            var ctrlSO = new SerializedObject(controller);

            // Symbol definitions (one per SymbolType).
            var symProp = ctrlSO.FindProperty("_symbols");
            symProp.arraySize = 4;

            SetSymbolDef(symProp, 0, SymbolType.Seven,  LoadSprite(AssetPaths.SymbolSeven));
            SetSymbolDef(symProp, 1, SymbolType.Bar,    LoadSprite(AssetPaths.SymbolBar));
            SetSymbolDef(symProp, 2, SymbolType.Bell,   LoadSprite(AssetPaths.SymbolBell));
            SetSymbolDef(symProp, 3, SymbolType.Cherry, LoadSprite(AssetPaths.SymbolCherry));

            // Reel references.
            var reelsProp = ctrlSO.FindProperty("_reels");
            reelsProp.arraySize = 3;
            for (int i = 0; i < 3; i++)
                reelsProp.GetArrayElementAtIndex(i).objectReferenceValue = reelComponents[i];

            ctrlSO.FindProperty("_audio").objectReferenceValue = audioMgr;
            ctrlSO.ApplyModifiedPropertiesWithoutUndo();

            // ──────────────────────────────────────────── LeverController
            var leverCtrl = leverGo.AddComponent<LeverController>();
            var leverSO = new SerializedObject(leverCtrl);
            leverSO.FindProperty("_leverUp").objectReferenceValue = leverUpGo;
            leverSO.FindProperty("_leverDown").objectReferenceValue = leverDownGo;
            leverSO.FindProperty("_machine").objectReferenceValue = controller;
            leverSO.FindProperty("_audio").objectReferenceValue = audioMgr;
            leverSO.ApplyModifiedPropertiesWithoutUndo();

            // ──────────────────────────────────────────── GameUI
            var uiGo = new GameObject("GameUI");
            uiGo.transform.SetParent(canvasGo.transform, false);
            var gameUI = uiGo.AddComponent<GameUI>();
            var uiSO = new SerializedObject(gameUI);
            uiSO.FindProperty("_machine").objectReferenceValue = controller;
            uiSO.FindProperty("_audio").objectReferenceValue = audioMgr;
            uiSO.FindProperty("_creditsValue").objectReferenceValue = creditsValue.GetComponent<Text>();
            uiSO.FindProperty("_betValue").objectReferenceValue = betValue.GetComponent<Text>();
            uiSO.FindProperty("_winValue").objectReferenceValue = winValue.GetComponent<Text>();
            uiSO.FindProperty("_messageText").objectReferenceValue = messageText.GetComponent<Text>();
            uiSO.FindProperty("_winText").objectReferenceValue = winText.GetComponent<Text>();
            uiSO.FindProperty("_spinButton").objectReferenceValue = spinBtnGo.GetComponent<Button>();
            uiSO.FindProperty("_betUpButton").objectReferenceValue = betUpBtn.GetComponent<Button>();
            uiSO.FindProperty("_betDownButton").objectReferenceValue = betDownBtn.GetComponent<Button>();
            uiSO.FindProperty("_popup").objectReferenceValue = popup;
            uiSO.FindProperty("_winPulse").objectReferenceValue = winPulseAnim;
            uiSO.ApplyModifiedPropertiesWithoutUndo();

            // ──────────────────────────────────────────── Save
            EditorSceneManager.SaveScene(scene, AssetPaths.ScenePath);
            Debug.Log("[SlotGame] ★ Scene built and saved to " + AssetPaths.ScenePath);

            // Add scene to Build Settings so WebGL build can find it.
            var buildScenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            bool found = false;
            foreach (var bs in buildScenes)
            {
                if (bs.path == AssetPaths.ScenePath) { found = true; break; }
            }
            if (!found)
            {
                buildScenes.Add(new EditorBuildSettingsScene(AssetPaths.ScenePath, true));
                EditorBuildSettings.scenes = buildScenes.ToArray();
                Debug.Log("[SlotGame] Scene added to Build Settings.");
            }

            Selection.activeGameObject = canvasGo;
            Debug.Log("[SlotGame] ★ Build complete! Press Play to test.");
        }

        // ═══════════════════════════════════════════════ helper methods

        private static void SetSymbolDef(SerializedProperty arrayProp, int index,
            SymbolType type, Sprite sprite)
        {
            var elem = arrayProp.GetArrayElementAtIndex(index);
            elem.FindPropertyRelative("Type").enumValueIndex = (int)type;
            elem.FindPropertyRelative("Sprite").objectReferenceValue = sprite;
        }

        private static Sprite LoadSprite(string assetPath)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        /// <summary>
        /// Loads a named sub-sprite from a Multiple-mode sprite sheet.
        /// Returns null gracefully when the sheet or sub-sprite is not found.
        /// </summary>
        private static Sprite LoadSubSprite(string sheetPath, string subName)
        {
            var all = AssetDatabase.LoadAllAssetsAtPath(sheetPath);
            foreach (var obj in all)
            {
                if (obj is Sprite sp && sp.name == subName)
                    return sp;
            }
            // Fallback: try loading the sheet as a single sprite.
            return AssetDatabase.LoadAssetAtPath<Sprite>(sheetPath);
        }

        private static AudioClip LoadClip(string nameWithoutExt)
        {
            string path = AssetPaths.SoundsFolder + "/" + nameWithoutExt + ".wav";
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        // ─────────────────────────── UI construction helpers

        private static GameObject CreateUI(string name, GameObject parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null)
                go.transform.SetParent(parent.transform, false);
            return go;
        }

        private static GameObject CreateImage(string name, GameObject parent,
            Sprite sprite, Vector2 anchoredPos, Vector2 size, bool stretch)
        {
            var go = CreateUI(name, parent);
            var rt = go.GetComponent<RectTransform>();
            if (stretch)
            {
                SetAnchors(rt, 0f, 0f, 1f, 1f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
            else
            {
                SetAnchors(rt, 0.5f, 0.5f, 0.5f, 0.5f);
                rt.anchoredPosition = anchoredPos;
                rt.sizeDelta = size;
            }
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = !stretch;
            img.raycastTarget = false;
            return go;
        }

        private static GameObject CreateText(string name, GameObject parent,
            Vector2 anchoredPos, string content, int fontSize,
            TextAnchor alignment, Color color)
        {
            var go = CreateUI(name, parent);
            var rt = go.GetComponent<RectTransform>();
            SetAnchors(rt, 0.5f, 0.5f, 0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(500f, 60f);

            var text = go.AddComponent<Text>();
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;

            // Add outline for readability against any background.
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            return go;
        }

        private static GameObject CreateButton(string name, GameObject parent,
            Vector2 anchoredPos, Vector2 size,
            Sprite normal, Sprite highlighted, Sprite pressed, Sprite disabled)
        {
            var go = CreateUI(name, parent);
            var rt = go.GetComponent<RectTransform>();
            SetAnchors(rt, 0.5f, 0.5f, 0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            var img = go.AddComponent<Image>();
            img.sprite = normal;
            img.type = Image.Type.Simple;
            img.preserveAspect = true;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            if (normal != null || highlighted != null || pressed != null || disabled != null)
            {
                btn.transition = Selectable.Transition.SpriteSwap;
                var ss = new SpriteState();
                if (highlighted != null) ss.highlightedSprite = highlighted;
                if (pressed != null) ss.pressedSprite = pressed;
                if (disabled != null) ss.disabledSprite = disabled;
                btn.spriteState = ss;
            }
            else
            {
                btn.transition = Selectable.Transition.ColorTint;
            }

            return go;
        }

        private static void SetAnchors(RectTransform rt,
            float minX, float minY, float maxX, float maxY)
        {
            rt.anchorMin = new Vector2(minX, minY);
            rt.anchorMax = new Vector2(maxX, maxY);
            rt.pivot = new Vector2(0.5f, 0.5f);
        }
    }
}
