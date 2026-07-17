using UnityEngine;

namespace HillbillyTaxi.FishNetMigration.TaxiJobs
{
    [DisallowMultipleComponent]
    public sealed class FishNetTaxiJobHud : MonoBehaviour
    {
        [SerializeField] private FishNetTaxiJobManager jobManager;
        [SerializeField, Min(260f)] private float panelWidth = 390f;

        private void OnGUI()
        {
            if (jobManager == null || !jobManager.IsClientInitialized)
            {
                return;
            }

            Rect area = new(12f, 270f, Mathf.Max(260f, panelWidth), 145f);
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label("HILLBILLY TAXI JOB");
            GUILayout.Label(jobManager.ObjectiveText);
            GUILayout.Space(4f);
            GUILayout.Label($"Current fare: £{jobManager.CurrentFare}");
            GUILayout.Label($"Session earnings: £{jobManager.TotalEarnings}");
            GUILayout.Label($"Completed fares: {jobManager.CompletedJobs}");
            GUILayout.EndArea();
        }

        private void Reset()
        {
            if (jobManager == null)
            {
                jobManager = GetComponent<FishNetTaxiJobManager>();
            }
        }

        private void OnValidate()
        {
            panelWidth = Mathf.Max(260f, panelWidth);
        }
    }
}
