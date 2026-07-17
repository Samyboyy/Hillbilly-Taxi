using UnityEngine;

namespace HillbillyTaxi.FishNetMigration.Interaction
{
    /// <summary>
    /// Maps a child collider to one interaction ID on a parent interactable.
    /// This becomes important when the pickup exposes four distinct seat targets.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FishNetInteractionPoint : MonoBehaviour
    {
        [SerializeField] private FishNetInteractable interactable;
        [SerializeField, Min(0)] private int interactionId;

        public int InteractionId => interactionId;

        public bool TryGetTarget(
            out FishNetInteractable target,
            out int targetInteractionId)
        {
            ResolveInteractable();

            target = interactable;
            targetInteractionId = interactionId;
            return target != null;
        }

        private void ResolveInteractable()
        {
            if (interactable == null)
            {
                interactable =
                    GetComponentInParent<FishNetInteractable>(
                        true);
            }
        }

        private void Reset()
        {
            ResolveInteractable();
        }

        private void OnValidate()
        {
            interactionId = Mathf.Max(0, interactionId);
            ResolveInteractable();
        }
    }
}
