using System;
using Nyoice.Audio;
using UnityEditor;
using UnityEngine;

namespace Nyoice.Editor
{
    public static class NyoiceSprint513ASoundSystemFoundationValidator
    {
        private const string GameScenePath = "Assets/_Project/Scenes/GameScene.unity";
        private const string HolderPath = "Assets/_Project/Audio/AudioClipHolder.asset";

        [MenuItem("Nyoice/Validate Sprint5-13A Sound System Foundation")]
        public static void ValidateSoundSystemFoundation()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GameScenePath) == null)
            {
                NyoiceProjectSetup.SetupSprintOne();
            }

            NyoiceGameStageSetup.SetupGameStage();
            NyoiceGameStageSetup.SetupGameStage();

            ValidateSceneAudioManager();
            ValidateNullSafety();
            NyoiceSprint512HWallVisualClearanceValidator.ValidateWallVisualClearanceFix();
            Debug.Log("Sprint5-13A Sound System Foundation validation passed.");
        }

        private static void ValidateSceneAudioManager()
        {
            Transform gameSystems = GameObject.Find("GameSystems")?.transform;
            Require(gameSystems != null, "GameSystems is missing.");
            int namedCount = 0;
            for (int index = 0; index < gameSystems.childCount; index++)
            {
                if (gameSystems.GetChild(index).name == "AudioManager") namedCount++;
            }

            Require(namedCount == 1, "GameSystems must contain exactly one AudioManager object.");
            AudioManager[] managers = gameSystems.GetComponentsInChildren<AudioManager>(true);
            Require(managers.Length == 1, "Scene must contain exactly one AudioManager component.");
            AudioManager manager = managers[0];
            AudioSource[] sources = manager.GetComponents<AudioSource>();
            Require(sources.Length == 1 && manager.SeSource == sources[0],
                "AudioManager must own exactly one referenced SE AudioSource.");
            Require(!sources[0].playOnAwake && !sources[0].loop && Mathf.Approximately(sources[0].spatialBlend, 0f),
                "SE AudioSource settings are invalid.");

            AudioClipHolder holder = AssetDatabase.LoadAssetAtPath<AudioClipHolder>(HolderPath);
            Require(holder != null && manager.ClipHolder == holder,
                "AudioClipHolder asset or AudioManager reference is missing.");
            Require(holder.SoundEffects != null && holder.SoundEffects.Count == 0,
                "Sprint5-13A holder must not contain audio clips yet.");
        }

        private static void ValidateNullSafety()
        {
            var root = new GameObject("Sprint513ANullSafety");
            try
            {
                AudioManager manager = root.AddComponent<AudioManager>();
                manager.PlaySE(SoundEffectId.NpcSpawn);
                manager.PlaySE((AudioClip)null);
                manager.StopSE();

                AudioSource source = root.AddComponent<AudioSource>();
                manager.Configure(source, null);
                manager.PlaySE(SoundEffectId.GameOver);
                manager.PlaySE((AudioClip)null);
                manager.StopSE();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
