#if UNITY_EDITOR
using FishNet.Object;
using HillbillyTaxi.FishNetMigration.TaxiJobs;
using HillbillyTaxi.FishNetMigration.Vehicles;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HillbillyTaxi.EditorTools
{
    public static class FishNetPhaseNineContractInstaller
    {
        private const string TaxiName =
            "FishNet Test Pickup Seat Rig";

        private const string JobRootName =
            "FishNet First Taxi Job";

        private const string RidingAnchorName =
            "Taxi Passenger Riding Anchor";

        private const string PrototypeFolder =
            "Assets/FishNetMigration/TaxiPrototype";

        private const string ContractAssetPath =
            PrototypeFolder +
            "/MoonshineRunPrototype.asset";

        private const string RequiredStopName =
            "Miller's Garage Required Stop";

        [MenuItem(
            "Hillbilly Taxi/FishNet Migration/Phase 9A/" +
            "Install Destination Contract Revision")]
        public static void Install()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning(
                    "Exit Play Mode before installing the " +
                    "destination-contract revision.");

                return;
            }

            GameObject taxiObject =
                GameObject.Find(TaxiName);

            if (taxiObject == null ||
                !taxiObject.TryGetComponent(
                    out FishNetVehicle taxiVehicle))
            {
                Debug.LogError(
                    $"Could not find '{TaxiName}' with " +
                    $"{nameof(FishNetVehicle)}.");

                return;
            }

            GameObject jobRoot =
                GameObject.Find(JobRootName);

            if (jobRoot == null ||
                !jobRoot.TryGetComponent(
                    out NetworkObject networkObject) ||
                !jobRoot.TryGetComponent(
                    out FishNetTaxiJobManager manager) ||
                !jobRoot.TryGetComponent(
                    out FishNetTaxiJobHud hud))
            {
                Debug.LogError(
                    "The existing FishNet taxi-job proof is missing.");

                return;
            }

            EnsurePrototypeFolder();

            FishNetTaxiContractDefinition definition =
                CreateOrUpdateContractDefinition();

            Transform pickupRoot =
                jobRoot.transform.Find(
                    "Passenger Pickup");

            if (pickupRoot == null)
            {
                pickupRoot =
                    FindDescendant(
                        jobRoot.transform,
                        "Passenger Pickup");
            }

            Transform dropoffRoot =
                jobRoot.transform.Find(
                    "Passenger Drop-off");

            if (dropoffRoot == null)
            {
                dropoffRoot =
                    FindDescendant(
                        jobRoot.transform,
                        "Passenger Drop-off");
            }

            Transform waitingPassenger =
                pickupRoot != null
                    ? pickupRoot.Find(
                        "Waiting Passenger")
                    : null;

            if (waitingPassenger == null)
            {
                waitingPassenger =
                    FindDescendant(
                        jobRoot.transform,
                        "Waiting Passenger");
            }

            Transform ridingAnchor =
                taxiObject.transform.Find(
                    RidingAnchorName);

            Transform ridingPassenger =
                ridingAnchor != null
                    ? ridingAnchor.Find(
                        "Riding Passenger")
                    : null;

            if (pickupRoot == null ||
                dropoffRoot == null ||
                waitingPassenger == null ||
                ridingPassenger == null)
            {
                Debug.LogError(
                    "Could not find the existing pickup, drop-off " +
                    "or passenger presentation objects.");

                return;
            }

            RemoveArtificialRouteObjectives(
                jobRoot.transform);

            Material pickupMaterial =
                CreateOrUpdateMaterial(
                    $"{PrototypeFolder}/PickupMarker.mat",
                    new Color(1f, 0.72f, 0.08f, 1f),
                    1.25f);

            Material stopMaterial =
                CreateOrUpdateMaterial(
                    $"{PrototypeFolder}/RequiredStopMarker.mat",
                    new Color(0.15f, 0.62f, 1f, 1f),
                    1.2f);

            Material dropoffMaterial =
                CreateOrUpdateMaterial(
                    $"{PrototypeFolder}/DropoffMarker.mat",
                    new Color(0.18f, 0.95f, 0.35f, 1f),
                    1.15f);

            // Repair the exact serialized hierarchy state from the original
            // taxi proof. Passenger Pickup must stay active, its Waiting
            // Passenger child must begin active, and Passenger Drop-off must
            // stay active even while its marker child is hidden.
            ForceSceneObjectActive(
                pickupRoot.gameObject,
                "Enable Passenger Pickup");

            ForceSceneObjectActive(
                waitingPassenger.gameObject,
                "Enable Waiting Passenger");

            ForceSceneObjectActive(
                dropoffRoot.gameObject,
                "Enable Passenger Drop-off");

            UpgradeExistingZone(
                pickupRoot.gameObject,
                manager,
                "pickup",
                FishNetTaxiJobZoneType.Pickup,
                pickupMaterial,
                new Vector3(5f, 2f, 5f));

            RequiredStopSetup requiredStop =
                CreateOrUpdateRequiredStop(
                    jobRoot.transform,
                    manager,
                    waitingPassenger.gameObject,
                    stopMaterial);

            UpgradeExistingZone(
                dropoffRoot.gameObject,
                manager,
                "final_dropoff",
                FishNetTaxiJobZoneType.Dropoff,
                dropoffMaterial,
                new Vector3(5f, 2f, 5f));

            ConfigureManager(
                manager,
                taxiVehicle,
                definition,
                waitingPassenger.gameObject,
                ridingPassenger.gameObject);

            ConfigureHud(
                hud,
                manager);

            ConfigurePassengerPresentation(
                requiredStop.Presentation,
                manager,
                requiredStop.PassengerRoot,
                requiredStop.RoadsidePoint,
                requiredStop.EntrancePoint);

            ForceSceneObjectActive(
                pickupRoot.gameObject,
                "Enable Passenger Pickup");

            ForceSceneObjectActive(
                waitingPassenger.gameObject,
                "Enable Waiting Passenger");

            ForceSceneObjectActive(
                dropoffRoot.gameObject,
                "Enable Passenger Drop-off");

            ridingPassenger.gameObject.SetActive(false);
            requiredStop.PassengerRoot.SetActive(false);

            ApplyInitialMarkerVisibility(
                jobRoot.transform,
                activeObjectiveId: "pickup");

            EditorUtility.SetDirty(networkObject);
            EditorUtility.SetDirty(manager);
            EditorUtility.SetDirty(hud);
            EditorUtility.SetDirty(taxiObject);
            EditorUtility.SetDirty(definition);
            EditorUtility.SetDirty(
                requiredStop.Presentation);

            Scene activeScene =
                SceneManager.GetActiveScene();

            if (activeScene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(
                    activeScene);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeGameObject =
                jobRoot;

            Debug.Log(
                "Installed the Phase 9A destination-contract revision. " +
                "Artificial route checkpoints were removed. The contract " +
                "now requires pickup, Miller's Garage and the final " +
                "destination only. Save FishNetProof.unity.",
                jobRoot);
        }

        [MenuItem(
            "Hillbilly Taxi/FishNet Migration/Phase 9A/" +
            "Repair Objective Startup State")]
        public static void RepairObjectiveStartupState()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning(
                    "Exit Play Mode before repairing the objective hierarchy.");

                return;
            }

            GameObject jobRoot =
                GameObject.Find(JobRootName);

            if (jobRoot == null)
            {
                Debug.LogError(
                    $"Could not find '{JobRootName}'.");

                return;
            }

            Transform pickupRoot =
                jobRoot.transform.Find(
                    "Passenger Pickup");

            Transform dropoffRoot =
                jobRoot.transform.Find(
                    "Passenger Drop-off");

            Transform waitingPassenger =
                pickupRoot != null
                    ? pickupRoot.Find(
                        "Waiting Passenger")
                    : null;

            if (pickupRoot == null ||
                dropoffRoot == null ||
                waitingPassenger == null)
            {
                Debug.LogError(
                    "Could not resolve the exact Passenger Pickup, " +
                    "Waiting Passenger and Passenger Drop-off hierarchy.");

                return;
            }

            ForceSceneObjectActive(
                pickupRoot.gameObject,
                "Enable Passenger Pickup");

            ForceSceneObjectActive(
                waitingPassenger.gameObject,
                "Enable Waiting Passenger");

            ForceSceneObjectActive(
                dropoffRoot.gameObject,
                "Enable Passenger Drop-off");

            FishNetTaxiJobManager manager =
                jobRoot.GetComponent<
                    FishNetTaxiJobManager>();

            if (manager != null)
            {
                SerializedObject managerSerialized =
                    new(manager);

                SerializedProperty legacyPickupMarker =
                    managerSerialized.FindProperty(
                        "pickupMarkerRoot");

                if (legacyPickupMarker != null)
                {
                    legacyPickupMarker.objectReferenceValue =
                        null;
                }

                SerializedProperty legacyDropoffMarker =
                    managerSerialized.FindProperty(
                        "dropoffMarkerRoot");

                if (legacyDropoffMarker != null)
                {
                    legacyDropoffMarker.objectReferenceValue =
                        null;
                }

                managerSerialized
                    .ApplyModifiedPropertiesWithoutUndo();

                EditorUtility.SetDirty(manager);
            }

            ApplyInitialMarkerVisibility(
                jobRoot.transform,
                activeObjectiveId: "pickup");

            EditorUtility.SetDirty(jobRoot);
            EditorUtility.SetDirty(
                pickupRoot.gameObject);

            EditorUtility.SetDirty(
                waitingPassenger.gameObject);

            EditorUtility.SetDirty(
                dropoffRoot.gameObject);

            Scene activeScene =
                SceneManager.GetActiveScene();

            if (activeScene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(
                    activeScene);
            }

            Selection.activeGameObject =
                waitingPassenger.gameObject;

            Debug.Log(
                "Repaired Phase 9A startup hierarchy: Passenger Pickup, " +
                "Waiting Passenger and Passenger Drop-off are active. " +
                "Save FishNetProof.unity.",
                jobRoot);
        }

        private static void ForceSceneObjectActive(
            GameObject target,
            string undoName)
        {
            if (target == null)
            {
                return;
            }

            Undo.RecordObject(
                target,
                undoName);

            target.hideFlags =
                HideFlags.None;

            target.SetActive(true);

            EditorUtility.SetDirty(
                target);
        }

        private static FishNetTaxiContractDefinition
            CreateOrUpdateContractDefinition()
        {
            FishNetTaxiContractDefinition definition =
                AssetDatabase.LoadAssetAtPath<
                    FishNetTaxiContractDefinition>(
                    ContractAssetPath);

            if (definition == null)
            {
                definition =
                    ScriptableObject.CreateInstance<
                        FishNetTaxiContractDefinition>();

                AssetDatabase.CreateAsset(
                    definition,
                    ContractAssetPath);
            }

            SerializedObject serialized =
                new(definition);

            SetString(
                serialized,
                "contractId",
                "moonshine_run_prototype");

            SetString(
                serialized,
                "contractName",
                "The Moonshine Run");

            SetString(
                serialized,
                "passengerName",
                "Earl");

            SetString(
                serialized,
                "specialRule",
                "Keep Earl and his moonshine in one piece.");

            SetInt(
                serialized,
                "baseReward",
                125);

            SetString(
                serialized,
                "pickupObjectiveId",
                "pickup");

            SetString(
                serialized,
                "pickupLocationName",
                "Dusty's Diner");

            SetString(
                serialized,
                "pickupObjectiveText",
                "Pick up Earl at Dusty's Diner.");

            SetFloat(
                serialized,
                "pickupStopDuration",
                0.75f);

            SetBool(
                serialized,
                "hasRequiredStop",
                true);

            SetString(
                serialized,
                "requiredStopObjectiveId",
                "millers_garage");

            SetString(
                serialized,
                "requiredStopLocationName",
                "Miller's Garage");

            SetString(
                serialized,
                "travelToRequiredStopText",
                "Take Earl to Miller's Garage.");

            SetString(
                serialized,
                "waitAtRequiredStopText",
                "Wait for Earl.");

            SetFloat(
                serialized,
                "requiredStopArrivalDuration",
                0.75f);

            SetFloat(
                serialized,
                "passengerExitDuration",
                3f);

            SetFloat(
                serialized,
                "requiredStopWaitDuration",
                35f);

            SetFloat(
                serialized,
                "passengerReturnDuration",
                3.5f);

            SetBool(
                serialized,
                "requireTaxiNearbyDuringWait",
                true);

            SetString(
                serialized,
                "finalObjectiveId",
                "final_dropoff");

            SetString(
                serialized,
                "finalLocationName",
                "Red Mesa Airfield");

            SetString(
                serialized,
                "finalObjectiveText",
                "Take Earl to Red Mesa Airfield.");

            SetFloat(
                serialized,
                "finalStopDuration",
                0.75f);

            SetFloat(
                serialized,
                "finalTimeLimitSeconds",
                0f);

            SetBool(
                serialized,
                "failOnFinalTimeout",
                false);

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }

        private static RequiredStopSetup
            CreateOrUpdateRequiredStop(
                Transform parent,
                FishNetTaxiJobManager manager,
                GameObject passengerTemplate,
                Material markerMaterial)
        {
            Transform existing =
                parent.Find(RequiredStopName);

            GameObject root;

            if (existing == null)
            {
                root =
                    new GameObject(
                        RequiredStopName);

                Undo.RegisterCreatedObjectUndo(
                    root,
                    "Create Miller's Garage");

                root.transform.SetParent(
                    parent,
                    false);
            }
            else
            {
                root = existing.gameObject;
            }

            root.SetActive(true);

            root.transform.position =
                new Vector3(20f, 0f, 14f);

            BoxCollider trigger =
                root.GetComponent<BoxCollider>();

            if (trigger == null)
            {
                trigger =
                    Undo.AddComponent<BoxCollider>(
                        root);
            }

            trigger.isTrigger = true;
            trigger.center =
                new Vector3(0f, 1.5f, 0f);

            trigger.size =
                new Vector3(13f, 3f, 13f);

            FishNetTaxiJobZone zone =
                root.GetComponent<
                    FishNetTaxiJobZone>();

            if (zone == null)
            {
                zone =
                    Undo.AddComponent<
                        FishNetTaxiJobZone>(
                        root);
            }

            GameObject markerRoot =
                EnsureMarkerPresentation(
                    root,
                    markerMaterial);

            ConfigureZone(
                zone,
                manager,
                "millers_garage",
                FishNetTaxiJobZoneType.Dropoff,
                markerRoot);

            Transform buildingRoot =
                EnsureChild(
                    root.transform,
                    "Garage Building");

            EnsurePrimitive(
                buildingRoot,
                "Main Building",
                PrimitiveType.Cube,
                new Vector3(0f, 1.75f, 4.5f),
                new Vector3(8f, 3.5f, 5f),
                new Color(0.35f, 0.27f, 0.18f, 1f));

            EnsurePrimitive(
                buildingRoot,
                "Garage Door",
                PrimitiveType.Cube,
                new Vector3(0f, 1.5f, 1.94f),
                new Vector3(4.5f, 2.7f, 0.12f),
                new Color(0.2f, 0.22f, 0.23f, 1f));

            EnsurePrimitive(
                buildingRoot,
                "Roof",
                PrimitiveType.Cube,
                new Vector3(0f, 3.65f, 4.5f),
                new Vector3(8.6f, 0.3f, 5.6f),
                new Color(0.16f, 0.12f, 0.09f, 1f));

            Transform roadsidePoint =
                EnsureChild(
                    root.transform,
                    "Passenger Roadside Point");

            roadsidePoint.localPosition =
                new Vector3(-2.5f, 0f, -1.8f);

            Transform entrancePoint =
                EnsureChild(
                    root.transform,
                    "Passenger Entrance Point");

            entrancePoint.localPosition =
                new Vector3(0f, 0f, 1.65f);

            Transform presentationRoot =
                EnsureChild(
                    root.transform,
                    "Passenger Stop Presentation");

            GameObject passengerRoot =
                EnsurePassengerPresentation(
                    presentationRoot,
                    passengerTemplate);

            FishNetTaxiPassengerStopPresentation
                presentation =
                    root.GetComponent<
                        FishNetTaxiPassengerStopPresentation>();

            if (presentation == null)
            {
                presentation =
                    Undo.AddComponent<
                        FishNetTaxiPassengerStopPresentation>(
                        root);
            }

            return new RequiredStopSetup(
                root,
                markerRoot,
                zone,
                presentation,
                passengerRoot,
                roadsidePoint,
                entrancePoint);
        }

        private static GameObject
            EnsurePassengerPresentation(
                Transform parent,
                GameObject passengerTemplate)
        {
            Transform existing =
                parent.Find(
                    "Earl Stop Presentation");

            if (existing != null)
            {
                return existing.gameObject;
            }

            GameObject clone =
                Object.Instantiate(
                    passengerTemplate,
                    parent);

            clone.name =
                "Earl Stop Presentation";

            Undo.RegisterCreatedObjectUndo(
                clone,
                "Create Earl Stop Presentation");

            clone.transform.localPosition =
                Vector3.zero;

            clone.transform.localRotation =
                Quaternion.identity;

            RemoveAllColliders(clone);
            return clone;
        }

        private static void
            RemoveArtificialRouteObjectives(
                Transform jobRoot)
        {
            string[] obsoleteNames =
            {
                "Early Journey Checkpoint",
                "Blocked Road Detour",
                "Scrapyard Repair Stop"
            };

            foreach (string obsoleteName in obsoleteNames)
            {
                Transform obsolete =
                    jobRoot.Find(obsoleteName);

                if (obsolete != null)
                {
                    Undo.DestroyObjectImmediate(
                        obsolete.gameObject);
                }
            }
        }

        private static void UpgradeExistingZone(
            GameObject root,
            FishNetTaxiJobManager manager,
            string objectiveId,
            FishNetTaxiJobZoneType legacyType,
            Material markerMaterial,
            Vector3 triggerSize)
        {
            root.SetActive(true);

            BoxCollider trigger =
                root.GetComponent<BoxCollider>();

            if (trigger == null)
            {
                trigger =
                    Undo.AddComponent<BoxCollider>(
                        root);
            }

            trigger.isTrigger = true;
            trigger.center =
                new Vector3(0f, 1f, 0f);

            trigger.size = triggerSize;

            FishNetTaxiJobZone zone =
                root.GetComponent<
                    FishNetTaxiJobZone>();

            if (zone == null)
            {
                zone =
                    Undo.AddComponent<
                        FishNetTaxiJobZone>(
                        root);
            }

            GameObject markerRoot =
                EnsureMarkerPresentation(
                    root,
                    markerMaterial);

            ConfigureZone(
                zone,
                manager,
                objectiveId,
                legacyType,
                markerRoot);
        }

        private static GameObject
            EnsureMarkerPresentation(
                GameObject zoneRoot,
                Material markerMaterial)
        {
            Transform existing =
                zoneRoot.transform.Find(
                    "Marker Presentation");

            GameObject markerRoot;

            if (existing == null)
            {
                markerRoot =
                    new GameObject(
                        "Marker Presentation");

                Undo.RegisterCreatedObjectUndo(
                    markerRoot,
                    "Create Marker Presentation");

                markerRoot.transform.SetParent(
                    zoneRoot.transform,
                    false);
            }
            else
            {
                markerRoot =
                    existing.gameObject;
            }

            MoveExistingMarkerChild(
                zoneRoot.transform,
                markerRoot.transform,
                "Ground Marker");

            MoveExistingMarkerChild(
                zoneRoot.transform,
                markerRoot.transform,
                "Vertical Beacon");

            Transform disc =
                markerRoot.transform.Find(
                    "Ground Marker");

            if (disc == null)
            {
                GameObject created =
                    GameObject.CreatePrimitive(
                        PrimitiveType.Cylinder);

                Undo.RegisterCreatedObjectUndo(
                    created,
                    "Create Ground Marker");

                created.name =
                    "Ground Marker";

                created.transform.SetParent(
                    markerRoot.transform,
                    false);

                created.transform.localPosition =
                    new Vector3(0f, 0.06f, 0f);

                created.transform.localScale =
                    new Vector3(
                        2.35f,
                        0.06f,
                        2.35f);

                RemovePrimitiveCollider(created);
                disc = created.transform;
            }

            Transform beacon =
                markerRoot.transform.Find(
                    "Vertical Beacon");

            if (beacon == null)
            {
                GameObject created =
                    GameObject.CreatePrimitive(
                        PrimitiveType.Cube);

                Undo.RegisterCreatedObjectUndo(
                    created,
                    "Create Vertical Beacon");

                created.name =
                    "Vertical Beacon";

                created.transform.SetParent(
                    markerRoot.transform,
                    false);

                created.transform.localPosition =
                    new Vector3(0f, 1.5f, 0f);

                created.transform.localScale =
                    new Vector3(
                        0.12f,
                        3f,
                        0.12f);

                RemovePrimitiveCollider(created);
                beacon = created.transform;
            }

            ApplyMaterial(
                disc.gameObject,
                markerMaterial);

            ApplyMaterial(
                beacon.gameObject,
                markerMaterial);

            return markerRoot;
        }

        private static void ConfigureManager(
            FishNetTaxiJobManager manager,
            FishNetVehicle taxiVehicle,
            FishNetTaxiContractDefinition definition,
            GameObject waitingPassenger,
            GameObject ridingPassenger)
        {
            SerializedObject serialized =
                new(manager);

            serialized.FindProperty("taxiVehicle")
                .objectReferenceValue =
                taxiVehicle;

            serialized.FindProperty("contractDefinition")
                .objectReferenceValue =
                definition;

            serialized.FindProperty("waitingPassengerRoot")
                .objectReferenceValue =
                waitingPassenger;

            serialized.FindProperty("ridingPassengerRoot")
                .objectReferenceValue =
                ridingPassenger;

            // Clear Phase 6 references. In the original proof these could
            // point at the full Passenger Pickup and Passenger Drop-off roots.
            SerializedProperty legacyPickupMarker =
                serialized.FindProperty(
                    "pickupMarkerRoot");

            if (legacyPickupMarker != null)
            {
                legacyPickupMarker.objectReferenceValue =
                    null;
            }

            SerializedProperty legacyDropoffMarker =
                serialized.FindProperty(
                    "dropoffMarkerRoot");

            if (legacyDropoffMarker != null)
            {
                legacyDropoffMarker.objectReferenceValue =
                    null;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureHud(
            FishNetTaxiJobHud hud,
            FishNetTaxiJobManager manager)
        {
            SerializedObject serialized =
                new(hud);

            serialized.FindProperty("jobManager")
                .objectReferenceValue =
                manager;

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void
            ConfigurePassengerPresentation(
                FishNetTaxiPassengerStopPresentation presentation,
                FishNetTaxiJobManager manager,
                GameObject passengerRoot,
                Transform roadsidePoint,
                Transform entrancePoint)
        {
            SerializedObject serialized =
                new(presentation);

            serialized.FindProperty("jobManager")
                .objectReferenceValue =
                manager;

            serialized.FindProperty("passengerRoot")
                .objectReferenceValue =
                passengerRoot;

            serialized.FindProperty("roadsidePoint")
                .objectReferenceValue =
                roadsidePoint;

            serialized.FindProperty("entrancePoint")
                .objectReferenceValue =
                entrancePoint;

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureZone(
            FishNetTaxiJobZone zone,
            FishNetTaxiJobManager manager,
            string objectiveId,
            FishNetTaxiJobZoneType legacyType,
            GameObject markerRoot)
        {
            SerializedObject serialized =
                new(zone);

            serialized.FindProperty("jobManager")
                .objectReferenceValue =
                manager;

            serialized.FindProperty("zoneType")
                .enumValueIndex =
                (int)legacyType;

            serialized.FindProperty("objectiveId")
                .stringValue =
                objectiveId;

            serialized.FindProperty("markerRoot")
                .objectReferenceValue =
                markerRoot;

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void
            ApplyInitialMarkerVisibility(
                Transform jobRoot,
                string activeObjectiveId)
        {
            FishNetTaxiJobZone[] zones =
                jobRoot.GetComponentsInChildren<
                    FishNetTaxiJobZone>(
                    includeInactive: true);

            foreach (FishNetTaxiJobZone zone in zones)
            {
                SerializedObject serialized =
                    new(zone);

                string id =
                    serialized.FindProperty(
                            "objectiveId")
                        .stringValue;

                GameObject marker =
                    serialized.FindProperty(
                            "markerRoot")
                        .objectReferenceValue
                    as GameObject;

                if (marker != null)
                {
                    marker.SetActive(
                        id == activeObjectiveId);
                }
            }
        }

        private static Transform EnsureChild(
            Transform parent,
            string childName)
        {
            Transform existing =
                parent.Find(childName);

            if (existing != null)
            {
                return existing;
            }

            GameObject child =
                new(childName);

            Undo.RegisterCreatedObjectUndo(
                child,
                $"Create {childName}");

            child.transform.SetParent(
                parent,
                false);

            return child.transform;
        }

        private static void EnsurePrimitive(
            Transform parent,
            string objectName,
            PrimitiveType primitiveType,
            Vector3 localPosition,
            Vector3 localScale,
            Color color)
        {
            Transform existing =
                parent.Find(objectName);

            GameObject target;

            if (existing == null)
            {
                target =
                    GameObject.CreatePrimitive(
                        primitiveType);

                Undo.RegisterCreatedObjectUndo(
                    target,
                    $"Create {objectName}");

                target.name = objectName;

                target.transform.SetParent(
                    parent,
                    false);
            }
            else
            {
                target = existing.gameObject;
            }

            target.transform.localPosition =
                localPosition;

            target.transform.localRotation =
                Quaternion.identity;

            target.transform.localScale =
                localScale;

            RemovePrimitiveCollider(target);

            Renderer renderer =
                target.GetComponent<Renderer>();

            if (renderer != null)
            {
                Material material =
                    new(
                        renderer.sharedMaterial);

                material.color = color;

                if (material.HasProperty(
                        "_BaseColor"))
                {
                    material.SetColor(
                        "_BaseColor",
                        color);
                }

                renderer.sharedMaterial =
                    material;
            }
        }

        private static void
            MoveExistingMarkerChild(
                Transform zoneRoot,
                Transform markerRoot,
                string childName)
        {
            Transform child =
                zoneRoot.Find(childName);

            if (child == null ||
                child.parent == markerRoot)
            {
                return;
            }

            Undo.SetTransformParent(
                child,
                markerRoot,
                "Move Marker Presentation");
        }

        private static Transform FindDescendant(
            Transform root,
            string objectName)
        {
            if (root.name == objectName)
            {
                return root;
            }

            for (int index = 0;
                 index < root.childCount;
                 index++)
            {
                Transform result =
                    FindDescendant(
                        root.GetChild(index),
                        objectName);

                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private static Material
            CreateOrUpdateMaterial(
                string path,
                Color color,
                float emissionMultiplier)
        {
            Material material =
                AssetDatabase.LoadAssetAtPath<
                    Material>(path);

            if (material == null)
            {
                Shader shader =
                    Shader.Find(
                        "Universal Render Pipeline/Lit");

                if (shader == null)
                {
                    shader =
                        Shader.Find("Standard");
                }

                material =
                    new Material(shader);

                AssetDatabase.CreateAsset(
                    material,
                    path);
            }

            material.color = color;

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor(
                    "_BaseColor",
                    color);
            }

            if (emissionMultiplier > 0f &&
                material.HasProperty(
                    "_EmissionColor"))
            {
                material.EnableKeyword(
                    "_EMISSION");

                material.SetColor(
                    "_EmissionColor",
                    color * emissionMultiplier);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsurePrototypeFolder()
        {
            if (!AssetDatabase.IsValidFolder(
                    "Assets/FishNetMigration"))
            {
                AssetDatabase.CreateFolder(
                    "Assets",
                    "FishNetMigration");
            }

            if (!AssetDatabase.IsValidFolder(
                    PrototypeFolder))
            {
                AssetDatabase.CreateFolder(
                    "Assets/FishNetMigration",
                    "TaxiPrototype");
            }
        }

        private static void RemovePrimitiveCollider(
            GameObject primitive)
        {
            Collider collider =
                primitive.GetComponent<Collider>();

            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }
        }

        private static void RemoveAllColliders(
            GameObject root)
        {
            Collider[] colliders =
                root.GetComponentsInChildren<
                    Collider>(
                    includeInactive: true);

            foreach (Collider collider in colliders)
            {
                Object.DestroyImmediate(collider);
            }
        }

        private static void ApplyMaterial(
            GameObject target,
            Material material)
        {
            Renderer renderer =
                target.GetComponent<Renderer>();

            if (renderer != null)
            {
                renderer.sharedMaterial =
                    material;
            }
        }

        private static void SetString(
            SerializedObject serialized,
            string propertyName,
            string value)
        {
            SerializedProperty property =
                serialized.FindProperty(
                    propertyName);

            if (property != null)
            {
                property.stringValue = value;
            }
        }

        private static void SetInt(
            SerializedObject serialized,
            string propertyName,
            int value)
        {
            SerializedProperty property =
                serialized.FindProperty(
                    propertyName);

            if (property != null)
            {
                property.intValue = value;
            }
        }

        private static void SetFloat(
            SerializedObject serialized,
            string propertyName,
            float value)
        {
            SerializedProperty property =
                serialized.FindProperty(
                    propertyName);

            if (property != null)
            {
                property.floatValue = value;
            }
        }

        private static void SetBool(
            SerializedObject serialized,
            string propertyName,
            bool value)
        {
            SerializedProperty property =
                serialized.FindProperty(
                    propertyName);

            if (property != null)
            {
                property.boolValue = value;
            }
        }

        private readonly struct RequiredStopSetup
        {
            public RequiredStopSetup(
                GameObject root,
                GameObject markerRoot,
                FishNetTaxiJobZone zone,
                FishNetTaxiPassengerStopPresentation presentation,
                GameObject passengerRoot,
                Transform roadsidePoint,
                Transform entrancePoint)
            {
                Root = root;
                MarkerRoot = markerRoot;
                Zone = zone;
                Presentation = presentation;
                PassengerRoot = passengerRoot;
                RoadsidePoint = roadsidePoint;
                EntrancePoint = entrancePoint;
            }

            public GameObject Root { get; }
            public GameObject MarkerRoot { get; }
            public FishNetTaxiJobZone Zone { get; }

            public FishNetTaxiPassengerStopPresentation
                Presentation { get; }

            public GameObject PassengerRoot { get; }
            public Transform RoadsidePoint { get; }
            public Transform EntrancePoint { get; }
        }
    }
}
#endif
