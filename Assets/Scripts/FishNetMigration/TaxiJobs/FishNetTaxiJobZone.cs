using System.Collections.Generic;
using HillbillyTaxi.FishNetMigration.Vehicles;
using UnityEngine;

namespace HillbillyTaxi.FishNetMigration.TaxiJobs
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class FishNetTaxiJobZone : MonoBehaviour
    {
        [SerializeField] private FishNetTaxiJobManager jobManager;

        // Legacy Phase 6 value retained for existing serialized data.
        [SerializeField] private FishNetTaxiJobZoneType zoneType;

        [SerializeField] private string objectiveId = string.Empty;
        [SerializeField] private GameObject markerRoot;

        private readonly HashSet<Collider> _taxiColliders = new();

        public string ObjectiveId
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(objectiveId))
                {
                    return objectiveId.Trim();
                }

                return zoneType ==
                       FishNetTaxiJobZoneType.Pickup
                    ? "pickup"
                    : "final_dropoff";
            }
        }

        private void FixedUpdate()
        {
            if (jobManager == null ||
                !jobManager.IsServerInitialized)
            {
                return;
            }

            _taxiColliders.RemoveWhere(
                collider => collider == null);

            jobManager.ReportZonePresenceOnServer(
                ObjectiveId,
                _taxiColliders.Count > 0,
                Time.fixedDeltaTime);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (IsConfiguredTaxiCollider(other))
            {
                _taxiColliders.Add(other);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            _taxiColliders.Remove(other);
        }

        private void OnDisable()
        {
            _taxiColliders.Clear();
        }

        internal void SetMarkerVisible(bool visible)
        {
            if (markerRoot != null &&
                markerRoot.activeSelf != visible)
            {
                markerRoot.SetActive(visible);
            }
        }

        private bool IsConfiguredTaxiCollider(
            Collider candidate)
        {
            if (candidate == null ||
                jobManager == null ||
                jobManager.TaxiVehicle == null)
            {
                return false;
            }

            FishNetVehicle candidateVehicle =
                candidate.GetComponentInParent<
                    FishNetVehicle>();

            return candidateVehicle ==
                   jobManager.TaxiVehicle;
        }

        private void Reset()
        {
            Collider zoneCollider =
                GetComponent<Collider>();

            if (zoneCollider != null)
            {
                zoneCollider.isTrigger = true;
            }

            if (markerRoot == null)
            {
                Transform candidate =
                    transform.Find(
                        "Marker Presentation");

                if (candidate != null)
                {
                    markerRoot =
                        candidate.gameObject;
                }
            }
        }
    }
}
