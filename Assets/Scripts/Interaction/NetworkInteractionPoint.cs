using UnityEngine;

namespace HillbillyTaxi.Interaction
{
    /// <summary>
    /// Maps a child collider to one interaction on a parent NetworkInteractable.
    /// This lets one NetworkObject expose several distinct targets, such as four
    /// vehicle seats or multiple doors.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetworkInteractionPoint : MonoBehaviour
    {
        [SerializeField] private NetworkInteractable interactable;
        [SerializeField, Min(0)] private int interactionId;

        public NetworkInteractable Interactable
        {
            get
            {
                ResolveInteractable();
                return interactable;
            }
        }

        public int InteractionId => interactionId;

        public bool TryGetTarget(
            out NetworkInteractable target,
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
                    GetComponentInParent<NetworkInteractable>(true);
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
