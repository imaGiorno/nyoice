using System;
using Nyoice.Toilet;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Nyoice.Editor
{
    public static class NyoiceSprint512DHudWidthOverlapValidator
    {
        private const string GameScenePath = "Assets/_Project/Scenes/GameScene.unity";
        private const float PreviousHudWidth = 1380f;
        private const float MinimumHudWidth = 1180f;
        private const float MaximumHudWidth = 1240f;

        [MenuItem("Nyoice/Validate Sprint5-12D HUD Width & Overlap Fix")]
        public static void ValidateHudWidthOverlapFix()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GameScenePath) == null)
            {
                NyoiceProjectSetup.SetupSprintOne();
            }

            NyoiceGameStageSetup.SetupGameStage();
            UrinalSnapshot[] snapshots = CaptureUrinalSnapshots();
            NyoiceGameStageSetup.SetupGameStage();

            Transform canvas = GameObject.Find("UI/DiscomfortCanvas")?.transform;
            Require(canvas != null, "HUD canvas is missing.");
            ValidateHud(canvas);
            ValidateLowerInfo(canvas);
            ValidateUrinals(canvas, snapshots);
            NyoiceSprint512CLowerInfoVerticalPathValidator.ValidateLowerInfoVerticalPath();
            Debug.Log("Sprint5-12D HUD Width & Overlap Fix validation passed.");
        }

        private static void ValidateHud(Transform canvas)
        {
            RectTransform container = RequireSingleDescendant(canvas, "HUDContainer").GetComponent<RectTransform>();
            Require(container.anchorMin == container.anchorMax, "HUDContainer must not stretch across the Canvas.");
            Require(container.sizeDelta.x < PreviousHudWidth &&
                    container.sizeDelta.x >= MinimumHudWidth &&
                    container.sizeDelta.x <= MaximumHudWidth,
                "HUDContainer width is outside the Sprint5-12D range.");

            RectTransform discomfort = RequireDirectChild(container, "DiscomfortPanel").GetComponent<RectTransform>();
            RectTransform stats = RequireDirectChild(container, "StatsPanel").GetComponent<RectTransform>();
            RectTransform combo = RequireDirectChild(container, "ComboPanel").GetComponent<RectTransform>();
            Require(Mathf.Approximately(discomfort.sizeDelta.x, 580f) &&
                    Mathf.Approximately(stats.sizeDelta.x, 420f) &&
                    Mathf.Approximately(combo.sizeDelta.x, 172f),
                "HUD panel widths do not match the Sprint5-12D layout.");
            Require(discomfort.sizeDelta.x > stats.sizeDelta.x && stats.sizeDelta.x > combo.sizeDelta.x,
                "HUD panel size priority changed.");

            ValidateScreenWidth(container, new Vector2(1920f, 1080f));
            ValidateScreenWidth(container, new Vector2(2340f, 1080f));
            ValidateScreenWidth(container, new Vector2(1280f, 720f));
        }

        private static void ValidateLowerInfo(Transform canvas)
        {
            RectTransform lower = RequireSingleDescendant(canvas, "LowerInfoRoot").GetComponent<RectTransform>();
            Require(Mathf.Approximately(lower.sizeDelta.y, 140f), "LowerInfoRoot height must remain 140 pixels.");

            Text rule = RequireDirectChild(RequireDirectChild(lower, "RulePanel"), "BodyText").GetComponent<Text>();
            Text guide = RequireDirectChild(RequireDirectChild(lower, "ComboGuidePanel"), "BodyText").GetComponent<Text>();
            Text stage = RequireDirectChild(RequireDirectChild(lower, "StageInfoPanel"), "BodyText").GetComponent<Text>();
            Require(rule.fontSize > 18 && guide.fontSize > 17 && stage.fontSize > 16,
                "LowerInfo text was not enlarged from Sprint5-12C.");
            Require(guide.alignment == TextAnchor.UpperLeft, "ComboGuide must be aligned to the upper-left.");
            Canvas.ForceUpdateCanvases();
            Require(guide.preferredWidth <= guide.rectTransform.rect.width + 0.1f &&
                    guide.preferredHeight <= guide.rectTransform.rect.height + 0.1f,
                "ComboGuide text does not fit inside its panel.");
            foreach (Graphic graphic in lower.GetComponentsInChildren<Graphic>(true))
            {
                Require(!graphic.raycastTarget, $"LowerInfo graphic {graphic.name} blocks raycasts.");
            }
        }

        private static void ValidateUrinals(Transform canvas, UrinalSnapshot[] snapshots)
        {
            Transform urinalRoot = GameObject.Find("GameStage/Urinals")?.transform;
            UrinalController[] urinals = urinalRoot != null
                ? urinalRoot.GetComponentsInChildren<UrinalController>(true)
                : Array.Empty<UrinalController>();
            Require(urinals.Length == 8 && snapshots.Length == 8, "The stage must retain eight urinals.");

            Camera camera = Camera.main;
            Require(camera != null && camera.orthographic, "Main Camera must remain orthographic.");
            RectTransform hudRoot = RequireDirectChild(canvas, "HUDRoot").GetComponent<RectTransform>();
            float hudBottomPixels = -hudRoot.anchoredPosition.y + hudRoot.sizeDelta.y;
            float hudBottomWorldY = camera.ViewportToWorldPoint(
                new Vector3(0.5f, 1f - (hudBottomPixels / 1080f), 0f)).y;
            float closestGap = float.MaxValue;

            for (int index = 0; index < urinals.Length; index++)
            {
                Transform urinal = urinals[index].transform;
                Transform pixel = urinal.Find("VisualRoot/PixelVisual");
                SpriteRenderer renderer = pixel?.GetComponent<SpriteRenderer>();
                Transform highlight = urinal.Find("Highlight");
                Require(renderer != null && renderer.sprite != null && highlight != null,
                    $"{urinal.name} visual or highlight is missing.");

                float spriteWidth = renderer.sprite.bounds.size.x * Mathf.Abs(pixel.localScale.x);
                float spriteHeight = renderer.sprite.bounds.size.y * Mathf.Abs(pixel.localScale.y);
                float frameWidth = highlight.Find("Top").localScale.x;
                float frameHeight = highlight.Find("Left").localScale.y;
                Require(frameWidth >= spriteWidth && frameHeight >= spriteHeight,
                    $"{urinal.name} highlight does not enclose the full sprite.");

                float highlightTop = highlight.position.y + (frameHeight * Mathf.Abs(highlight.lossyScale.y) * 0.5f);
                closestGap = Mathf.Min(closestGap, hudBottomWorldY - highlightTop);
                Require(highlightTop < hudBottomWorldY,
                    $"{urinal.name} highlight overlaps the HUD lower edge " +
                    $"(highlight={highlightTop:0.000}, HUD={hudBottomWorldY:0.000}).");
                Require(snapshots[index].Matches(urinals[index]),
                    $"{urinal.name} collider, MovePoint, UsePoint, or transform accumulated during Setup.");
            }

            Require(closestGap > 0f, "Highlight and HUD require a positive separation.");
            Debug.Log($"Sprint5-12D closest Highlight/HUD gap: {closestGap:0.000} world units.");
        }

        private static UrinalSnapshot[] CaptureUrinalSnapshots()
        {
            UrinalController[] urinals = GameObject.Find("GameStage/Urinals")
                ?.GetComponentsInChildren<UrinalController>(true) ?? Array.Empty<UrinalController>();
            var snapshots = new UrinalSnapshot[urinals.Length];
            for (int index = 0; index < urinals.Length; index++)
            {
                snapshots[index] = new UrinalSnapshot(urinals[index]);
            }

            return snapshots;
        }

        private static void ValidateScreenWidth(RectTransform rect, Vector2 resolution)
        {
            float scale = Mathf.Sqrt((resolution.x / 1920f) * (resolution.y / 1080f));
            float logicalWidth = resolution.x / scale;
            Require(rect.sizeDelta.x <= logicalWidth - 48f,
                $"HUD exceeds the screen at {resolution.x:0}x{resolution.y:0}.");
        }

        private static Transform RequireSingleDescendant(Transform root, string objectName)
        {
            Transform match = null;
            int count = 0;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name != objectName) continue;
                match = child;
                count++;
            }

            Require(count == 1, $"Hierarchy must contain exactly one {objectName}.");
            return match;
        }

        private static Transform RequireDirectChild(Transform parent, string objectName)
        {
            Transform match = null;
            int count = 0;
            for (int index = 0; index < parent.childCount; index++)
            {
                Transform child = parent.GetChild(index);
                if (child.name != objectName) continue;
                match = child;
                count++;
            }

            Require(count == 1, $"{parent.name} must contain exactly one {objectName}.");
            return match;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private readonly struct UrinalSnapshot
        {
            private readonly Vector3 _position;
            private readonly Vector3 _bodyPosition;
            private readonly Vector3 _bodyScale;
            private readonly Vector3 _colliderCenter;
            private readonly Vector3 _colliderSize;
            private readonly Vector3 _movePosition;
            private readonly Vector3 _usePosition;

            public UrinalSnapshot(UrinalController controller)
            {
                Transform body = controller.transform.Find("Body");
                BoxCollider collider = body.GetComponent<BoxCollider>();
                _position = controller.transform.position;
                _bodyPosition = body.localPosition;
                _bodyScale = body.localScale;
                _colliderCenter = collider.center;
                _colliderSize = collider.size;
                _movePosition = controller.MovePoint.position;
                _usePosition = controller.UsePoint.position;
            }

            public bool Matches(UrinalController controller)
            {
                Transform body = controller.transform.Find("Body");
                BoxCollider collider = body.GetComponent<BoxCollider>();
                return Approximately(_position, controller.transform.position) &&
                       Approximately(_bodyPosition, body.localPosition) &&
                       Approximately(_bodyScale, body.localScale) &&
                       Approximately(_colliderCenter, collider.center) &&
                       Approximately(_colliderSize, collider.size) &&
                       Approximately(_movePosition, controller.MovePoint.position) &&
                       Approximately(_usePosition, controller.UsePoint.position);
            }

            private static bool Approximately(Vector3 first, Vector3 second)
            {
                return Vector3.SqrMagnitude(first - second) < 0.000001f;
            }
        }
    }
}
