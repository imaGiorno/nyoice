using System;
using Nyoice.NPC;
using Nyoice.Toilet;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Nyoice.Editor
{
    public static class NyoiceSprint59PixelArtFoundationValidator
    {
        private const string NpcPrefabPath = "Assets/_Project/Prefabs/NPC.prefab";
        private const string GameScenePath = "Assets/_Project/Scenes/GameScene.unity";
        private static readonly string[] RequiredFolders =
        {
            "Characters", "Urinals", "Environment", "UI", "Effects", "Palette", "Test"
        };

        [MenuItem("Nyoice/Validate Sprint5-9 Pixel Art Foundation")]
        public static void ValidatePixelArtFoundation()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GameScenePath) == null)
            {
                NyoiceProjectSetup.SetupSprintOne();
            }

            NyoiceGameStageSetup.SetupGameStage();
            NyoiceGameStageSetup.SetupGameStage();
            ValidateFoldersAndImporters();
            ValidateNpcVisuals();
            ValidateUrinalVisuals();
            ValidateSortingAndCamera();
            NyoiceSprint52ExitFlowValidator.ValidateExitFlow();
            NyoiceSprint53ADiscomfortValidator.ValidateDiscomfortFlow();
            NyoiceSprint54ScoreValidator.ValidateScoreFlow();
            NyoiceSprint54AInitialFlowValidator.ValidateInitialFlow();
            NyoiceSprint56ARandomTimingGaugeValidator.ValidateRandomTimingGauge();
            Debug.Log("Sprint5-9 Pixel Art Foundation validation passed.");
        }

        private static void ValidateFoldersAndImporters()
        {
            Require(AssetDatabase.IsValidFolder(NyoicePixelArtSetup.PixelRoot), "Pixel root folder is missing.");
            foreach (string folder in RequiredFolders)
            {
                Require(AssetDatabase.IsValidFolder(NyoicePixelArtSetup.PixelRoot + "/" + folder),
                    $"Pixel folder {folder} is missing.");
            }

            ValidateTexture(NyoicePixelArtSetup.TestNpcPath);
            ValidateTexture(NyoicePixelArtSetup.TestUrinalPath);
            ValidateTexture(NyoicePixelArtSetup.TestFloorPath);
        }

        private static void ValidateTexture(string path)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            Require(sprite != null, $"Test sprite is missing or not imported as a Sprite: {path}");
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Require(importer != null && importer.textureType == TextureImporterType.Sprite,
                $"{path} is not a Sprite texture.");
            Require(importer.spriteImportMode == SpriteImportMode.Single, $"{path} is not Single mode.");
            Require(Mathf.Approximately(importer.spritePixelsPerUnit, 32f), $"{path} is not 32 PPU.");
            Require(importer.filterMode == FilterMode.Point, $"{path} is not Point filtered.");
            Require(importer.textureCompression == TextureImporterCompression.Uncompressed,
                $"{path} is compressed.");
            Require(!importer.mipmapEnabled, $"{path} has mip maps enabled.");
            Require(importer.wrapMode == TextureWrapMode.Clamp, $"{path} does not use Clamp wrapping.");
            Require(importer.alphaIsTransparency, $"{path} does not use alpha transparency.");
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            Require(settings.spriteMeshType == SpriteMeshType.FullRect, $"{path} does not use Full Rect mesh.");
        }

        private static void ValidateNpcVisuals()
        {
            GameObject prefab = PrefabUtility.LoadPrefabContents(NpcPrefabPath);
            try
            {
                Transform visualRoot = RequireSingleDirectChild(prefab.transform, "VisualRoot");
                Transform legacy = RequireSingleDirectChild(visualRoot, "LegacyVisual");
                Transform pixel = RequireSingleDirectChild(visualRoot, "PixelVisual");
                SpriteRenderer renderer = pixel.GetComponent<SpriteRenderer>();
                Require(renderer != null && AssetDatabase.GetAssetPath(renderer.sprite) == NyoicePixelArtSetup.TestNpcPath,
                    "NPC PixelVisual does not reference TestNPC.");
                ValidateIntegerScale(pixel, NyoicePixelArtSetup.NpcPixelVisualScale, "NPC PixelVisual");
                Require(Approximately(legacy.localScale, NyoicePixelArtSetup.NpcLegacyVisualScale),
                    "NPC LegacyVisual scale changed during pixel setup.");
                ValidatePixelSnappedPosition(pixel, "NPC PixelVisual");
                ValidateExclusiveToggle(visualRoot, legacy, pixel);
                Require(prefab.GetComponent<NPCController>() != null && prefab.GetComponent<NPCMovement>() != null,
                    "NPC runtime components are missing from the prefab root.");
                foreach (Collider collider in prefab.GetComponentsInChildren<Collider>(true))
                {
                    Require(!collider.transform.IsChildOf(visualRoot),
                        $"NPC collider {collider.name} was moved under VisualRoot.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefab);
            }
        }

        private static void ValidateUrinalVisuals()
        {
            Transform urinalRoot = GameObject.Find("GameStage/Urinals")?.transform;
            Require(urinalRoot != null, "Configured GameScene has no Urinals root.");
            for (int index = 0; index < 8; index++)
            {
                Transform urinal = urinalRoot.Find($"Urinal{index + 1:00}");
                Require(urinal != null, $"Urinal{index + 1:00} is missing.");
                Transform visualRoot = RequireSingleDirectChild(urinal, "VisualRoot");
                Transform legacy = RequireSingleDirectChild(visualRoot, "LegacyVisual");
                Transform pixel = RequireSingleDirectChild(visualRoot, "PixelVisual");
                SpriteRenderer renderer = pixel.GetComponent<SpriteRenderer>();
                Require(renderer != null && AssetDatabase.GetAssetPath(renderer.sprite) == NyoicePixelArtSetup.TestUrinalPath,
                    $"Urinal{index + 1:00} PixelVisual does not reference TestUrinal.");
                ValidateIntegerScale(pixel, NyoicePixelArtSetup.UrinalPixelVisualScale,
                    $"Urinal{index + 1:00} PixelVisual");
                Require(Approximately(legacy.localScale, NyoicePixelArtSetup.UrinalLegacyVisualScale),
                    $"Urinal{index + 1:00} LegacyVisual scale changed during pixel setup.");
                ValidatePixelSnappedPosition(pixel, $"Urinal{index + 1:00} PixelVisual");

                UrinalController controller = urinal.GetComponent<UrinalController>();
                Require(controller != null && controller.MovePoint != null && controller.UsePoint != null,
                    $"Urinal{index + 1:00} runtime references are incomplete.");
                Require(urinal.Find("Body")?.GetComponent<Collider>() != null,
                    $"Urinal{index + 1:00} collider was removed or moved.");
                Require(urinal.Find("Highlight") != null, $"Urinal{index + 1:00} highlight is missing.");
                ValidateUrinalHighlight(urinal, visualRoot, legacy, pixel, renderer, controller);
                ValidateExclusiveToggle(visualRoot, legacy, pixel);
            }
        }

        private static void ValidateUrinalHighlight(
            Transform urinal,
            Transform visualRoot,
            Transform legacy,
            Transform pixel,
            SpriteRenderer pixelRenderer,
            UrinalController controller)
        {
            Transform body = urinal.Find("Body");
            Transform highlight = urinal.Find("Highlight");
            BoxCollider collider = body.GetComponent<BoxCollider>();
            Vector3 colliderSize = collider.size;
            Vector3 colliderCenter = collider.center;
            Vector3 colliderPosition = body.localPosition;
            Vector3 colliderScale = body.localScale;
            bool originalPixelState = pixel.gameObject.activeSelf;

            NyoicePixelArtSetup.SetExclusiveVisuals(visualRoot, true);
            NyoiceGameStageSetup.ConfigureUrinalHighlightLayout(urinal, body, highlight, true);
            float spriteWidth = pixelRenderer.sprite.bounds.size.x * Mathf.Abs(pixel.localScale.x);
            float spriteHeight = pixelRenderer.sprite.bounds.size.y * Mathf.Abs(pixel.localScale.y);
            float frameWidth = highlight.Find("Top").localScale.x;
            float frameHeight = highlight.Find("Left").localScale.y;
            Require(frameWidth >= spriteWidth && frameHeight >= spriteHeight,
                $"{urinal.name} pixel highlight does not enclose the full sprite.");
            Require(frameWidth >= spriteWidth * 1.05f && frameWidth <= spriteWidth * 1.15f &&
                    frameHeight >= spriteHeight * 1.05f && frameHeight <= spriteHeight * 1.15f,
                $"{urinal.name} pixel highlight does not provide approximately 10 percent padding.");

            NyoiceGameStageSetup.ConfigureUrinalHighlightLayout(urinal, body, highlight, true);
            Require(Mathf.Approximately(frameWidth, highlight.Find("Top").localScale.x) &&
                    Mathf.Approximately(frameHeight, highlight.Find("Left").localScale.y),
                $"{urinal.name} pixel highlight size accumulated across repeated setup.");

            NyoicePixelArtSetup.SetExclusiveVisuals(visualRoot, false);
            NyoiceGameStageSetup.ConfigureUrinalHighlightLayout(urinal, body, highlight, false);
            controller.SetSelected(true);
            Require(highlight.gameObject.activeSelf && legacy.gameObject.activeSelf,
                $"{urinal.name} highlight no longer works with LegacyVisual.");
            controller.SetSelected(false);

            Require(Approximately(collider.size, colliderSize) && Approximately(collider.center, colliderCenter) &&
                    Approximately(body.localPosition, colliderPosition) && Approximately(body.localScale, colliderScale),
                $"{urinal.name} collider or click range changed while resizing the highlight.");

            NyoicePixelArtSetup.SetExclusiveVisuals(visualRoot, originalPixelState);
            NyoiceGameStageSetup.ConfigureUrinalHighlightLayout(
                urinal,
                body,
                highlight,
                originalPixelState);
        }

        private static void ValidateSortingAndCamera()
        {
            Require(NyoicePixelArtSetup.EnvironmentOrder < NyoicePixelArtSetup.UrinalOrder &&
                    NyoicePixelArtSetup.UrinalOrder < NyoicePixelArtSetup.NpcOrder &&
                    NyoicePixelArtSetup.NpcOrder < NyoicePixelArtSetup.GaugeOrder &&
                    NyoicePixelArtSetup.GaugeOrder < NyoicePixelArtSetup.WorldUiOrder,
                "Pixel-art sorting constants are not ordered correctly.");
            Require(Camera.main != null && Camera.main.orthographic, "Main Camera is not orthographic.");
            Transform environment = GameObject.Find("GameStage")?.transform.Find("PixelEnvironmentTest");
            Require(environment != null && environment.GetComponent<SpriteRenderer>()?.sortingOrder ==
                    NyoicePixelArtSetup.EnvironmentOrder,
                "Environment test sprite is missing or incorrectly sorted.");

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NpcPrefabPath);
            Transform pixel = prefab?.transform.Find("VisualRoot/PixelVisual");
            Require(pixel != null, "NPC PixelVisual is missing.");
            ValidateIntegerScale(pixel, NyoicePixelArtSetup.NpcPixelVisualScale, "NPC PixelVisual");
            Canvas gauge = prefab?.transform.Find("UrinationGauge")?.GetComponent<Canvas>();
            Require(gauge != null && gauge.sortingOrder == NyoicePixelArtSetup.GaugeOrder,
                "NPC gauge is not sorted in front of the NPC sprite.");
        }

        private static void ValidateExclusiveToggle(Transform root, Transform legacy, Transform pixel)
        {
            bool original = pixel.gameObject.activeSelf;
            NyoicePixelArtSetup.SetExclusiveVisuals(root, false);
            Require(legacy.gameObject.activeSelf && !pixel.gameObject.activeSelf,
                $"{root.parent.name} cannot switch to legacy-only visuals.");
            NyoicePixelArtSetup.SetExclusiveVisuals(root, true);
            Require(!legacy.gameObject.activeSelf && pixel.gameObject.activeSelf,
                $"{root.parent.name} cannot switch to pixel-only visuals.");
            NyoicePixelArtSetup.SetExclusiveVisuals(root, original);
        }

        private static void ValidateIntegerScale(Transform target, int expected, string label)
        {
            Vector3 scale = target.localScale;
            Require(Mathf.Approximately(scale.x, Mathf.Round(scale.x)) &&
                    Mathf.Approximately(scale.y, Mathf.Round(scale.y)) &&
                    Mathf.Approximately(scale.z, Mathf.Round(scale.z)),
                $"{label} scale must use integer values.");
            Require(Approximately(scale, Vector3.one * expected),
                $"{label} scale must be an absolute {expected}x and must not accumulate across setup runs.");
        }

        private static void ValidatePixelSnappedPosition(Transform target, string label)
        {
            Vector3 scaled = target.localPosition * 32f;
            Require(Mathf.Approximately(scaled.x, Mathf.Round(scaled.x)) &&
                    Mathf.Approximately(scaled.y, Mathf.Round(scaled.y)) &&
                    Mathf.Approximately(scaled.z, Mathf.Round(scaled.z)),
                $"{label} local position is not aligned to the 32 PPU pixel grid.");
        }

        private static bool Approximately(Vector3 first, Vector3 second)
        {
            return Mathf.Approximately(first.x, second.x) &&
                   Mathf.Approximately(first.y, second.y) &&
                   Mathf.Approximately(first.z, second.z);
        }

        private static Transform RequireSingleDirectChild(Transform parent, string name)
        {
            Transform match = null;
            int count = 0;
            for (int index = 0; index < parent.childCount; index++)
            {
                Transform child = parent.GetChild(index);
                if (child.name == name)
                {
                    match = child;
                    count++;
                }
            }

            Require(count == 1, $"{parent.name} must contain exactly one direct {name} child.");
            return match;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
