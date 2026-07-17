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
    public static class FishNetFirstTaxiJobInstaller
    {
        private const string TaxiName = "FishNet Test Pickup Seat Rig";
        private const string JobRootName = "FishNet First Taxi Job";
        private const string RidingAnchorName = "Taxi Passenger Riding Anchor";
        private const string MaterialFolder = "Assets/FishNetMigration/TaxiPrototype";

        [MenuItem("Hillbilly Taxi/FishNet Migration/Phase 6/Create First Taxi Job")]
        public static void CreateFirstTaxiJob()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Exit Play Mode before creating the taxi job.");
                return;
            }

            GameObject taxiObject = GameObject.Find(TaxiName);
            if (taxiObject == null ||
                !taxiObject.TryGetComponent(out FishNetVehicle taxiVehicle))
            {
                Debug.LogError(
                    $"Could not find '{TaxiName}' with " +
                    $"{nameof(FishNetVehicle)} in the active scene.");
                return;
            }

            GameObject existing = GameObject.Find(JobRootName);
            if (existing != null)
            {
                bool replace = EditorUtility.DisplayDialog(
                    "Replace first taxi job?",
                    "The first taxi-job prototype already exists.",
                    "Replace",
                    "Cancel");

                if (!replace)
                {
                    return;
                }

                Undo.DestroyObjectImmediate(existing);
            }

            Transform oldRidingAnchor = taxiObject.transform.Find(RidingAnchorName);
            if (oldRidingAnchor != null)
            {
                Undo.DestroyObjectImmediate(oldRidingAnchor.gameObject);
            }

            EnsureMaterialFolder();

            Material pickupMaterial = CreateOrUpdateMaterial(
                $"{MaterialFolder}/PickupMarker.mat",
                new Color(1f, 0.72f, 0.08f, 1f),
                1.25f);

            Material dropoffMaterial = CreateOrUpdateMaterial(
                $"{MaterialFolder}/DropoffMarker.mat",
                new Color(0.18f, 0.95f, 0.35f, 1f),
                1.15f);

            Material passengerMaterial = CreateOrUpdateMaterial(
                $"{MaterialFolder}/PassengerPrototype.mat",
                new Color(0.78f, 0.34f, 0.18f, 1f),
                0f);

            GameObject jobRoot = new(JobRootName);
            Undo.RegisterCreatedObjectUndo(jobRoot, "Create First FishNet Taxi Job");

            NetworkObject networkObject = Undo.AddComponent<NetworkObject>(jobRoot);
            FishNetTaxiJobManager manager =
                Undo.AddComponent<FishNetTaxiJobManager>(jobRoot);
            FishNetTaxiJobHud hud =
                Undo.AddComponent<FishNetTaxiJobHud>(jobRoot);

            ZoneSetup pickup = CreateZone(
                jobRoot.transform,
                "Passenger Pickup",
                new Vector3(-7f, 0f, 8f),
                pickupMaterial);

            ZoneSetup dropoff = CreateZone(
                jobRoot.transform,
                "Passenger Drop-off",
                new Vector3(17f, 0f, 8f),
                dropoffMaterial);

            GameObject waitingPassenger = CreatePassengerPrototype(
                "Waiting Passenger",
                pickup.Root.transform,
                new Vector3(0f, 1f, 0f),
                passengerMaterial);

            GameObject ridingAnchorObject = new(RidingAnchorName);
            Undo.RegisterCreatedObjectUndo(
                ridingAnchorObject,
                $"Create {RidingAnchorName}");
            ridingAnchorObject.transform.SetParent(taxiObject.transform, false);
            ridingAnchorObject.transform.localPosition =
                new Vector3(0f, 1.55f, -1.45f);

            GameObject ridingPassenger = CreatePassengerPrototype(
                "Riding Passenger",
                ridingAnchorObject.transform,
                Vector3.zero,
                passengerMaterial);

            ConfigureManager(
                manager,
                taxiVehicle,
                pickup.Root,
                dropoff.Root,
                waitingPassenger,
                ridingPassenger);

            ConfigureZone(pickup.Zone, manager, FishNetTaxiJobZoneType.Pickup);
            ConfigureZone(dropoff.Zone, manager, FishNetTaxiJobZoneType.Dropoff);
            ConfigureHud(hud, manager);

            pickup.Root.SetActive(true);
            waitingPassenger.SetActive(true);
            dropoff.Root.SetActive(false);
            ridingPassenger.SetActive(false);

            EditorUtility.SetDirty(networkObject);
            EditorUtility.SetDirty(manager);
            EditorUtility.SetDirty(hud);
            EditorUtility.SetDirty(taxiObject);

            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(activeScene);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeGameObject = jobRoot;

            Debug.Log(
                "Created the first synchronized taxi job. Save FishNetProof.unity.",
                jobRoot);
        }

        private static ZoneSetup CreateZone(
            Transform parent,
            string zoneName,
            Vector3 worldPosition,
            Material markerMaterial)
        {
            GameObject root = new(zoneName);
            Undo.RegisterCreatedObjectUndo(root, $"Create {zoneName}");
            root.transform.SetParent(parent, false);
            root.transform.position = worldPosition;

            BoxCollider trigger = Undo.AddComponent<BoxCollider>(root);
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 1f, 0f);
            trigger.size = new Vector3(5f, 2f, 5f);

            FishNetTaxiJobZone zone = Undo.AddComponent<FishNetTaxiJobZone>(root);

            GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Undo.RegisterCreatedObjectUndo(disc, $"Create {zoneName} Disc");
            disc.name = "Ground Marker";
            disc.transform.SetParent(root.transform, false);
            disc.transform.localPosition = new Vector3(0f, 0.06f, 0f);
            disc.transform.localScale = new Vector3(2.35f, 0.06f, 2.35f);
            RemovePrimitiveCollider(disc);
            ApplyMaterial(disc, markerMaterial);

            GameObject beacon = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(beacon, $"Create {zoneName} Beacon");
            beacon.name = "Vertical Beacon";
            beacon.transform.SetParent(root.transform, false);
            beacon.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            beacon.transform.localScale = new Vector3(0.12f, 3f, 0.12f);
            RemovePrimitiveCollider(beacon);
            ApplyMaterial(beacon, markerMaterial);

            return new ZoneSetup(root, zone);
        }

        private static GameObject CreatePassengerPrototype(
            string passengerName,
            Transform parent,
            Vector3 localPosition,
            Material material)
        {
            GameObject root = new(passengerName);
            Undo.RegisterCreatedObjectUndo(root, $"Create {passengerName}");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPosition;

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Undo.RegisterCreatedObjectUndo(body, $"Create {passengerName} Body");
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = new Vector3(0.55f, 0.85f, 0.55f);
            RemovePrimitiveCollider(body);
            ApplyMaterial(body, material);

            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Undo.RegisterCreatedObjectUndo(head, $"Create {passengerName} Head");
            head.name = "Head";
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.15f, 0f);
            head.transform.localScale = Vector3.one * 0.62f;
            RemovePrimitiveCollider(head);
            ApplyMaterial(head, material);

            return root;
        }

        private static void ConfigureManager(
            FishNetTaxiJobManager manager,
            FishNetVehicle taxiVehicle,
            GameObject pickupMarker,
            GameObject dropoffMarker,
            GameObject waitingPassenger,
            GameObject ridingPassenger)
        {
            SerializedObject serialized = new(manager);
            serialized.FindProperty("taxiVehicle").objectReferenceValue = taxiVehicle;
            serialized.FindProperty("pickupMarkerRoot").objectReferenceValue = pickupMarker;
            serialized.FindProperty("dropoffMarkerRoot").objectReferenceValue = dropoffMarker;
            serialized.FindProperty("waitingPassengerRoot").objectReferenceValue = waitingPassenger;
            serialized.FindProperty("ridingPassengerRoot").objectReferenceValue = ridingPassenger;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureZone(
            FishNetTaxiJobZone zone,
            FishNetTaxiJobManager manager,
            FishNetTaxiJobZoneType zoneType)
        {
            SerializedObject serialized = new(zone);
            serialized.FindProperty("jobManager").objectReferenceValue = manager;
            serialized.FindProperty("zoneType").enumValueIndex = (int)zoneType;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureHud(
            FishNetTaxiJobHud hud,
            FishNetTaxiJobManager manager)
        {
            SerializedObject serialized = new(hud);
            serialized.FindProperty("jobManager").objectReferenceValue = manager;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Material CreateOrUpdateMaterial(
            string path,
            Color color,
            float emissionMultiplier)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }

                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = color;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (emissionMultiplier > 0f && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * emissionMultiplier);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureMaterialFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/FishNetMigration"))
            {
                AssetDatabase.CreateFolder("Assets", "FishNetMigration");
            }

            if (!AssetDatabase.IsValidFolder(MaterialFolder))
            {
                AssetDatabase.CreateFolder("Assets/FishNetMigration", "TaxiPrototype");
            }
        }

        private static void RemovePrimitiveCollider(GameObject primitive)
        {
            Collider collider = primitive.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }
        }

        private static void ApplyMaterial(GameObject target, Material material)
        {
            Renderer renderer = target.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }
        }

        private readonly struct ZoneSetup
        {
            public ZoneSetup(GameObject root, FishNetTaxiJobZone zone)
            {
                Root = root;
                Zone = zone;
            }

            public GameObject Root { get; }
            public FishNetTaxiJobZone Zone { get; }
        }
    }
}
#endif
