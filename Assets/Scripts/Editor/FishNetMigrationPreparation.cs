#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

namespace HillbillyTaxi.EditorTools
{
    /// <summary>
    /// Runs while the current NGO branch still compiles.
    /// Produces framework-neutral assets that remain usable after NGO is removed.
    /// </summary>
    public static class FishNetMigrationPreparation
    {
        private const string SourcePlayerPrefab =
            "Assets/Player.prefab";

        private const string MigrationFolder =
            "Assets/FishNetMigration";

        private const string NeutralPlayerPrefab =
            MigrationFolder + "/PlayerBase.prefab";

        private const string ProofScenePath =
            "Assets/Scenes/FishNetProof.unity";

        private static readonly string[] ProjectNetworkingRemovalOrder =
        {
            // Remove the component which requires the others first.
            "HillbillyTaxi.Player.NetworkPlayerCharacter",

            // These require NetworkObject.
            "HillbillyTaxi.Interaction.NetworkPlayerInteractor",
            "HillbillyTaxi.Player.NetworkPlayerSeatController",

            // Custom NGO transform derives from NGO NetworkTransform and must be
            // removed before the underlying NGO components.
            "HillbillyTaxi.Networking.OwnerNetworkTransform",

            // Temporary owner-only UI component is unnecessary in the neutral prefab.
            "HillbillyTaxi.Interaction.InteractionPromptView"
        };

        [MenuItem(
            "Hillbilly Taxi/FishNet Migration/Prepare Framework-Neutral Assets")]
        public static void PrepareFrameworkNeutralAssets()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning(
                    "Exit Play Mode before preparing the FishNet migration.");

                return;
            }

