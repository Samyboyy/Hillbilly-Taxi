#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HillbillyTaxi.EditorTools
{
    internal static class TruckRuntimeVisualBuilder
    {
        private const string OutputRootFolder =
            "Assets/Vehicles/Truck";

        private const string OutputMaterialFolder =
            OutputRootFolder + "/Materials";

        public static void Build(GameObject sourceRoot)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning(
                    "Exit Play Mode before preparing the truck.");
                return;
            }

            TruckValidationResult validation =
                TruckImportValidation.Build(sourceRoot);

            string validationPath =
                TruckImportValidation.SaveReport(
                    validation.Report,
                    sourceRoot.name);

            if (!validation.SafeToPrepare)
            {
                EditorGUIUtility.systemCopyBuffer = validation.Report;

                Debug.LogError(
                    validation.Report +
                    "\n\nPreparation stopped because the visual doors, " +
                    "glass hierarchy, or collision meshes failed validation. " +
                    "Saved report: " + validationPath,
                    sourceRoot);

                EditorUtility.DisplayDialog(
                    "Truck preparation stopped",
                    "The selected truck did not pass the required Door_*, " +
                    "Glass_* and COL_Door_* checks. The report was saved " +
                    "and copied to the clipboard.",
                    "OK");
                return;
            }

            string sourceAssetPath =
                AssetDatabase.GetAssetPath(sourceRoot);

            if (!string.IsNullOrWhiteSpace(sourceAssetPath))
            {
                ConfigureModelImporter(sourceAssetPath);

                sourceRoot =
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        sourceAssetPath);

                if (sourceRoot == null)
                {
                    Debug.LogError(
                        "The truck could not be reloaded after changing " +
                        "the model importer.");
                    return;
                }
            }

            TruckImportValidation.EnsureAssetFolder(
                OutputRootFolder);
            TruckImportValidation.EnsureAssetFolder(
                OutputMaterialFolder);

            Material glassMaterial = CreateOrUpdateGlassMaterial();
            GameObject instance = InstantiateSource(sourceRoot);

            if (instance == null)
            {
                Debug.LogError(
                    "Could not instantiate the selected truck.",
                    sourceRoot);
                return;
            }

            instance.name = sourceRoot.name + "_RuntimeVisual";

            int collisionRenderersDisabled = 0;
            int fitObjectsDisabled = 0;
            int glassRenderersConfigured = 0;
            int camerasRemoved = 0;
            int lightsRemoved = 0;

            Transform[] transforms =
                instance.GetComponentsInChildren<Transform>(true);

            foreach (Transform transform in transforms)
            {
                string objectName = transform.name;

                if (objectName.StartsWith(
                        "COL_",
                        System.StringComparison.Ordinal))
                {
                    Renderer[] renderers =
                        transform.GetComponents<Renderer>();

                    foreach (Renderer renderer in renderers)
                    {
                        if (renderer.enabled)
                        {
                            collisionRenderersDisabled++;
                        }

                        renderer.enabled = false;
                        renderer.shadowCastingMode = ShadowCastingMode.Off;
                        renderer.receiveShadows = false;
                        renderer.allowOcclusionWhenDynamic = false;
                    }

                    continue;
                }

                if (objectName.StartsWith(
                        "FIT_",
                        System.StringComparison.Ordinal))
                {
                    transform.gameObject.SetActive(false);
                    fitObjectsDisabled++;
                    continue;
                }

                if (objectName.StartsWith(
                        "Glass_",
                        System.StringComparison.Ordinal))
                {
                    Renderer renderer = transform.GetComponent<Renderer>();

                    if (renderer != null)
                    {
                        Material[] materials = renderer.sharedMaterials;

                        if (materials.Length == 0)
                        {
                            materials = new[] { glassMaterial };
                        }
                        else
                        {
                            for (int index = 0;
                                 index < materials.Length;
                                 index++)
                            {
                                materials[index] = glassMaterial;
                            }
                        }

                        renderer.sharedMaterials = materials;
                        renderer.enabled = true;
                        renderer.shadowCastingMode = ShadowCastingMode.Off;
                        renderer.receiveShadows = false;
                        glassRenderersConfigured++;
                    }
                }
            }

            Camera[] cameras =
                instance.GetComponentsInChildren<Camera>(true);

            foreach (Camera camera in cameras)
            {
                Object.DestroyImmediate(camera);
                camerasRemoved++;
            }

            Light[] lights =
                instance.GetComponentsInChildren<Light>(true);

            foreach (Light light in lights)
            {
                Object.DestroyImmediate(light);
                lightsRemoved++;
            }

            string prefabPath =
                AssetDatabase.GenerateUniqueAssetPath(
                    OutputRootFolder + "/" +
                    sourceRoot.name +
                    "_RuntimeVisual.prefab");

            GameObject prefab =
                PrefabUtility.SaveAsPrefabAsset(
                    instance,
                    prefabPath);

            Object.DestroyImmediate(instance);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            TruckValidationResult preparedValidation =
                TruckImportValidation.Build(prefab);

            string preparedReportPath =
                TruckImportValidation.SaveReport(
                    preparedValidation.Report,
                    prefab.name);

            EditorGUIUtility.systemCopyBuffer =
                preparedValidation.Report;

            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);

            Debug.Log(
                "TRUCK RUNTIME VISUAL PREFAB CREATED\n" +
                new string('=', 48) + "\n" +
                $"Prefab: {prefabPath}\n" +
                "COL_* renderers disabled: " +
                collisionRenderersDisabled + "\n" +
                "FIT_* objects disabled: " +
                fitObjectsDisabled + "\n" +
                "Glass_* renderers configured: " +
                glassRenderersConfigured + "\n" +
                "Camera components removed: " +
                camerasRemoved + "\n" +
                "Light components removed: " +
                lightsRemoved + "\n" +
                "Original FBX modified: NO\n" +
                "Prepared validation report: " +
                preparedReportPath + "\n\n" +
                "Door_* renderers and their material slots were preserved. " +
                "COL_* MeshFilters and mesh data were retained.",
                prefab);
        }

        private static void ConfigureModelImporter(string assetPath)
        {
            ModelImporter importer =
                AssetImporter.GetAtPath(assetPath) as ModelImporter;

            if (importer == null)
            {
                return;
            }

            bool changed = false;

            if (importer.importCameras)
            {
                importer.importCameras = false;
                changed = true;
            }

            if (importer.importLights)
            {
                importer.importLights = false;
                changed = true;
            }

            if (changed)
            {
                importer.SaveAndReimport();
            }
        }

        private static GameObject InstantiateSource(GameObject sourceRoot)
        {
            string assetPath = AssetDatabase.GetAssetPath(sourceRoot);

            if (!string.IsNullOrWhiteSpace(assetPath))
            {
                Object created = PrefabUtility.InstantiatePrefab(sourceRoot);

                if (created is GameObject prefabInstance)
                {
                    prefabInstance.hideFlags = HideFlags.None;
                    return prefabInstance;
                }
            }

            GameObject clone = Object.Instantiate(sourceRoot);
            clone.hideFlags = HideFlags.None;
            return clone;
        }

        private static Material CreateOrUpdateGlassMaterial()
        {
            const string glassPath =
                OutputMaterialFolder +
                "/HT_Glass_Dark_URP.mat";

            Material material =
                AssetDatabase.LoadAssetAtPath<Material>(glassPath);

            Shader shader = Shader.Find(
                "Universal Render Pipeline/Lit");

            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, glassPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            Color glassColor =
                new Color(0.055f, 0.095f, 0.12f, 0.34f);

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", glassColor);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", glassColor);
            }

            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
            }

            if (material.HasProperty("_Blend"))
            {
                material.SetFloat("_Blend", 0f);
            }

            if (material.HasProperty("_SrcBlend"))
            {
                material.SetFloat(
                    "_SrcBlend",
                    (float)BlendMode.SrcAlpha);
            }

            if (material.HasProperty("_DstBlend"))
            {
                material.SetFloat(
                    "_DstBlend",
                    (float)BlendMode.OneMinusSrcAlpha);
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetFloat("_ZWrite", 0f);
            }

            if (material.HasProperty("_Cull"))
            {
                material.SetFloat(
                    "_Cull",
                    (float)CullMode.Off);
            }

            material.SetOverrideTag(
                "RenderType",
                "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_SURFACE_TYPE_OPAQUE");
            material.doubleSidedGI = true;

            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
#endif
