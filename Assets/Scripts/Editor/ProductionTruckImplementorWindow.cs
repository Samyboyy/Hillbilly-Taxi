#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace HillbillyTaxi.EditorTools
{
    /// <summary>
    /// Compatibility menu replacing the retired destructive V1 implementor.
    /// </summary>
    public sealed class ProductionTruckImplementorWindow :
        EditorWindow
    {
        [MenuItem(
            "Hillbilly Taxi/Truck/Production Truck Implementor")]
        public static void OpenWindow()
        {
            GetWindow<ProductionTruckImplementorWindow>(
                "Truck Integration V2");
        }

        [MenuItem(
            "Hillbilly Taxi/Truck/Implement Selected Production Truck")]
        public static void RedirectOldDirectCommand()
        {
            ProductionTruckIntegrationV2Finalizer
                .FinalizeCurrentWorkingTruck();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10f);

            EditorGUILayout.LabelField(
                "Production Truck Integration V2",
                EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "The destructive V1 model/physics rebuild has been retired. " +
                "The current scene truck has already passed Play Mode. " +
                "V2 validates and consolidates that working hierarchy without " +
                "regenerating WheelColliders.",
                MessageType.Info);

            if (GUILayout.Button(
                    "FINALIZE CURRENT WORKING TRUCK",
                    GUILayout.Height(44f)))
            {
                ProductionTruckIntegrationV2Finalizer
                    .FinalizeCurrentWorkingTruck();
            }

            if (GUILayout.Button(
                    "VALIDATE FINAL INTEGRATION",
                    GUILayout.Height(34f)))
            {
                ProductionTruckIntegrationV2Finalizer
                    .ValidateFinalIntegration();
            }

            if (GUILayout.Button(
                    "CREATE PREFAB SNAPSHOT",
                    GUILayout.Height(34f)))
            {
                ProductionTruckIntegrationV2Finalizer
                    .CreatePrefabSnapshot();
            }
        }
    }
}
#endif