            if (!EditorSceneManager
                    .SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EnsureFolderExists(MigrationFolder);

            if (!CreateNeutralPlayerPrefab())
            {
                return;
            }

            CreateCleanProofScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                "FishNet migration preparation complete and verified. Created:\n" +
                "• Assets/FishNetMigration/PlayerBase.prefab\n" +
                "• Assets/Scenes/FishNetProof.unity\n\n" +
                "Close Unity before running the NGO removal script.");
        }

        private static bool CreateNeutralPlayerPrefab()
        {
            GameObject source =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    SourcePlayerPrefab);

            if (source == null)
            {
                Debug.LogError(
                    $"Could not find {SourcePlayerPrefab}.");

                return false;
            }

            // Always recreate the neutral copy. A previous failed preparation can
            // contain NGO components which would become missing scripts later.
            if (AssetDatabase.LoadAssetAtPath<GameObject>(
                    NeutralPlayerPrefab) != null)
            {
                AssetDatabase.DeleteAsset(
                    NeutralPlayerPrefab);
            }

            if (!AssetDatabase.CopyAsset(
                    SourcePlayerPrefab,
                    NeutralPlayerPrefab))
            {
                Debug.LogError(
                    "Unity could not copy the Player prefab.");

                return false;
            }

            AssetDatabase.ImportAsset(
                NeutralPlayerPrefab,
                ImportAssetOptions.ForceSynchronousImport);

            GameObject root =
                PrefabUtility.LoadPrefabContents(
                    NeutralPlayerPrefab);

            try
            {
                RemoveProjectNetworkingComponentsInDependencyOrder(root);
                RemoveNgoComponents(root);

                PlayerInput playerInput =
                    root.GetComponent<PlayerInput>();

                if (playerInput != null)
                {
                    playerInput.enabled = false;
                }

                Component inputReader =
                    FindComponentByFullName(
                        root,
                        "HillbillyTaxi.Input.PlayerInputReader");

                if (inputReader is Behaviour inputReaderBehaviour)
                {
                    inputReaderBehaviour.enabled = true;
                }

                Component motor =
                    FindComponentByFullName(
                        root,
                        "HillbillyTaxi.Player.FirstPersonCharacterMotor");

                if (motor is Behaviour motorBehaviour)
                {
                    motorBehaviour.enabled = true;
                }

                root.name = "PlayerBase";
                root.transform.localPosition = Vector3.zero;
                root.transform.localRotation = Quaternion.identity;
                root.transform.localScale = Vector3.one;

                if (!VerifyFrameworkNeutral(root))
                {
                    Debug.LogError(
                        "PlayerBase verification failed. The prefab was not saved.");

                    return false;
                }

                PrefabUtility.SaveAsPrefabAsset(
                    root,
                    NeutralPlayerPrefab);

                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void RemoveProjectNetworkingComponentsInDependencyOrder(
            GameObject root)
        {
            foreach (string fullName in ProjectNetworkingRemovalOrder)
            {
                // A prefab may contain more than one component of a type in children.
                while (true)
                {
                    Component component =
                        FindComponentByFullName(
                            root,
                            fullName);

                    if (component == null)
                    {
                        break;
                    }

                    UnityEngine.Object.DestroyImmediate(
                        component,
                        true);
                }
            }
        }

        private static void RemoveNgoComponents(GameObject root)
        {
            // Remove in repeated passes because some NGO helper components can depend
            // on another NGO component. Each pass removes everything currently legal.
            for (int pass = 0; pass < 10; pass++)
            {
                bool removedAnything = false;

                Component[] components =
                    root.GetComponentsInChildren<Component>(true);

                for (int index = components.Length - 1;
                     index >= 0;
                     index--)
                {
                    Component component = components[index];

                    if (component == null ||
                        component is Transform)
                    {
                        continue;
                    }

                    string fullName =
                        component.GetType().FullName ??
                        string.Empty;

                    if (!fullName.StartsWith(
                            "Unity.Netcode.",
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    UnityEngine.Object.DestroyImmediate(
                        component,
                        true);

                    removedAnything = true;
                }

                if (!removedAnything)
                {
                    break;
                }
            }
        }

        private static bool VerifyFrameworkNeutral(GameObject root)
        {
            List<string> forbiddenComponents =
                new List<string>();

            Component[] components =
                root.GetComponentsInChildren<Component>(true);

            foreach (Component component in components)
            {
                if (component == null)
                {
                    forbiddenComponents.Add(
                        "Missing Script");

                    continue;
                }

                string fullName =
                    component.GetType().FullName ??
                    string.Empty;

                bool isNgo =
                    fullName.StartsWith(
                        "Unity.Netcode.",
                        StringComparison.Ordinal);

                bool isProjectNetworkGlue =
                    Array.IndexOf(
                        ProjectNetworkingRemovalOrder,
                        fullName) >= 0;

                if (isNgo || isProjectNetworkGlue)
                {
                    forbiddenComponents.Add(fullName);
                }
            }

            if (forbiddenComponents.Count == 0)
            {
                return true;
            }

            Debug.LogError(
                "PlayerBase still contains forbidden components:\n• " +
                string.Join("\n• ", forbiddenComponents));

            return false;
        }

        private static Component FindComponentByFullName(
            GameObject root,
            string fullName)
        {
            Component[] components =
                root.GetComponentsInChildren<Component>(true);

            foreach (Component component in components)
            {
                if (component != null &&
                    component.GetType().FullName == fullName)
                {
                    return component;
                }
            }

            return null;
        }

        private static void CreateCleanProofScene()
        {
            Scene scene =
                EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene,
                    NewSceneMode.Single);

            GameObject lightObject =
                new GameObject("Directional Light");

            Light light =
                lightObject.AddComponent<Light>();

            light.type = LightType.Directional;
            light.intensity = 1f;

            lightObject.transform.rotation =
                Quaternion.Euler(50f, -30f, 0f);

            GameObject ground =
                GameObject.CreatePrimitive(
                    PrimitiveType.Plane);

            ground.name = "Ground";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale =
                new Vector3(5f, 1f, 5f);

            GameObject marker =
                GameObject.CreatePrimitive(
                    PrimitiveType.Cube);

            marker.name = "Movement Reference Cube";
            marker.transform.position =
                new Vector3(0f, 0.5f, 5f);

            marker.transform.localScale =
                new Vector3(2f, 1f, 2f);

            EditorSceneManager.SaveScene(
                scene,
                ProofScenePath);
        }

        private static void EnsureFolderExists(
            string folder)
        {
            string[] parts = folder.Split('/');
            string current = parts[0];

            for (int index = 1;
                 index < parts.Length;
                 index++)
            {
                string next =
                    current + "/" + parts[index];

                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(
                        current,
                        parts[index]);
                }

                current = next;
            }
        }
    }
}
#endif
