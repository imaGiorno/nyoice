using System;
using System.Reflection;
using Nyoice.NPC;
using Nyoice.Toilet;
using UnityEditor;
using UnityEngine;

namespace Nyoice.Editor
{
    public static class NyoiceSprint510CSpriteRuntimeConnectionValidator
    {
        private const string GameScenePath = "Assets/_Project/Scenes/GameScene.unity";
        private static readonly MethodInfo SetNpcStateMethod = typeof(NPCController).GetMethod(
            "SetState",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo AdvanceWalkSpriteTimeMethod = typeof(NPCController).GetMethod(
            "AdvanceWalkSpriteTime",
            BindingFlags.Instance | BindingFlags.NonPublic);

        [MenuItem("Nyoice/Validate Sprint5-10C Sprite Runtime Connection")]
        public static void ValidateSpriteRuntimeConnection()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GameScenePath) == null)
            {
                NyoiceProjectSetup.SetupSprintOne();
            }

            NyoiceGameStageSetup.SetupGameStage();
            NyoiceGameStageSetup.SetupGameStage();
            ValidateNpcConnections();
            ValidateUrinalConnections();
            NyoiceSprint510BUrinalSpriteIntegrationValidator.ValidateUrinalSpriteIntegration();
            Debug.Log("Sprint5-10C Sprite Runtime Connection validation passed.");
        }

        private static void ValidateNpcConnections()
        {
            Require(SetNpcStateMethod != null && AdvanceWalkSpriteTimeMethod != null,
                "NPC sprite presentation hooks are missing.");

            var fixture = new GameObject("Sprint5-10C NPC Fixture");
            var visual = new GameObject("PixelVisual");
            visual.transform.SetParent(fixture.transform, false);
            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            NPCBusinessSpriteHolder holder = ScriptableObject.CreateInstance<NPCBusinessSpriteHolder>();
            Sprite front01 = CreateSprite(Color.white);
            Sprite front02 = CreateSprite(Color.gray);
            Sprite left01 = CreateSprite(Color.red);
            Sprite left02 = CreateSprite(Color.green);
            Sprite backPee = CreateSprite(Color.blue);

            try
            {
                SetSpriteFields(holder, front01, front02, left01, left02, backPee);
                NPCController controller = fixture.AddComponent<NPCController>();
                controller.ConfigureSpriteHolder(holder, renderer);
                Require(renderer.sprite == front01, "Spawn did not use the idle sprite.");

                renderer.sprite = left01;
                controller.Initialize(null);
                Require(renderer.sprite == front01, "Queue did not use the idle sprite.");

                SetNpcStateMethod.Invoke(controller, new object[] { NPCState.ApproachingLine });
                Require(renderer.sprite == left01, "Move did not start with left01.");
                AdvanceWalkSpriteTimeMethod.Invoke(
                    controller,
                    new object[] { NPCController.WalkFrameIntervalSeconds });
                Require(renderer.sprite == left02, "Walk did not alternate to left02 after 0.2 seconds.");
                AdvanceWalkSpriteTimeMethod.Invoke(
                    controller,
                    new object[] { NPCController.WalkFrameIntervalSeconds });
                Require(renderer.sprite == left01, "Walk did not alternate back to left01.");

                SetNpcStateMethod.Invoke(controller, new object[] { NPCState.UsingUrinal });
                Require(renderer.sprite == backPee, "Pee did not use the back sprite.");
                SetNpcStateMethod.Invoke(controller, new object[] { NPCState.Leaving });
                Require(renderer.sprite == front01, "Exit did not use the front sprite.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(fixture);
                UnityEngine.Object.DestroyImmediate(holder);
                UnityEngine.Object.DestroyImmediate(front01.texture);
                UnityEngine.Object.DestroyImmediate(front02.texture);
                UnityEngine.Object.DestroyImmediate(left01.texture);
                UnityEngine.Object.DestroyImmediate(left02.texture);
                UnityEngine.Object.DestroyImmediate(backPee.texture);
                UnityEngine.Object.DestroyImmediate(front01);
                UnityEngine.Object.DestroyImmediate(front02);
                UnityEngine.Object.DestroyImmediate(left01);
                UnityEngine.Object.DestroyImmediate(left02);
                UnityEngine.Object.DestroyImmediate(backPee);
            }
        }

        private static void ValidateUrinalConnections()
        {
            var fixture = new GameObject("Sprint5-10C Urinal Fixture");
            var visual = new GameObject("PixelVisual");
            visual.transform.SetParent(fixture.transform, false);
            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            UrinalSpriteHolder holder = ScriptableObject.CreateInstance<UrinalSpriteHolder>();
            Sprite normal = CreateSprite(Color.white);
            Sprite selected = CreateSprite(Color.yellow);

            try
            {
                var serializedHolder = new SerializedObject(holder);
                serializedHolder.FindProperty("urinalNormal").objectReferenceValue = normal;
                serializedHolder.FindProperty("urinalSelected").objectReferenceValue = selected;
                serializedHolder.ApplyModifiedPropertiesWithoutUndo();

                UrinalController controller = fixture.AddComponent<UrinalController>();
                controller.ConfigureSpriteHolder(holder, renderer);
                Require(renderer.sprite == normal, "Urinal did not start with the normal sprite.");
                controller.SetSelected(true);
                Require(renderer.sprite == selected, "Selected urinal did not use the selected sprite.");
                controller.SetSelected(false);
                Require(renderer.sprite == normal, "Deselected urinal did not restore the normal sprite.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(fixture);
                UnityEngine.Object.DestroyImmediate(holder);
                UnityEngine.Object.DestroyImmediate(normal.texture);
                UnityEngine.Object.DestroyImmediate(selected.texture);
                UnityEngine.Object.DestroyImmediate(normal);
                UnityEngine.Object.DestroyImmediate(selected);
            }
        }

        private static void SetSpriteFields(
            NPCBusinessSpriteHolder holder,
            Sprite front01,
            Sprite front02,
            Sprite left01,
            Sprite left02,
            Sprite backPee)
        {
            var serializedHolder = new SerializedObject(holder);
            serializedHolder.FindProperty("npcBusinessFront01").objectReferenceValue = front01;
            serializedHolder.FindProperty("npcBusinessFront02").objectReferenceValue = front02;
            serializedHolder.FindProperty("npcBusinessLeft01").objectReferenceValue = left01;
            serializedHolder.FindProperty("npcBusinessLeft02").objectReferenceValue = left02;
            serializedHolder.FindProperty("npcBusinessBackPee").objectReferenceValue = backPee;
            serializedHolder.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Sprite CreateSprite(Color color)
        {
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), Vector2.zero);
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
