using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Nyoice.Editor
{
    public sealed class NyoicePixelArtImportPostprocessor : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!NyoicePixelArtSetup.IsPixelPng(assetPath))
            {
                return;
            }

            NyoicePixelArtSetup.ConfigureImporter((TextureImporter)assetImporter);
        }
    }

    public static class NyoicePixelArtSetup
    {
        public const string PixelRoot = "Assets/_Project/Art/Pixel";
        public const string TestNpcPath = PixelRoot + "/Test/TestNPC.png";
        public const string TestUrinalPath = PixelRoot + "/Test/TestUrinal.png";
        public const string TestFloorPath = PixelRoot + "/Test/TestFloor.png";

        public const int EnvironmentOrder = 0;
        public const int UrinalOrder = 10;
        public const int NpcOrder = 20;
        public const int GaugeOrder = 30;
        public const int WorldUiOrder = 40;
        public const int NpcPixelVisualScale = 2;
        public const int UrinalPixelVisualScale = 2;
        public const int EnvironmentPixelVisualScale = 2;
        public const float UrinalPixelVisualYOffset = -0.40625f;

        private const float PixelsPerUnit = 32f;
        public static readonly Vector3 NpcLegacyVisualScale = new Vector3(0.12f, 0.45f, 0.3f);
        public static readonly Vector3 UrinalLegacyVisualScale = new Vector3(1f, 1.2f, 0.5f);

        private const string GameScenePath = "Assets/_Project/Scenes/GameScene.unity";
        private const string NpcPrefabPath = "Assets/_Project/Prefabs/NPC.prefab";
        private const string UsePixelVisualsKey = "Nyoice.PixelArt.UsePixelVisuals";

        private static readonly string[] PixelFolders =
        {
            "Characters", "Urinals", "Environment", "UI", "Effects", "Palette", "Test"
        };

        public static bool UsePixelVisuals => EditorPrefs.GetBool(UsePixelVisualsKey, false);

        [MenuItem("Nyoice/Pixel Art/Apply Import Settings")]
        public static void ApplyImportSettings()
        {
            EnsureFolders();
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { PixelRoot });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!IsPixelPng(path))
                {
                    continue;
                }

                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    continue;
                }

                ConfigureImporter(importer);
                importer.SaveAndReimport();
            }

            Debug.Log($"Applied pixel-art import settings to {guids.Length} texture(s).");
        }

        [MenuItem("Nyoice/Pixel Art/Use Legacy Visuals")]
        public static void UseLegacyVisuals()
        {
            SetVisualMode(false);
        }

        [MenuItem("Nyoice/Pixel Art/Use Pixel Visuals")]
        public static void UsePixelVisualsMenu()
        {
            SetVisualMode(true);
        }

        public static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder(PixelRoot))
            {
                EnsureAssetFolder("Assets/_Project/Art", "Pixel");
            }

            foreach (string folder in PixelFolders)
            {
                EnsureAssetFolder(PixelRoot, folder);
            }
        }

        public static bool IsPixelPng(string path)
        {
            return !string.IsNullOrEmpty(path) &&
                   path.StartsWith(PixelRoot + "/", StringComparison.Ordinal) &&
                   path.EndsWith(".png", StringComparison.OrdinalIgnoreCase);
        }

        public static void ConfigureImporter(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 32f;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
        }

        public static void EnsureNpcVisuals(GameObject npcRoot)
        {
            Transform visualRoot = GetOrCreateDirectChild(npcRoot.transform, "VisualRoot");
            ResetLocalTransform(visualRoot);
            Transform legacy = GetOrCreateDirectChild(visualRoot, "LegacyVisual");
            Transform pixel = GetOrCreateDirectChild(visualRoot, "PixelVisual");
            Renderer sourceRenderer = FindRuntimeRenderer(npcRoot.transform, visualRoot, "Visual");
            CopyLegacyMesh(sourceRenderer, legacy);
            RemoveRuntimeRenderer(sourceRenderer);
            legacy.localScale = NpcLegacyVisualScale;

            SpriteRenderer pixelRenderer = GetOrAddSpriteRenderer(pixel.gameObject);
            pixelRenderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TestNpcPath);
            pixelRenderer.sortingOrder = NpcOrder;
            pixel.localPosition = SnapToPixelGrid(new Vector3(0f, 0.45f, -0.3f));
            pixel.localRotation = Quaternion.identity;
            pixel.localScale = Vector3.one * NpcPixelVisualScale;
            SetExclusiveVisuals(visualRoot, UsePixelVisuals);
        }

        public static Renderer EnsureUrinalVisuals(Transform urinal, Transform body)
        {
            Transform visualRoot = GetOrCreateDirectChild(urinal, "VisualRoot");
            ResetLocalTransform(visualRoot);
            Transform legacy = GetOrCreateDirectChild(visualRoot, "LegacyVisual");
            Transform pixel = GetOrCreateDirectChild(visualRoot, "PixelVisual");
            Renderer sourceRenderer = body.GetComponent<Renderer>();
            CopyLegacyMesh(sourceRenderer, legacy);
            RemoveRuntimeRenderer(sourceRenderer);
            legacy.localScale = UrinalLegacyVisualScale;

            Renderer legacyRenderer = legacy.GetComponent<Renderer>();
            if (legacyRenderer != null)
            {
                legacyRenderer.sortingOrder = UrinalOrder;
            }

            SpriteRenderer pixelRenderer = GetOrAddSpriteRenderer(pixel.gameObject);
            pixelRenderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TestUrinalPath);
            pixelRenderer.sortingOrder = UrinalOrder;
            pixel.localPosition = SnapToPixelGrid(new Vector3(0f, UrinalPixelVisualYOffset, -0.3f));
            pixel.localRotation = Quaternion.identity;
            pixel.localScale = Vector3.one * UrinalPixelVisualScale;
            SetExclusiveVisuals(visualRoot, UsePixelVisuals);
            return UsePixelVisuals ? pixelRenderer : legacyRenderer;
        }

        public static void EnsureEnvironmentVisual(Transform gameStage)
        {
            Transform testVisual = GetOrCreateDirectChild(gameStage, "PixelEnvironmentTest");
            SpriteRenderer renderer = GetOrAddSpriteRenderer(testVisual.gameObject);
            renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TestFloorPath);
            renderer.sortingOrder = EnvironmentOrder;
            testVisual.localPosition = SnapToPixelGrid(new Vector3(0f, -4.25f, 0.5f));
            testVisual.localRotation = Quaternion.identity;
            testVisual.localScale = Vector3.one * EnvironmentPixelVisualScale;
            testVisual.gameObject.SetActive(true);
            renderer.enabled = false;
        }

        public static void SetExclusiveVisuals(Transform visualRoot, bool usePixel)
        {
            Transform legacy = visualRoot.Find("LegacyVisual");
            Transform pixel = visualRoot.Find("PixelVisual");
            if (legacy != null)
            {
                legacy.gameObject.SetActive(!usePixel);
            }

            if (pixel != null)
            {
                pixel.gameObject.SetActive(usePixel);
            }
        }

        private static void SetVisualMode(bool usePixel)
        {
            EditorPrefs.SetBool(UsePixelVisualsKey, usePixel);
            NyoiceGameStageSetup.SetupGameStage();
            Debug.Log(usePixel ? "Pixel visuals enabled." : "Legacy visuals enabled.");
        }

        private static void EnsureAssetFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static Transform GetOrCreateDirectChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child == null)
            {
                child = new GameObject(name).transform;
                child.SetParent(parent, false);
                ResetLocalTransform(child);
            }
            return child;
        }

        private static void ResetLocalTransform(Transform target)
        {
            target.localPosition = Vector3.zero;
            target.localRotation = Quaternion.identity;
            target.localScale = Vector3.one;
        }

        private static Vector3 SnapToPixelGrid(Vector3 position)
        {
            return new Vector3(
                Mathf.Round(position.x * PixelsPerUnit) / PixelsPerUnit,
                Mathf.Round(position.y * PixelsPerUnit) / PixelsPerUnit,
                Mathf.Round(position.z * PixelsPerUnit) / PixelsPerUnit);
        }

        private static Renderer FindRuntimeRenderer(Transform root, Transform visualRoot, string preferredName)
        {
            Transform preferred = root.Find(preferredName);
            if (preferred != null && !preferred.IsChildOf(visualRoot))
            {
                Renderer renderer = preferred.GetComponent<Renderer>();
                if (renderer != null)
                {
                    return renderer;
                }
            }

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.transform.IsChildOf(visualRoot))
                {
                    return renderer;
                }
            }

            return null;
        }

        private static void CopyLegacyMesh(Renderer source, Transform legacy)
        {
            if (source == null)
            {
                return;
            }

            MeshFilter sourceFilter = source.GetComponent<MeshFilter>();
            MeshFilter targetFilter = legacy.GetComponent<MeshFilter>();
            if (targetFilter == null)
            {
                targetFilter = legacy.gameObject.AddComponent<MeshFilter>();
            }

            MeshRenderer targetRenderer = legacy.GetComponent<MeshRenderer>();
            if (targetRenderer == null)
            {
                targetRenderer = legacy.gameObject.AddComponent<MeshRenderer>();
            }

            targetFilter.sharedMesh = sourceFilter != null ? sourceFilter.sharedMesh : null;
            targetRenderer.sharedMaterials = source.sharedMaterials;
            legacy.localPosition = source.transform.localPosition;
            legacy.localRotation = source.transform.localRotation;
            legacy.localScale = source.transform.localScale;
        }

        private static void RemoveRuntimeRenderer(Renderer renderer)
        {
            if (renderer != null)
            {
                UnityEngine.Object.DestroyImmediate(renderer);
            }
        }

        private static SpriteRenderer GetOrAddSpriteRenderer(GameObject target)
        {
            SpriteRenderer renderer = target.GetComponent<SpriteRenderer>();
            return renderer != null ? renderer : target.AddComponent<SpriteRenderer>();
        }
    }
}
