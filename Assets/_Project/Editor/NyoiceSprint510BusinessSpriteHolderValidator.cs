using System;
using Nyoice.NPC;
using UnityEditor;
using UnityEngine;

namespace Nyoice.Editor
{
    public static class NyoiceSprint510BusinessSpriteHolderValidator
    {
        private const string HolderPath =
            "Assets/_Project/Art/Pixel/NPC/Business/NPCBusinessSpriteHolder.asset";
        private const string NpcPrefabPath = "Assets/_Project/Prefabs/NPC.prefab";

        [MenuItem("Nyoice/Validate Sprint5-10 Business Sprite Holder")]
        public static void ValidateBusinessSpriteHolder()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    "Assets/_Project/Scenes/GameScene.unity") == null)
            {
                NyoiceProjectSetup.SetupSprintOne();
            }

            NyoiceGameStageSetup.SetupGameStage();
            NyoiceGameStageSetup.SetupGameStage();

            NPCBusinessSpriteHolder holder =
                AssetDatabase.LoadAssetAtPath<NPCBusinessSpriteHolder>(HolderPath);
            Require(holder != null, "Business SpriteHolder asset is missing.");
            var serializedHolder = new SerializedObject(holder);
            Require(serializedHolder.FindProperty("npcBusinessFront01") != null &&
                    serializedHolder.FindProperty("npcBusinessFront02") != null &&
                    serializedHolder.FindProperty("npcBusinessLeft01") != null &&
                    serializedHolder.FindProperty("npcBusinessLeft02") != null &&
                    serializedHolder.FindProperty("npcBusinessBackPee") != null,
                "Business SpriteHolder does not expose all five formal sprite slots.");

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NpcPrefabPath);
            NPCController prefabController = prefab != null ? prefab.GetComponent<NPCController>() : null;
            Require(prefabController != null, "NPC prefab has no NPCController.");
            Require(prefabController.SpriteHolder == holder,
                "Setup Game Stage did not restore the NPC SpriteHolder reference.");

            var fixture = new GameObject("Sprint5-10 Null Sprite Fixture");
            try
            {
                NPCController controller = fixture.AddComponent<NPCController>();
                controller.ConfigureSpriteHolder(null, null);
                controller.SetIdle();
                controller.SetWalkFrame(false);
                controller.SetWalkFrame(true);
                controller.SetPee();
                controller.SetExit();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(fixture);
            }

            NyoiceSprint59PixelArtFoundationValidator.ValidatePixelArtFoundation();
            Debug.Log("Sprint5-10 Business SpriteHolder validation passed.");
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
