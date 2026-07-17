#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace HillbillyTaxi.EditorTools
{
    public static class TruckVisualCleanupAndShadowFix
    {
        private const string OutputFolder =
            "Assets/Vehicles/Truck";

        [MenuItem(
            "Hillbilly Taxi/Truck/Apply Visual Cleanup and Shadow Fix")]
        public static void ApplyCleanupAndShadowFix()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning(
                    "Exit Play Mode before preparing the truck visual.");

                return;
            }

            GameObject selected =
                Selection.activeObject as GameObject;

            if (selected == null)
            {
                Debug.LogError(
                    "Select the prepared truck prefab or a truck scene root.");

                return;
            }

            GameObject workingInstance =
                CreateWorkingInstance(
                    selected);

            if (workingInstance == null)
            {
                Debug.LogError(
                    "Could not instantiate the selected truck.");

                return;
            }

            CleanupSummary summary =
                ApplyToHierarchy(
                    workingInstance);

            EnsureAssetFolder(
                OutputFolder);

            string outputPath =
                AssetDatabase.GenerateUniqueAssetPath(
                    $"{OutputFolder}/" +
                    $"{selected.name}_ShadowFixed.prefab");

            GameObject savedPrefab =
                PrefabUtility.SaveAsPrefabAsset(
                    workingInstance,
                    outputPath);

            UnityEngine.Object.DestroyImmediate(
                workingInstance);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string report =
                BuildReport(
                    savedPrefab,
                    summary,
                    outputPath);

            string reportPath =
                SaveReport(
                    report,
                    savedPrefab.name);

            EditorGUIUtility.systemCopyBuffer =
                report;

            Selection.activeObject =
                savedPrefab;

            EditorGUIUtility.PingObject(
                savedPrefab);

            Debug.Log(
                report +
                "\n\nSaved report: " +
                reportPath,
                savedPrefab);
        }

        [MenuItem(
            "Hillbilly Taxi/Truck/Validate Selected Truck Visual")]
        public static void ValidateSelectedTruckVisual()
        {
            GameObject selected =
                Selection.activeObject as GameObject;

            if (selected == null)
            {
                Debug.LogError(
                    "Select a prepared truck prefab or truck scene root.");

                return;
            }

            CleanupSummary summary =
                InspectHierarchy(
                    selected);

            string report =
                BuildReport(
                    selected,
                    summary,
                    AssetDatabase.GetAssetPath(
                        selected));

            string reportPath =
                SaveReport(
                    report,
                    selected.name);

            EditorGUIUtility.systemCopyBuffer =
                report;

            Debug.Log(
                report +
                "\n\nSaved report: " +
                reportPath,
                selected);
        }

        private static CleanupSummary ApplyToHierarchy(
            GameObject root)
        {
            CleanupSummary summary =
                new CleanupSummary();

            Transform[] transforms =
                root.GetComponentsInChildren<
                    Transform>(
                    includeInactive: true);

            HashSet<GameObject> fitRoots =
                new HashSet<GameObject>();

            foreach (Transform transform in transforms)
            {
                string objectName =
                    transform.name;

                if (objectName.StartsWith(
                        "FIT_",
                        StringComparison.Ordinal))
                {
                    fitRoots.Add(
                        transform.gameObject);

                    continue;
                }

                Renderer[] renderers =
                    transform.GetComponents<
                        Renderer>();

                foreach (Renderer renderer in renderers)
                {
                    summary.TotalRenderers++;

                    bool collisionProxy =
                        objectName.StartsWith(
                            "COL_",
                            StringComparison.Ordinal);

                    bool glass =
                        IsGlassRenderer(
                            renderer);

                    if (collisionProxy)
                    {
                        if (renderer.enabled)
                        {
                            summary.CollisionRenderersDisabled++;
                        }

                        renderer.enabled = false;
                        renderer.shadowCastingMode =
                            ShadowCastingMode.Off;

                        renderer.receiveShadows = false;
                        renderer.lightProbeUsage =
                            LightProbeUsage.Off;

                        renderer.reflectionProbeUsage =
                            ReflectionProbeUsage.Off;

                        continue;
                    }

                    if (glass)
                    {
                        renderer.enabled = true;
                        renderer.shadowCastingMode =
                            ShadowCastingMode.Off;

                        renderer.receiveShadows = false;
                        renderer.lightProbeUsage =
                            LightProbeUsage.BlendProbes;

                        renderer.reflectionProbeUsage =
                            ReflectionProbeUsage.BlendProbes;

                        summary.GlassRenderersConfigured++;
                        continue;
                    }

                    renderer.enabled = true;
                    renderer.shadowCastingMode =
                        ShadowCastingMode.On;

                    renderer.receiveShadows = true;
                    renderer.lightProbeUsage =
                        LightProbeUsage.BlendProbes;

                    renderer.reflectionProbeUsage =
                        ReflectionProbeUsage.BlendProbes;

                    renderer.allowOcclusionWhenDynamic =
                        true;

                    summary.OpaqueRenderersShadowEnabled++;
                }
            }

            foreach (GameObject fitRoot in fitRoots)
            {
                if (fitRoot.activeSelf)
                {
                    summary.FitObjectsDisabled++;
                }

                fitRoot.SetActive(false);
            }

            Camera[] cameras =
                root.GetComponentsInChildren<
                    Camera>(
                    includeInactive: true);

            foreach (Camera camera in cameras)
            {
                UnityEngine.Object.DestroyImmediate(
                    camera);

                summary.CameraComponentsRemoved++;
            }

            Light[] lights =
                root.GetComponentsInChildren<
                    Light>(
                    includeInactive: true);

            foreach (Light light in lights)
            {
                UnityEngine.Object.DestroyImmediate(
                    light);

                summary.LightComponentsRemoved++;
            }

            return summary;
        }

        private static CleanupSummary InspectHierarchy(
            GameObject root)
        {
            CleanupSummary summary =
                new CleanupSummary();

            Renderer[] renderers =
                root.GetComponentsInChildren<
                    Renderer>(
                    includeInactive: true);

            foreach (Renderer renderer in renderers)
            {
                summary.TotalRenderers++;

                bool collisionProxy =
                    renderer.name.StartsWith(
                        "COL_",
                        StringComparison.Ordinal);

                bool glass =
                    IsGlassRenderer(
                        renderer);

                if (collisionProxy &&
                    renderer.enabled)
                {
                    summary.CollisionRenderersStillEnabled++;
                }

                if (!collisionProxy &&
                    !glass &&
                    renderer.enabled &&
                    renderer.shadowCastingMode ==
                        ShadowCastingMode.On)
                {
                    summary.OpaqueRenderersShadowEnabled++;
                }

                if (!collisionProxy &&
                    !glass &&
                    renderer.enabled &&
                    renderer.shadowCastingMode !=
                        ShadowCastingMode.On)
                {
                    summary.OpaqueRenderersWithoutShadows++;
                }

                if (glass)
                {
                    summary.GlassRenderersConfigured++;
                }

                if (renderer.enabled &&
                    renderer.sharedMaterials.Any(
                        material =>
                            material != null &&
                            material.name.IndexOf(
                                "HT_Debug_Col",
                                StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    summary.EnabledDebugMaterialRenderers++;
                }
            }

            Transform[] transforms =
                root.GetComponentsInChildren<
                    Transform>(
                    includeInactive: true);

            summary.ActiveFitObjects =
                transforms.Count(
                    transform =>
                        transform.name.StartsWith(
                            "FIT_",
                            StringComparison.Ordinal) &&
                        transform.gameObject.activeSelf);

            summary.CameraComponentsRemaining =
                root.GetComponentsInChildren<
                    Camera>(
                    includeInactive: true).Length;

            summary.LightComponentsRemaining =
                root.GetComponentsInChildren<
                    Light>(
                    includeInactive: true).Length;

            return summary;
        }

        private static string BuildReport(
            GameObject root,
            CleanupSummary summary,
            string assetPath)
        {
            StringBuilder report =
                new StringBuilder(8192);

            report.AppendLine(
                "HILLBILLY TAXI — TRUCK VISUAL / SHADOW REPORT");

            report.AppendLine(
                new string('=', 58));

            report.AppendLine(
                $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            report.AppendLine(
                $"Root: {root.name}");

            report.AppendLine(
                $"Asset path: " +
                $"{(string.IsNullOrWhiteSpace(assetPath) ? "<scene object>" : assetPath)}");

            Bounds bounds =
                CalculateRendererBounds(
                    root);

            report.AppendLine(
                $"Visible renderer bounds size: " +
                $"({bounds.size.x:0.###}, " +
                $"{bounds.size.y:0.###}, " +
                $"{bounds.size.z:0.###})");

            report.AppendLine();

            report.AppendLine(
                "RENDERER STATE");

            report.AppendLine(
                new string('-', 58));

            report.AppendLine(
                $"Total Renderer components: " +
                $"{summary.TotalRenderers}");

            report.AppendLine(
                $"Opaque visible renderers casting shadows: " +
                $"{summary.OpaqueRenderersShadowEnabled}");

            report.AppendLine(
                $"Opaque visible renderers not casting shadows: " +
                $"{summary.OpaqueRenderersWithoutShadows}");

            report.AppendLine(
                $"Collision renderers disabled by cleanup: " +
                $"{summary.CollisionRenderersDisabled}");

            report.AppendLine(
                $"Collision renderers still enabled: " +
                $"{summary.CollisionRenderersStillEnabled}");

            report.AppendLine(
                $"Glass renderers configured: " +
                $"{summary.GlassRenderersConfigured}");

            report.AppendLine(
                $"Enabled renderers using HT_Debug_Col: " +
                $"{summary.EnabledDebugMaterialRenderers}");

            report.AppendLine(
                $"FIT_* objects disabled by cleanup: " +
                $"{summary.FitObjectsDisabled}");

            report.AppendLine(
                $"FIT_* objects still activeSelf: " +
                $"{summary.ActiveFitObjects}");

            report.AppendLine(
                $"Camera components removed by cleanup: " +
                $"{summary.CameraComponentsRemoved}");

            report.AppendLine(
                $"Camera components remaining: " +
                $"{summary.CameraComponentsRemaining}");

            report.AppendLine(
                $"Light components removed by cleanup: " +
                $"{summary.LightComponentsRemoved}");

            report.AppendLine(
                $"Light components remaining: " +
                $"{summary.LightComponentsRemaining}");

            report.AppendLine();

            report.AppendLine(
                "PROJECT SHADOW STATE");

            report.AppendLine(
                new string('-', 58));

            report.AppendLine(
                $"QualitySettings.shadows: " +
                $"{QualitySettings.shadows}");

            report.AppendLine(
                $"Current render pipeline: " +
                $"{(GraphicsSettings.currentRenderPipeline != null ? GraphicsSettings.currentRenderPipeline.name : "Built-in")}");

            Light[] sceneLights =
                UnityEngine.Object.FindObjectsByType<
                    Light>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            Light[] directionalLights =
                sceneLights
                    .Where(
                        light =>
                            light.type ==
                            LightType.Directional)
                    .ToArray();

            report.AppendLine(
                $"Scene Directional Lights: " +
                $"{directionalLights.Length}");

            foreach (Light light in directionalLights)
            {
                report.AppendLine(
                    $"  - {GetHierarchyPath(light.transform)} | " +
                    $"Enabled: {light.enabled} | " +
                    $"Active: {light.gameObject.activeInHierarchy} | " +
                    $"Shadows: {light.shadows} | " +
                    $"Intensity: {light.intensity:0.###}");
            }

            report.AppendLine();

            report.AppendLine(
                "VISIBLE RENDERERS WITHOUT SHADOWS");

            report.AppendLine(
                new string('-', 58));

            Renderer[] renderers =
                root.GetComponentsInChildren<
                    Renderer>(
                    includeInactive: true);

            List<Renderer> offenders =
                renderers
                    .Where(
                        renderer =>
                            renderer.enabled &&
                            renderer.gameObject.activeInHierarchy &&
                            !renderer.name.StartsWith(
                                "COL_",
                                StringComparison.Ordinal) &&
                            !IsGlassRenderer(renderer) &&
                            renderer.shadowCastingMode !=
                                ShadowCastingMode.On)
                    .ToList();

            if (offenders.Count == 0)
            {
                report.AppendLine(
                    "None.");
            }
            else
            {
                foreach (Renderer renderer in offenders)
                {
                    report.AppendLine(
                        $"  - {GetHierarchyPath(renderer.transform)} | " +
                        $"Mode: {renderer.shadowCastingMode}");
                }
            }

            report.AppendLine();

            report.AppendLine(
                "INTERPRETATION");

            report.AppendLine(
                new string('-', 58));

            if (summary.OpaqueRenderersWithoutShadows == 0 &&
                QualitySettings.shadows !=
                    ShadowQuality.Disable &&
                directionalLights.Any(
                    light =>
                        light.enabled &&
                        light.gameObject.activeInHierarchy &&
                        light.shadows != LightShadows.None))
            {
                report.AppendLine(
                    "The truck renderers and scene lighting are configured " +
                    "to cast shadows. If no shadow appears only in Scene View, " +
                    "enable the Scene Lighting toggle in the Scene toolbar.");
            }
            else
            {
                report.AppendLine(
                    "One or more renderer/light/project shadow settings are " +
                    "preventing shadows. Inspect the lines above.");
            }

            report.AppendLine(
                "Glass intentionally does not cast a solid shadow.");

            report.AppendLine(
                "COL_* renderers intentionally remain invisible while their " +
                "MeshFilter data is preserved.");

            return report.ToString();
        }

        private static bool IsGlassRenderer(
            Renderer renderer)
        {
            if (renderer.name.StartsWith(
                    "Glass_",
                    StringComparison.Ordinal))
            {
                return true;
            }

            return renderer.sharedMaterials.Any(
                material =>
                    material != null &&
                    material.name.IndexOf(
                        "Glass",
                        StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static GameObject CreateWorkingInstance(
            GameObject selected)
        {
            string assetPath =
                AssetDatabase.GetAssetPath(
                    selected);

            if (!string.IsNullOrWhiteSpace(assetPath))
            {
                UnityEngine.Object instance =
                    PrefabUtility.InstantiatePrefab(
                        selected);

                if (instance is GameObject prefabInstance)
                {
                    prefabInstance.hideFlags =
                        HideFlags.None;

                    return prefabInstance;
                }
            }

            GameObject clone =
                UnityEngine.Object.Instantiate(
                    selected);

            clone.name =
                selected.name;

            clone.hideFlags =
                HideFlags.None;

            return clone;
        }

        private static Bounds CalculateRendererBounds(
            GameObject root)
        {
            Renderer[] renderers =
                root.GetComponentsInChildren<
                    Renderer>(
                    includeInactive: false);

            bool initialized = false;
            Bounds bounds = new Bounds(
                root.transform.position,
                Vector3.zero);

            foreach (Renderer renderer in renderers)
            {
                if (!renderer.enabled ||
                    renderer.name.StartsWith(
                        "COL_",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (!initialized)
                {
                    bounds =
                        renderer.bounds;

                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(
                        renderer.bounds);
                }
            }

            return bounds;
        }

        private static string SaveReport(
            string report,
            string rootName)
        {
            const string reportsFolder =
                "Assets/TruckValidationReports";

            EnsureAssetFolder(
                reportsFolder);

            string safeName =
                string.Concat(
                    rootName.Select(
                        character =>
                            Path.GetInvalidFileNameChars()
                                .Contains(character)
                                ? '_'
                                : character));

            string assetPath =
                $"{reportsFolder}/" +
                $"{safeName}_VisualShadow_" +
                $"{DateTime.Now:yyyyMMdd_HHmmss}.txt";

            string projectRoot =
                Directory.GetParent(
                    Application.dataPath).FullName;

            File.WriteAllText(
                Path.Combine(
                    projectRoot,
                    assetPath),
                report,
                Encoding.UTF8);

            AssetDatabase.Refresh();

            return assetPath;
        }

        private static void EnsureAssetFolder(
            string fullPath)
        {
            string normalized =
                fullPath.Replace(
                    '\\',
                    '/');

            string[] parts =
                normalized.Split('/');

            string current =
                "Assets";

            for (int index = 1;
                 index < parts.Length;
                 index++)
            {
                string next =
                    $"{current}/{parts[index]}";

                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(
                        current,
                        parts[index]);
                }

                current = next;
            }
        }

        private static string GetHierarchyPath(
            Transform transform)
        {
            Stack<string> names =
                new Stack<string>();

            Transform current =
                transform;

            while (current != null)
            {
                names.Push(
                    current.name);

                current =
                    current.parent;
            }

            return string.Join(
                "/",
                names);
        }

        private sealed class CleanupSummary
        {
            public int TotalRenderers;
            public int OpaqueRenderersShadowEnabled;
            public int OpaqueRenderersWithoutShadows;
            public int CollisionRenderersDisabled;
            public int CollisionRenderersStillEnabled;
            public int GlassRenderersConfigured;
            public int EnabledDebugMaterialRenderers;
            public int FitObjectsDisabled;
            public int ActiveFitObjects;
            public int CameraComponentsRemoved;
            public int CameraComponentsRemaining;
            public int LightComponentsRemoved;
            public int LightComponentsRemaining;
        }
    }
}
#endif
