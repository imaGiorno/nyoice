using System;
using Nyoice.Toilet;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Nyoice.Editor
{
    public static class NyoiceSprint512FUiWidthVisualRestoreValidator
    {
        private const string GameScenePath = "Assets/_Project/Scenes/GameScene.unity";
        private const float ExpectedWidth = 1220f;

        [MenuItem("Nyoice/Validate Sprint5-12F UI Width Alignment & Visual Restore")]
        public static void ValidateUiWidthVisualRestore()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GameScenePath) == null)
            {
                NyoiceProjectSetup.SetupSprintOne();
            }

            NyoiceGameStageSetup.SetupGameStage();
            UrinalSnapshot[] snapshots = CaptureUrinals();
            NyoiceGameStageSetup.SetupGameStage();

            Transform canvas = GameObject.Find("UI/DiscomfortCanvas")?.transform;
            Require(canvas != null, "HUD canvas is missing.");
            ValidateUi(canvas);
            ValidateHighlight(canvas, snapshots);
            ValidateVisualVisibility();
            NyoiceSprint512EHudReadabilityDebugCleanupValidator.ValidateHudReadabilityDebugCleanup();
            Debug.Log("Sprint5-12F UI Width Alignment & Visual Restore validation passed.");
        }

        private static void ValidateUi(Transform canvas)
        {
            RectTransform hud = canvas.Find("HUDRoot/HUDContainer")?.GetComponent<RectTransform>();
            RectTransform lower = canvas.Find("LowerInfoRoot")?.GetComponent<RectTransform>();
            Require(hud != null && lower != null, "HUDContainer or LowerInfoRoot is missing.");
            Require(Mathf.Approximately(hud.sizeDelta.x, ExpectedWidth) &&
                    Mathf.Approximately(lower.sizeDelta.x, ExpectedWidth),
                "Upper and lower UI widths must both be 1220 pixels.");
            Require(hud.anchorMin.x == 0.5f && lower.anchorMin.x == 0.5f &&
                    Mathf.Approximately(hud.anchoredPosition.x, lower.anchoredPosition.x),
                "Upper and lower UI horizontal centers do not match.");
            Require(Mathf.Approximately(hud.anchoredPosition.x - (hud.sizeDelta.x * hud.pivot.x),
                                       lower.anchoredPosition.x - (lower.sizeDelta.x * lower.pivot.x)) &&
                    Mathf.Approximately(hud.anchoredPosition.x + (hud.sizeDelta.x * (1f - hud.pivot.x)),
                                       lower.anchoredPosition.x + (lower.sizeDelta.x * (1f - lower.pivot.x))),
                "Upper and lower UI edges do not align.");
            Require(Mathf.Approximately(lower.sizeDelta.y, 140f), "LowerInfoRoot height changed.");

            RectTransform rule = RequireDirectChild(lower, "RulePanel").GetComponent<RectTransform>();
            RectTransform guide = RequireDirectChild(lower, "ComboGuidePanel").GetComponent<RectTransform>();
            RectTransform stage = RequireDirectChild(lower, "StageInfoPanel").GetComponent<RectTransform>();
            Require(Mathf.Approximately(rule.sizeDelta.x, 380f) &&
                    Mathf.Approximately(guide.sizeDelta.x, 540f) &&
                    Mathf.Approximately(stage.sizeDelta.x, 252f),
                "LowerInfo panel widths do not match 380/540/252.");
            Require(Mathf.Approximately(guide.anchoredPosition.x - (rule.anchoredPosition.x + rule.sizeDelta.x), 24f) &&
                    Mathf.Approximately(stage.anchoredPosition.x - (guide.anchoredPosition.x + guide.sizeDelta.x), 24f),
                "LowerInfo panel gaps are not consistently 24 pixels.");

            Text ruleText = rule.Find("BodyText").GetComponent<Text>();
            Text guideText = guide.Find("BodyText").GetComponent<Text>();
            Text stageText = stage.Find("BodyText").GetComponent<Text>();
            Require(ruleText.fontSize == 22 && guideText.fontSize == 21 && stageText.fontSize == 20,
                "LowerInfo text sizes changed.");
            Require(ruleText.text.Contains("GameOver") &&
                    guideText.text.Contains("5 COMBO") && guideText.text.Contains("50 COMBO") &&
                    stageText.text.Contains("LEVEL") && stageText.text.Contains("SPAWN") && stageText.text.Contains("PEE"),
                "LowerInfo content was reduced.");
            Require(guideText.alignment == TextAnchor.UpperLeft, "ComboGuide is not left aligned.");
            foreach (Graphic graphic in lower.GetComponentsInChildren<Graphic>(true))
            {
                Require(!graphic.raycastTarget, $"LowerInfo graphic {graphic.name} blocks raycasts.");
            }

            ValidateScreenWidth(lower, new Vector2(1920f, 1080f));
            ValidateScreenWidth(lower, new Vector2(2340f, 1080f));
            ValidateScreenWidth(lower, new Vector2(1280f, 720f));
        }

        private static void ValidateHighlight(Transform canvas, UrinalSnapshot[] snapshots)
        {
            Camera camera = Camera.main;
            RectTransform hudRoot = canvas.Find("HUDRoot")?.GetComponent<RectTransform>();
            Require(camera != null && hudRoot != null, "Camera or HUDRoot is missing.");
            float hudBottomPixels = -hudRoot.anchoredPosition.y + hudRoot.sizeDelta.y;
            float hudBottomWorldY = camera.ViewportToWorldPoint(
                new Vector3(0.5f, 1f - (hudBottomPixels / 1080f), 0f)).y;
            UrinalController[] urinals = GameObject.Find("GameStage/Urinals")
                ?.GetComponentsInChildren<UrinalController>(true) ?? Array.Empty<UrinalController>();
            Require(urinals.Length == 8 && snapshots.Length == 8, "The stage must retain eight urinals.");
            float minimumGap = float.MaxValue;

            for (int index = 0; index < urinals.Length; index++)
            {
                UrinalController controller = urinals[index];
                Transform pixel = controller.transform.Find("VisualRoot/PixelVisual");
                SpriteRenderer sprite = pixel?.GetComponent<SpriteRenderer>();
                Transform highlight = controller.transform.Find("Highlight");
                Require(sprite != null && sprite.sprite != null && highlight != null, "Urinal visual is incomplete.");
                float spriteWidth = sprite.sprite.bounds.size.x * Mathf.Abs(pixel.localScale.x);
                float spriteHeight = sprite.sprite.bounds.size.y * Mathf.Abs(pixel.localScale.y);
                float frameWidth = highlight.Find("Top").localScale.x;
                float frameHeight = highlight.Find("Left").localScale.y;
                Vector3 spriteCenter = controller.transform.TransformPoint(pixel.localPosition);
                float frameTop = highlight.position.y + (frameHeight * 0.5f);
                Require(highlight.position.x - (frameWidth * 0.5f) <= spriteCenter.x - (spriteWidth * 0.5f) &&
                        highlight.position.x + (frameWidth * 0.5f) >= spriteCenter.x + (spriteWidth * 0.5f) &&
                        highlight.position.y - (frameHeight * 0.5f) <= spriteCenter.y - (spriteHeight * 0.5f) &&
                        frameTop >= spriteCenter.y + (spriteHeight * 0.5f),
                    $"{controller.name} highlight does not enclose the full sprite.");
                minimumGap = Mathf.Min(minimumGap, hudBottomWorldY - frameTop);
                Require(snapshots[index].Matches(controller),
                    $"{controller.name} collider, MovePoint, UsePoint, or transform changed after repeated Setup.");
            }

            Require(minimumGap >= 0.2f, $"Highlight/HUD gap is only {minimumGap:0.000} world units.");
            Debug.Log($"Sprint5-12F closest Highlight/HUD gap: {minimumGap:0.000} world units.");
        }

        private static void ValidateVisualVisibility()
        {
            Transform stage = GameObject.Find("GameStage")?.transform;
            Require(stage != null, "GameStage is missing.");
            string[] hiddenPaths =
            {
                "PixelEnvironmentTest", "Entrance/SpawnPoint", "Queue/DecisionPoint",
                "Queue/NyoiceApproachPoint", "NyoiceLine/CrossingTarget", "Exit/ExitPoint", "Waypoints"
            };
            foreach (string path in hiddenPaths) RequireRendererState(stage, path, false);
            foreach (Renderer renderer in stage.Find("Queue").GetComponentsInChildren<Renderer>(true))
            {
                Require(!renderer.enabled, "A QueueSlot debug Renderer is visible.");
            }

            RequireRendererState(stage, "Entrance/EntranceMarker", true);
            RequireRendererState(stage, "Exit/ExitMarker", true);
            RequireRendererState(stage, "NyoiceLine/WallVisual", true);
            Renderer runtimeLineRenderer = stage.Find("NyoiceLine/Line")?.GetComponent<Renderer>();
            Require(runtimeLineRenderer != null && !runtimeLineRenderer.enabled,
                "Runtime line Renderer must remain hidden behind WallVisual.");
            RequireRendererState(stage, "Partitions", true);
            Require(stage.Find("Entrance/SpawnPoint") != null && stage.Find("Queue/DecisionPoint") != null &&
                    stage.Find("Queue/NyoiceApproachPoint") != null && stage.Find("NyoiceLine/CrossingTarget") != null &&
                    stage.Find("Exit/ExitPoint") != null,
                "A required runtime path Transform was removed.");
        }

        private static void RequireRendererState(Transform stage, string path, bool expectedEnabled)
        {
            Transform target = stage.Find(path);
            Require(target != null && target.gameObject.activeInHierarchy, $"Required object {path} is missing or inactive.");
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
            Require(renderers.Length > 0, $"{path} has no Renderer to validate.");
            foreach (Renderer renderer in renderers)
            {
                Require(renderer.enabled == expectedEnabled, $"{path} Renderer visibility is incorrect.");
            }
        }

        private static UrinalSnapshot[] CaptureUrinals()
        {
            UrinalController[] controllers = GameObject.Find("GameStage/Urinals")
                ?.GetComponentsInChildren<UrinalController>(true) ?? Array.Empty<UrinalController>();
            var result = new UrinalSnapshot[controllers.Length];
            for (int index = 0; index < controllers.Length; index++) result[index] = new UrinalSnapshot(controllers[index]);
            return result;
        }

        private static void ValidateScreenWidth(RectTransform rect, Vector2 resolution)
        {
            float scale = Mathf.Sqrt((resolution.x / 1920f) * (resolution.y / 1080f));
            Require(rect.sizeDelta.x <= (resolution.x / scale) - 48f,
                $"LowerInfo exceeds the screen at {resolution.x:0}x{resolution.y:0}.");
        }

        private static Transform RequireDirectChild(Transform parent, string name)
        {
            Transform match = null;
            int count = 0;
            for (int index = 0; index < parent.childCount; index++)
            {
                if (parent.GetChild(index).name != name) continue;
                match = parent.GetChild(index);
                count++;
            }
            Require(count == 1, $"{parent.name} must contain exactly one {name}.");
            return match;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private readonly struct UrinalSnapshot
        {
            private readonly Vector3 _position, _bodyPosition, _bodyScale, _colliderCenter, _colliderSize, _move, _use;
            public UrinalSnapshot(UrinalController controller)
            {
                Transform body = controller.transform.Find("Body");
                BoxCollider collider = body.GetComponent<BoxCollider>();
                _position = controller.transform.position;
                _bodyPosition = body.localPosition;
                _bodyScale = body.localScale;
                _colliderCenter = collider.center;
                _colliderSize = collider.size;
                _move = controller.MovePoint.position;
                _use = controller.UsePoint.position;
            }
            public bool Matches(UrinalController controller)
            {
                Transform body = controller.transform.Find("Body");
                BoxCollider collider = body.GetComponent<BoxCollider>();
                return Close(_position, controller.transform.position) && Close(_bodyPosition, body.localPosition) &&
                       Close(_bodyScale, body.localScale) && Close(_colliderCenter, collider.center) &&
                       Close(_colliderSize, collider.size) && Close(_move, controller.MovePoint.position) &&
                       Close(_use, controller.UsePoint.position);
            }
            private static bool Close(Vector3 a, Vector3 b) => Vector3.SqrMagnitude(a - b) < 0.000001f;
        }
    }
}
