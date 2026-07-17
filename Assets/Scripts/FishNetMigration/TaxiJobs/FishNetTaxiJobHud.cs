using UnityEngine;

namespace HillbillyTaxi.FishNetMigration.TaxiJobs
{
    [DisallowMultipleComponent]
    public sealed class FishNetTaxiJobHud : MonoBehaviour
    {
        [SerializeField] private FishNetTaxiJobManager jobManager;
        [SerializeField, Min(380f)] private float panelWidth = 470f;

        [SerializeField]
        private Vector2 panelPosition =
            new(550f, 12f);

        private void OnGUI()
        {
            if (jobManager == null ||
                !jobManager.IsClientInitialized)
            {
                return;
            }

            float height =
                jobManager.IsTerminal
                    ? 280f
                    : 265f;

            GUILayout.BeginArea(
                new Rect(
                    panelPosition.x,
                    panelPosition.y,
                    Mathf.Max(
                        380f,
                        panelWidth),
                    height),
                GUI.skin.box);

            GUILayout.Label(
                "HILLBILLY TAXI CONTRACT");

            GUILayout.Label(
                jobManager.ContractName);

            GUILayout.Space(5f);

            GUILayout.Label(
                $"Destination: " +
                $"{jobManager.ActiveDestinationName}");

            GUILayout.Label(
                jobManager.ObjectiveText);

            GUILayout.Space(5f);

            GUILayout.Label(
                $"{jobManager.PassengerName}: " +
                $"{jobManager.PassengerStatusText}");

            GUILayout.Label(
                $"Special condition: " +
                $"{jobManager.SpecialRule}");

            if (jobManager.ShowStateTimer &&
                !jobManager.IsTerminal)
            {
                GUILayout.Label(
                    $"Time: " +
                    $"{FormatTime(jobManager.StateTimeRemaining)}");

                if (jobManager.State ==
                        FishNetTaxiJobState
                            .WaitingAtRequiredStop &&
                    !jobManager.TaxiAtRequiredStop)
                {
                    GUILayout.Label(
                        "Timer paused: taxi is outside the waiting area.");
                }
            }

            GUILayout.Space(5f);

            GUILayout.Label(
                $"Contract value: " +
                $"£{jobManager.CurrentPayout}");

            GUILayout.Label(
                $"Session earnings: " +
                $"£{jobManager.TotalEarnings}");

            GUILayout.Label(
                $"Completed contracts: " +
                $"{jobManager.CompletedContracts}");

            GUILayout.Label(
                $"Run time: " +
                $"{FormatTime(jobManager.ContractElapsedSeconds)}");

            if (jobManager.CanRestartPrototype)
            {
                GUILayout.Space(7f);

                if (GUILayout.Button(
                        "Restart Prototype Contract",
                        GUILayout.Height(32f)))
                {
                    jobManager.RequestPrototypeRestart();
                }
            }

            GUILayout.EndArea();
        }

        private static string FormatTime(float seconds)
        {
            int totalSeconds =
                Mathf.Max(
                    0,
                    Mathf.CeilToInt(seconds));

            int minutes =
                totalSeconds / 60;

            int remainingSeconds =
                totalSeconds % 60;

            return
                $"{minutes:00}:{remainingSeconds:00}";
        }

        private void Reset()
        {
            if (jobManager == null)
            {
                jobManager =
                    GetComponent<
                        FishNetTaxiJobManager>();
            }
        }

        private void OnValidate()
        {
            panelWidth =
                Mathf.Max(
                    380f,
                    panelWidth);
        }
    }
}
