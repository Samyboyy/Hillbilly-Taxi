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
        [SerializeField] private FishNetTaxiJobZoneType zoneType;

        private readonly HashSet<Collider> _taxiColliders = new();

        private void FixedUpdate()
        {
            if (jobManager == null || !jobManager.IsServerInitialized)
            {
                return;
            }

            _taxiColliders.RemoveWhere(collider => collider == null);
            jobManager.ReportZonePresenceOnServer(
                zoneType,
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

        private bool IsConfiguredTaxiCollider(Collider candidate)
        {
            if (candidate == null ||
                jobManager == null ||
                jobManager.TaxiVehicle == null)
            {
                return false;
            }

            FishNetVehicle candidateVehicle =
                candidate.GetComponentInParent<FishNetVehicle>();

            return candidateVehicle == jobManager.TaxiVehicle;
        }

        private void Reset()
        {
            Collider zoneCollider = GetComponent<Collider>();
            if (zoneCollider != null)
            {
                zoneCollider.isTrigger = true;
            }
        }
    }
}
