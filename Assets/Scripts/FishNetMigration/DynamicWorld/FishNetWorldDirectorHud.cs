using HillbillyTaxi.FishNetMigration.TaxiJobs;
using UnityEngine;

namespace HillbillyTaxi.FishNetMigration.DynamicWorld
{
    /// <summary>
    /// Development-only visibility into the server's committed world state.
    /// This is diagnostic information, not player objective text.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FishNetWorldDirectorHud : MonoBehaviour
    {
        [SerializeField] private FishNetWorldDirector worldDirector;
        [SerializeField] private FishNetTaxiJobManager taxiJobManager;

        private Vector2 _scroll;

        private void OnGUI()
        {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            return;
#else
            if (worldDirector == null ||
                !worldDirector.IsClientInitialized)
            {
                return;
            }

            GUILayout.BeginArea(
                new Rect(12f, 530f, 430f, 285f),
                GUI.skin.box);

            GUILayout.Label(
                "DYNAMIC WORLD — DEVELOPMENT VIEW");

            GUILayout.Label(
                "These are committed county conditions, " +
                "not mission instructions.");

            GUILayout.Label(
                $"World cycle: " +
                $"{worldDirector.WorldCycle}");

            GUILayout.Label(
                $"Seed: {worldDirector.WorldSeed}");

            GUILayout.Space(5f);
            GUILayout.Label("Active situations");

            _scroll =
                GUILayout.BeginScrollView(
                    _scroll,
                    GUI.skin.box,
                    GUILayout.Height(125f));

            bool anyActive = false;

            for (int index = 0;
                 index <
                    worldDirector.Situations.Count;
                 index++)
            {
                if (!worldDirector
                        .IsSituationActive(index))
                {
                    continue;
                }

                anyActive = true;

                FishNetWorldSituationAnchor
                    situation =
                        worldDirector
                            .Situations[index];

                GUILayout.Label(
                    $"• {situation.DisplayName}");

                GUILayout.Label(
                    $"  {situation.DebugDescription}");
            }

            if (!anyActive)
            {
                GUILayout.Label(
                    "No situations active.");
            }

            GUILayout.EndScrollView();

            bool canReroll =
                taxiJobManager != null &&
                taxiJobManager.State ==
                    FishNetTaxiJobState
                        .WaitingForPickup;

            bool previousEnabled =
                GUI.enabled;

            GUI.enabled = canReroll;

            if (GUILayout.Button(
                    "Reroll Before Pickup",
                    GUILayout.Height(30f)))
            {
                worldDirector
                    .RequestDebugReroll();
            }

            GUI.enabled =
                previousEnabled;

            GUILayout.EndArea();
#endif
        }

        private void Reset()
        {
            if (worldDirector == null)
            {
                worldDirector =
                    GetComponent<
                        FishNetWorldDirector>();
            }

            if (taxiJobManager == null)
            {
                taxiJobManager =
                    FindFirstObjectByType<
                        FishNetTaxiJobManager>();
            }
        }
    }
}
