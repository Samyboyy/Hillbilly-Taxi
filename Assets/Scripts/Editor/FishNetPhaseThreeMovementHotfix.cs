#if UNITY_EDITOR
using HillbillyTaxi.Player;
using UnityEditor;
using UnityEngine;

namespace HillbillyTaxi.EditorTools
{
    public static class FishNetPhaseThreeMovementHotfix
    {
        private const string FishNetPlayerPath =
            "Assets/FishNetMigration/FishNetPlayer.prefab";

        [MenuItem(
            "Hillbilly Taxi/FishNet Migration/Phase 3/Restore Movement Tuning")]
        public static void RestoreMovementTuning()
        {
            GameObject root =
                PrefabUtility.LoadPrefabContents(
                    FishNetPlayerPath);

            if (root == null)
            {
                Debug.LogError(
                    $"Could not load {FishNetPlayerPath}.");

                return;
            }

            try
            {
                FirstPersonCharacterMotor motor =
                    root.GetComponent<
                        FirstPersonCharacterMotor>();

                if (motor == null)
                {
                    Debug.LogError(
                        "FishNetPlayer has no FirstPersonCharacterMotor.");

                    return;
                }

                SerializedObject serializedMotor =
                    new SerializedObject(motor);

                SerializedProperty acceleration =
                    serializedMotor.FindProperty(
                        "groundAcceleration");

                if (acceleration == null)
                {
                    Debug.LogError(
                        "Could not find the groundAcceleration field.");

                    return;
                }

                acceleration.floatValue = 24f;
                serializedMotor.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(
                    root,
                    FishNetPlayerPath);

                AssetDatabase.SaveAssets();

                Debug.Log(
                    "Restored FishNetPlayer ground acceleration to 24. " +
                    "The movement-mode reset bug is fixed in code, so 1000 is no longer needed.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
#endif
