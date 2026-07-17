#if UNITY_EDITOR
using FishNet.Object;
using HillbillyTaxi.FishNetMigration.DynamicWorld;
using HillbillyTaxi.FishNetMigration.TaxiJobs;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HillbillyTaxi.EditorTools
{
    public static class FishNetPhaseNineWorldDirectorInstaller
    {
        private const string DirectorRootName =
            "FishNet Dynamic World Director";

        private const string JobRootName =
            "FishNet First Taxi Job";

        private const string PrototypeFolder =
            "Assets/FishNetMigration/WorldPrototype";

        [MenuItem(
            "Hillbilly Taxi/FishNet Migration/Phase 9B/" +
            "Install Dynamic World Director")]
        public static void Install()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning(
                    "Exit Play Mode before installing Phase 9B.");

                return;
            }

            GameObject jobRoot =
                GameObject.Find(JobRootName);

            if (jobRoot == null ||
                !jobRoot.TryGetComponent(
                    out FishNetTaxiJobManager
                        taxiJobManager))
            {
                Debug.LogError(
                    "Phase 9B requires the Phase 9A " +
                    "destination-led taxi contract.");

                return;
            }

            EnsurePrototypeFolder();

            Material barrierMaterial =
                CreateOrUpdateMaterial(
                    $"{PrototypeFolder}/Barrier.mat",
                    new Color(
                        0.95f,
                        0.42f,
                        0.04f,
                        1f),
                    emissionMultiplier: 0f);

            Material darkVehicleMaterial =
                CreateOrUpdateMaterial(
                    $"{PrototypeFolder}/DarkVehicle.mat",
                    new Color(
                        0.07f,
                        0.08f,
                        0.09f,
                        1f),
                    emissionMultiplier: 0f);

            Material damagedVehicleMaterial =
                CreateOrUpdateMaterial(
                    $"{PrototypeFolder}/DamagedVehicle.mat",
                    new Color(
                        0.32f,
                        0.18f,
                        0.1f,
                        1f),
                    emissionMultiplier: 0f);

            Material policeWhiteMaterial =
                CreateOrUpdateMaterial(
                    $"{PrototypeFolder}/PoliceWhite.mat",
                    new Color(
                        0.75f,
                        0.77f,
                        0.79f,
                        1f),
                    emissionMultiplier: 0f);

            Material redLightMaterial =
                CreateOrUpdateMaterial(
                    $"{PrototypeFolder}/PoliceRed.mat",
                    new Color(
                        1f,
                        0.03f,
                        0.02f,
                        1f),
                    emissionMultiplier: 2.4f);

            Material blueLightMaterial =
                CreateOrUpdateMaterial(
                    $"{PrototypeFolder}/PoliceBlue.mat",
                    new Color(
                        0.03f,
                        0.2f,
                        1f,
                        1f),
                    emissionMultiplier: 2.4f);

            GameObject directorRoot =
                FindOrCreateSceneRoot(
                    DirectorRootName);

            NetworkObject networkObject =
                GetOrAddComponent<
                    NetworkObject>(
                    directorRoot);

            FishNetWorldDirector director =
                GetOrAddComponent<
                    FishNetWorldDirector>(
                    directorRoot);

            FishNetWorldDirectorHud hud =
                GetOrAddComponent<
                    FishNetWorldDirectorHud>(
                    directorRoot);

            CreateSouthCrossingClosure(
                directorRoot.transform,
                barrierMaterial,
                redLightMaterial);

            CreateTownPoliceCheckpoint(
                directorRoot.transform,
                barrierMaterial,
                policeWhiteMaterial,
                darkVehicleMaterial,
                redLightMaterial,
                blueLightMaterial);

            CreateRoadsideAccident(
                directorRoot.transform,
                damagedVehicleMaterial,
                barrierMaterial,
                redLightMaterial);

            CreateGarageSearchPerimeter(
                directorRoot.transform,
                darkVehicleMaterial,
                redLightMaterial,
                blueLightMaterial);

            ConfigureDirector(
                director,
                taxiJobManager);

            ConfigureHud(
                hud,
                director,
                taxiJobManager);

            SetAllSituationPresentationsInactive(
                directorRoot.transform);

            EditorUtility.SetDirty(
                directorRoot);

            EditorUtility.SetDirty(
                networkObject);

            EditorUtility.SetDirty(
                director);

            EditorUtility.SetDirty(
                hud);

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
                directorRoot;

            Debug.Log(
                "Installed Phase 9B Dynamic World Director. " +
                "At contract start the server commits to one persistent " +
                "route situation. A separate search perimeter activates " +
                "while Earl is inside Miller's Garage. Save " +
                "FishNetProof.unity.",
                directorRoot);
        }

        private static void
            CreateSouthCrossingClosure(
                Transform directorRoot,
                Material barrierMaterial,
                Material redLightMaterial)
        {
            SituationSetup setup =
                EnsureSituation(
                    directorRoot,
                    "Situation - South Crossing Closed",
                    "south_crossing_closed",
                    "South Crossing Closed",
                    "A barricaded crossing remains closed for " +
                    "the whole contract.",
                    FishNetWorldSituationPhase
                        .ContractStart,
                    "route_primary",
                    weight: 1f,
                    new Vector3(-8f, 0f, 12f));

            Transform presentation =
                setup.Presentation.transform;

            EnsurePrimitive(
                presentation,
                "Barricade Left",
                PrimitiveType.Cube,
                new Vector3(-2.4f, 0.55f, 0f),
                new Vector3(3.6f, 1.1f, 0.45f),
                Quaternion.identity,
                barrierMaterial,
                keepCollider: true);

            EnsurePrimitive(
                presentation,
                "Barricade Right",
                PrimitiveType.Cube,
                new Vector3(2.4f, 0.55f, 0f),
                new Vector3(3.6f, 1.1f, 0.45f),
                Quaternion.identity,
                barrierMaterial,
                keepCollider: true);

            EnsureWarningPost(
                presentation,
                "Warning Light Left",
                new Vector3(-4.2f, 0f, 0f),
                redLightMaterial);

            EnsureWarningPost(
                presentation,
                "Warning Light Right",
                new Vector3(4.2f, 0f, 0f),
                redLightMaterial);
        }

        private static void
            CreateTownPoliceCheckpoint(
                Transform directorRoot,
                Material barrierMaterial,
                Material policeWhiteMaterial,
                Material darkVehicleMaterial,
                Material redLightMaterial,
                Material blueLightMaterial)
        {
            SituationSetup setup =
                EnsureSituation(
                    directorRoot,
                    "Situation - Town Police Checkpoint",
                    "town_police_checkpoint",
                    "Town Police Checkpoint",
                    "Police occupy one fixed junction and do not " +
                    "relocate when players choose another route.",
                    FishNetWorldSituationPhase
                        .ContractStart,
                    "route_primary",
                    weight: 1f,
                    new Vector3(7f, 0f, 17f));

            Transform presentation =
                setup.Presentation.transform;

            EnsureVehicle(
                presentation,
                "Police Car A",
                new Vector3(-3.5f, 0f, 1.5f),
                Quaternion.Euler(0f, 25f, 0f),
                policeWhiteMaterial,
                darkVehicleMaterial,
                redLightMaterial,
                blueLightMaterial);

            EnsureVehicle(
                presentation,
                "Police Car B",
                new Vector3(3.5f, 0f, -1.2f),
                Quaternion.Euler(0f, -20f, 0f),
                policeWhiteMaterial,
                darkVehicleMaterial,
                blueLightMaterial,
                redLightMaterial);

            EnsurePrimitive(
                presentation,
                "Checkpoint Barrier",
                PrimitiveType.Cube,
                new Vector3(0f, 0.45f, 0f),
                new Vector3(3.2f, 0.9f, 0.35f),
                Quaternion.identity,
                barrierMaterial,
                keepCollider: true);
        }

        private static void
            CreateRoadsideAccident(
                Transform directorRoot,
                Material damagedVehicleMaterial,
                Material barrierMaterial,
                Material redLightMaterial)
        {
            SituationSetup setup =
                EnsureSituation(
                    directorRoot,
                    "Situation - Roadside Accident",
                    "roadside_accident",
                    "Roadside Accident",
                    "A wreck partially blocks one road and remains " +
                    "where it happened.",
                    FishNetWorldSituationPhase
                        .ContractStart,
                    "route_primary",
                    weight: 1f,
                    new Vector3(12f, 0f, 4f));

            Transform presentation =
                setup.Presentation.transform;

            EnsurePrimitive(
                presentation,
                "Wrecked Vehicle",
                PrimitiveType.Cube,
                new Vector3(0f, 0.7f, 0f),
                new Vector3(4.2f, 1.4f, 2.1f),
                Quaternion.Euler(
                    0f,
                    37f,
                    8f),
                damagedVehicleMaterial,
                keepCollider: true);

            EnsurePrimitive(
                presentation,
                "Road Debris A",
                PrimitiveType.Cube,
                new Vector3(-2.4f, 0.25f, 1.7f),
                new Vector3(1.2f, 0.5f, 0.7f),
                Quaternion.Euler(
                    0f,
                    18f,
                    12f),
                barrierMaterial,
                keepCollider: true);

            EnsurePrimitive(
                presentation,
                "Road Debris B",
                PrimitiveType.Cube,
                new Vector3(2.6f, 0.2f, -1.5f),
                new Vector3(0.8f, 0.4f, 1.1f),
                Quaternion.Euler(
                    0f,
                    -25f,
                    7f),
                barrierMaterial,
                keepCollider: true);

            EnsureWarningPost(
                presentation,
                "Accident Warning Light",
                new Vector3(-4f, 0f, -2f),
                redLightMaterial);
        }

        private static void
            CreateGarageSearchPerimeter(
                Transform directorRoot,
                Material darkVehicleMaterial,
                Material redLightMaterial,
                Material blueLightMaterial)
        {
            SituationSetup setup =
                EnsureSituation(
                    directorRoot,
                    "Situation - Miller Search Perimeter",
                    "miller_search_perimeter",
                    "Search Vehicles Closing In",
                    "Placeholder government vehicles take fixed " +
                    "positions around Miller's Garage while Earl is inside.",
                    FishNetWorldSituationPhase
                        .RequiredStopWait,
                    string.Empty,
                    weight: 1f,
                    new Vector3(20f, 0f, 14f));

            Transform presentation =
                setup.Presentation.transform;

            EnsureVehicle(
                presentation,
                "Search Vehicle North",
                new Vector3(0f, 0f, 8f),
                Quaternion.Euler(0f, 180f, 0f),
                darkVehicleMaterial,
                darkVehicleMaterial,
                redLightMaterial,
                blueLightMaterial);

            EnsureVehicle(
                presentation,
                "Search Vehicle West",
                new Vector3(-8f, 0f, -2f),
                Quaternion.Euler(0f, 90f, 0f),
                darkVehicleMaterial,
                darkVehicleMaterial,
                blueLightMaterial,
                redLightMaterial);

            EnsureVehicle(
                presentation,
                "Search Vehicle East",
                new Vector3(8f, 0f, -2f),
                Quaternion.Euler(0f, -90f, 0f),
                darkVehicleMaterial,
                darkVehicleMaterial,
                redLightMaterial,
                blueLightMaterial);
        }

        private static SituationSetup EnsureSituation(
            Transform parent,
            string objectName,
            string situationId,
            string displayName,
            string description,
            FishNetWorldSituationPhase phase,
            string selectionGroup,
            float weight,
            Vector3 worldPosition)
        {
            Transform existing =
                parent.Find(objectName);

            GameObject root;

            if (existing == null)
            {
                root =
                    new GameObject(objectName);

                Undo.RegisterCreatedObjectUndo(
                    root,
                    $"Create {objectName}");

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
                worldPosition;

            Transform presentationTransform =
                root.transform.Find(
                    "Presentation");

            GameObject presentation;

            if (presentationTransform == null)
            {
                presentation =
                    new GameObject(
                        "Presentation");

                Undo.RegisterCreatedObjectUndo(
                    presentation,
                    "Create Situation Presentation");

                presentation.transform.SetParent(
                    root.transform,
                    false);
            }
            else
            {
                presentation =
                    presentationTransform.gameObject;
            }

            FishNetWorldSituationAnchor anchor =
                GetOrAddComponent<
                    FishNetWorldSituationAnchor>(
                    root);

            SerializedObject serialized =
                new(anchor);

            serialized.FindProperty("situationId")
                .stringValue =
                situationId;

            serialized.FindProperty("displayName")
                .stringValue =
                displayName;

            serialized.FindProperty("debugDescription")
                .stringValue =
                description;

            serialized.FindProperty("activationPhase")
                .enumValueIndex =
                (int)phase;

            serialized.FindProperty("selectionGroup")
                .stringValue =
                selectionGroup;

            serialized.FindProperty("selectionWeight")
                .floatValue =
                weight;

            serialized.FindProperty("presentationRoot")
                .objectReferenceValue =
                presentation;

            serialized
                .ApplyModifiedPropertiesWithoutUndo();

            return new SituationSetup(
                root,
                presentation,
                anchor);
        }

        private static void EnsureVehicle(
            Transform parent,
            string vehicleName,
            Vector3 localPosition,
            Quaternion localRotation,
            Material bodyMaterial,
            Material lowerMaterial,
            Material leftLightMaterial,
            Material rightLightMaterial)
        {
            Transform existing =
                parent.Find(vehicleName);

            Transform vehicleRoot;

            if (existing == null)
            {
                vehicleRoot =
                    new GameObject(
                        vehicleName).transform;

                Undo.RegisterCreatedObjectUndo(
                    vehicleRoot.gameObject,
                    $"Create {vehicleName}");

                vehicleRoot.SetParent(
                    parent,
                    false);
            }
            else
            {
                vehicleRoot = existing;
            }

            vehicleRoot.localPosition =
                localPosition;

            vehicleRoot.localRotation =
                localRotation;

            EnsurePrimitive(
                vehicleRoot,
                "Body",
                PrimitiveType.Cube,
                new Vector3(0f, 0.75f, 0f),
                new Vector3(2.2f, 1.1f, 4.2f),
                Quaternion.identity,
                bodyMaterial,
                keepCollider: true);

            EnsurePrimitive(
                vehicleRoot,
                "Lower Body",
                PrimitiveType.Cube,
                new Vector3(0f, 0.35f, 0f),
                new Vector3(2.35f, 0.5f, 4.35f),
                Quaternion.identity,
                lowerMaterial,
                keepCollider: false);

            EnsurePrimitive(
                vehicleRoot,
                "Light Left",
                PrimitiveType.Cube,
                new Vector3(-0.45f, 1.42f, 0f),
                new Vector3(0.65f, 0.18f, 0.3f),
                Quaternion.identity,
                leftLightMaterial,
                keepCollider: false);

            EnsurePrimitive(
                vehicleRoot,
                "Light Right",
                PrimitiveType.Cube,
                new Vector3(0.45f, 1.42f, 0f),
                new Vector3(0.65f, 0.18f, 0.3f),
                Quaternion.identity,
                rightLightMaterial,
                keepCollider: false);
        }

        private static void EnsureWarningPost(
            Transform parent,
            string objectName,
            Vector3 localPosition,
            Material lightMaterial)
        {
            Transform existing =
                parent.Find(objectName);

            Transform root;

            if (existing == null)
            {
                root =
                    new GameObject(
                        objectName).transform;

                Undo.RegisterCreatedObjectUndo(
                    root.gameObject,
                    $"Create {objectName}");

                root.SetParent(
                    parent,
                    false);
            }
            else
            {
                root = existing;
            }

            root.localPosition =
                localPosition;

            EnsurePrimitive(
                root,
                "Post",
                PrimitiveType.Cylinder,
                new Vector3(0f, 0.7f, 0f),
                new Vector3(0.15f, 0.7f, 0.15f),
                Quaternion.identity,
                lightMaterial,
                keepCollider: false);

            EnsurePrimitive(
                root,
                "Light",
                PrimitiveType.Sphere,
                new Vector3(0f, 1.5f, 0f),
                new Vector3(0.35f, 0.35f, 0.35f),
                Quaternion.identity,
                lightMaterial,
                keepCollider: false);
        }

        private static void EnsurePrimitive(
            Transform parent,
            string objectName,
            PrimitiveType primitiveType,
            Vector3 localPosition,
            Vector3 localScale,
            Quaternion localRotation,
            Material material,
            bool keepCollider)
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
                localRotation;

            target.transform.localScale =
                localScale;

            Renderer renderer =
                target.GetComponent<Renderer>();

            if (renderer != null)
            {
                renderer.sharedMaterial =
                    material;
            }

            Collider collider =
                target.GetComponent<Collider>();

            if (!keepCollider &&
                collider != null)
            {
                Object.DestroyImmediate(
                    collider);
            }
        }

        private static void ConfigureDirector(
            FishNetWorldDirector director,
            FishNetTaxiJobManager taxiJobManager)
        {
            SerializedObject serialized =
                new(director);

            serialized.FindProperty("taxiJobManager")
                .objectReferenceValue =
                taxiJobManager;

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureHud(
            FishNetWorldDirectorHud hud,
            FishNetWorldDirector director,
            FishNetTaxiJobManager taxiJobManager)
        {
            SerializedObject serialized =
                new(hud);

            serialized.FindProperty("worldDirector")
                .objectReferenceValue =
                director;

            serialized.FindProperty("taxiJobManager")
                .objectReferenceValue =
                taxiJobManager;

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void
            SetAllSituationPresentationsInactive(
                Transform directorRoot)
        {
            FishNetWorldSituationAnchor[] anchors =
                directorRoot.GetComponentsInChildren<
                    FishNetWorldSituationAnchor>(
                    includeInactive: true);

            foreach (
                FishNetWorldSituationAnchor anchor
                in anchors)
            {
                SerializedObject serialized =
                    new(anchor);

                GameObject presentation =
                    serialized.FindProperty(
                            "presentationRoot")
                        .objectReferenceValue
                    as GameObject;

                if (presentation != null)
                {
                    presentation.SetActive(false);
                    EditorUtility.SetDirty(
                        presentation);
                }
            }
        }

        private static GameObject FindOrCreateSceneRoot(
            string objectName)
        {
            GameObject existing =
                GameObject.Find(objectName);

            if (existing != null)
            {
                existing.SetActive(true);
                return existing;
            }

            GameObject created =
                new(objectName);

            Undo.RegisterCreatedObjectUndo(
                created,
                $"Create {objectName}");

            return created;
        }

        private static T GetOrAddComponent<T>(
            GameObject target)
            where T : Component
        {
            T component =
                target.GetComponent<T>();

            if (component == null)
            {
                component =
                    Undo.AddComponent<T>(
                        target);
            }

            return component;
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
                    "WorldPrototype");
            }
        }

        private readonly struct SituationSetup
        {
            public SituationSetup(
                GameObject root,
                GameObject presentation,
                FishNetWorldSituationAnchor anchor)
            {
                Root = root;
                Presentation = presentation;
                Anchor = anchor;
            }

            public GameObject Root { get; }
            public GameObject Presentation { get; }
            public FishNetWorldSituationAnchor Anchor { get; }
        }
    }
}
#endif
