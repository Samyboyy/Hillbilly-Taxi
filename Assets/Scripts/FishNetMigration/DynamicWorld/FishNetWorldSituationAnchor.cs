using UnityEngine;

namespace HillbillyTaxi.FishNetMigration.DynamicWorld
{
    public enum FishNetWorldSituationPhase
    {
        ContractStart = 0,
        RequiredStopWait = 1,
        FinalJourney = 2
    }

    /// <summary>
    /// A stable situation at a committed county location.
    ///
    /// The anchor always remains active. Only its presentation root is toggled,
    /// preserving stable ordering and allowing the director to reconstruct state
    /// for late joiners.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FishNetWorldSituationAnchor : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private string situationId = "situation";
        [SerializeField] private string displayName = "World Situation";

        [SerializeField, TextArea(2, 4)]
        private string debugDescription =
            "A persistent county condition.";

        [Header("Selection")]
        [SerializeField] private FishNetWorldSituationPhase activationPhase =
            FishNetWorldSituationPhase.ContractStart;

        [Tooltip(
            "One situation is selected from each non-empty selection group. " +
            "Situations with an empty group always activate at their phase.")]
        [SerializeField] private string selectionGroup = "route_primary";

        [SerializeField, Min(0.01f)] private float selectionWeight = 1f;

        [Header("Presentation")]
        [SerializeField] private GameObject presentationRoot;

        public string SituationId =>
            string.IsNullOrWhiteSpace(situationId)
                ? gameObject.name
                : situationId.Trim();

        public string DisplayName =>
            string.IsNullOrWhiteSpace(displayName)
                ? gameObject.name
                : displayName.Trim();

        public string DebugDescription =>
            string.IsNullOrWhiteSpace(debugDescription)
                ? "Persistent world situation."
                : debugDescription.Trim();

        public FishNetWorldSituationPhase ActivationPhase =>
            activationPhase;

        public string SelectionGroup =>
            string.IsNullOrWhiteSpace(selectionGroup)
                ? string.Empty
                : selectionGroup.Trim();

        public float SelectionWeight =>
            Mathf.Max(0.01f, selectionWeight);

        public bool IsPresentationActive =>
            presentationRoot != null &&
            presentationRoot.activeSelf;

        internal void SetSituationActive(bool active)
        {
            if (presentationRoot != null &&
                presentationRoot.activeSelf != active)
            {
                presentationRoot.SetActive(active);
            }
        }

        private void Reset()
        {
            if (presentationRoot == null)
            {
                Transform candidate =
                    transform.Find("Presentation");

                if (candidate != null)
                {
                    presentationRoot =
                        candidate.gameObject;
                }
            }
        }

        private void OnValidate()
        {
            selectionWeight =
                Mathf.Max(0.01f, selectionWeight);
        }
    }
}
