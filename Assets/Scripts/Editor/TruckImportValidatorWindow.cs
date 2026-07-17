#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HillbillyTaxi.EditorTools
{
    public sealed class TruckImportValidatorWindow : EditorWindow
    {
        private UnityEngine.Object _truckSelection;
        private Vector2 _scroll;
        private string _lastReport = string.Empty;
        private string _lastReportAssetPath = string.Empty;

        [MenuItem("Hillbilly Taxi/Truck/Import Validator and Preparer")]
        public static void OpenWindow()
        {
            TruckImportValidatorWindow window =
                GetWindow<TruckImportValidatorWindow>("Truck Import");

            window.minSize = new Vector2(760f, 560f);
            window.TryAdoptCurrentSelection();
            window.Show();
        }

        [MenuItem("Hillbilly Taxi/Truck/Validate Selected Truck (Read Only)")]
        public static void ValidateSelectionMenu()
        {
            GameObject root = ResolveSelectedTruckRoot(
                Selection.activeObject,
                out string error);

            if (root == null)
            {
                Debug.LogError(error);
                return;
            }

            TruckValidationResult result =
                TruckImportValidation.Build(root);

            string reportPath =
                TruckImportValidation.SaveReport(
                    result.Report,
                    root.name);

            EditorGUIUtility.systemCopyBuffer = result.Report;

            Debug.Log(
                result.Report +
                "\n\nSaved report: " + reportPath +
                "\nThe report was also copied to the clipboard.",
                root);
        }

        [MenuItem("Hillbilly Taxi/Truck/Create Safe Runtime Visual Prefab")]
        public static void PrepareSelectionMenu()
        {
            GameObject sourceRoot = ResolveSelectedTruckRoot(
                Selection.activeObject,
                out string error);

            if (sourceRoot == null)
            {
                Debug.LogError(error);
                return;
            }

            TruckRuntimeVisualBuilder.Build(sourceRoot);
        }

        private void OnEnable()
        {
            TryAdoptCurrentSelection();
        }

        private void OnSelectionChange()
        {
            if (Selection.activeObject is GameObject)
            {
                _truckSelection = Selection.activeObject;
                Repaint();
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(
                "Hillbilly Taxi Truck Import",
                EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "Select Truck.fbx, a truck prefab, or a truck scene root. " +
                "Validation is read-only. Preparation creates a separate " +
                "runtime visual prefab and never rewrites the original FBX.",
                MessageType.Info);

            _truckSelection = EditorGUILayout.ObjectField(
                "Truck",
                _truckSelection,
                typeof(GameObject),
                allowSceneObjects: true);

            EditorGUILayout.Space(6f);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(
                        "1. Validate Read Only",
                        GUILayout.Height(34f)))
                {
                    ValidateCurrentSelection();
                }

                GUI.enabled = !string.IsNullOrWhiteSpace(_lastReport);

                if (GUILayout.Button(
                        "Copy Last Report",
                        GUILayout.Height(34f)))
                {
                    EditorGUIUtility.systemCopyBuffer = _lastReport;
                }

                GUI.enabled = !string.IsNullOrWhiteSpace(_lastReportAssetPath);

                if (GUILayout.Button(
                        "Select Saved Report",
                        GUILayout.Height(34f)))
                {
                    UnityEngine.Object reportAsset =
                        AssetDatabase.LoadAssetAtPath<TextAsset>(
                            _lastReportAssetPath);

                    Selection.activeObject = reportAsset;
                }

                GUI.enabled = true;
            }

            EditorGUILayout.Space(4f);

            if (GUILayout.Button(
                    "2. Create Safe Runtime Visual Prefab",
                    GUILayout.Height(38f)))
            {
                GameObject root = ResolveSelectedTruckRoot(
                    _truckSelection,
                    out string error);

                if (root == null)
                {
                    EditorUtility.DisplayDialog(
                        "Truck preparation",
                        error,
                        "OK");

                    return;
                }

                TruckRuntimeVisualBuilder.Build(root);
            }

            EditorGUILayout.HelpBox(
                "Safe preparation disables COL_* renderers, disables FIT_* " +
                "objects, turns off imported FBX cameras/lights, preserves " +
                "Door_* and Glass_* renderers, creates transparent URP glass, " +
                "and saves a separate prefab.",
                MessageType.None);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(
                "Last validation report",
                EditorStyles.boldLabel);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.TextArea(
                string.IsNullOrWhiteSpace(_lastReport)
                    ? "No report generated yet."
                    : _lastReport,
                GUILayout.ExpandHeight(true));

            EditorGUILayout.EndScrollView();
        }

        private void TryAdoptCurrentSelection()
        {
            if (Selection.activeObject is GameObject)
            {
                _truckSelection = Selection.activeObject;
            }
        }

        private void ValidateCurrentSelection()
        {
            GameObject root = ResolveSelectedTruckRoot(
                _truckSelection,
                out string error);

            if (root == null)
            {
                EditorUtility.DisplayDialog(
                    "Truck validation",
                    error,
                    "OK");

                return;
            }

            TruckValidationResult result =
                TruckImportValidation.Build(root);

            _lastReport = result.Report;
            _lastReportAssetPath =
                TruckImportValidation.SaveReport(
                    result.Report,
                    root.name);

            EditorGUIUtility.systemCopyBuffer = result.Report;

            Debug.Log(
                result.Report +
                "\n\nSaved report: " + _lastReportAssetPath,
                root);
        }

        internal static GameObject ResolveSelectedTruckRoot(
            UnityEngine.Object selected,
            out string error)
        {
            error = string.Empty;

            if (!(selected is GameObject selectedObject))
            {
                error =
                    "Select Truck.fbx, a truck prefab, or the truck root " +
                    "in the scene first.";

                return null;
            }

            string assetPath = AssetDatabase.GetAssetPath(selectedObject);

            if (!string.IsNullOrWhiteSpace(assetPath))
            {
                GameObject assetRoot =
                    AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);

                if (assetRoot == null)
                {
                    error = $"Could not load a GameObject from '{assetPath}'.";
                    return null;
                }

                return assetRoot;
            }

            Transform current = selectedObject.transform;
            Transform namedTruckRoot = null;

            while (current != null)
            {
                if (string.Equals(
                        current.name,
                        "TRUCK_ROOT",
                        StringComparison.Ordinal))
                {
                    namedTruckRoot = current;
                    break;
                }

                current = current.parent;
            }

            if (namedTruckRoot != null)
            {
                return namedTruckRoot.gameObject;
            }

            current = selectedObject.transform;

            while (current.parent != null)
            {
                current = current.parent;
            }

            return current.gameObject;
        }
    }

    internal sealed class TruckValidationResult
    {
        public TruckValidationResult(
            string report,
            bool visualDoorsHealthy,
            bool glassHealthy,
            bool collisionMeshesHealthy)
        {
            Report = report;
            VisualDoorsHealthy = visualDoorsHealthy;
            GlassHealthy = glassHealthy;
            CollisionMeshesHealthy = collisionMeshesHealthy;
        }

        public string Report { get; }
        public bool VisualDoorsHealthy { get; }
        public bool GlassHealthy { get; }
        public bool CollisionMeshesHealthy { get; }

        public bool SafeToPrepare =>
            VisualDoorsHealthy &&
            GlassHealthy &&
            CollisionMeshesHealthy;
    }

    internal static class TruckImportValidation
    {
        private static readonly string[] DoorSuffixes =
        {
            "FL",
            "FR",
            "RL",
            "RR"
        };

        public static TruckValidationResult Build(GameObject truckRoot)
        {
            StringBuilder report = new StringBuilder(16384);

            Transform[] transforms =
                truckRoot.GetComponentsInChildren<Transform>(true);

            Dictionary<string, List<Transform>> byName =
                BuildNameLookup(transforms);

            string assetPath = AssetDatabase.GetAssetPath(truckRoot);

            report.AppendLine("HILLBILLY TAXI — TRUCK IMPORT VALIDATION");
            report.AppendLine(new string('=', 58));
            report.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine($"Selected root: {truckRoot.name}");
            report.AppendLine(
                "Asset path: " +
                (string.IsNullOrWhiteSpace(assetPath)
                    ? "<scene object>"
                    : assetPath));
            report.AppendLine($"Objects inspected: {transforms.Length}");
            report.AppendLine();

            AppendImporterReport(report, assetPath);

            bool doorsHealthy = true;
            bool glassHealthy = true;
            bool collisionHealthy = true;

            report.AppendLine();
            report.AppendLine("DOOR / GLASS / COLLISION THREE-WAY CHECK");
            report.AppendLine(new string('-', 58));

            foreach (string suffix in DoorSuffixes)
            {
                string doorName = $"Door_{suffix}";
                string glassName = $"Glass_{suffix}";
                string collisionName = $"COL_Door_{suffix}";

                report.AppendLine();
                report.AppendLine($"[{suffix}]");

                Transform door = ResolveExactlyOne(
                    report,
                    byName,
                    doorName,
                    required: true,
                    ref doorsHealthy);

                if (door != null)
                {
                    AppendObjectReport(
                        report,
                        "VISIBLE DOOR",
                        door,
                        expectedVisible: true);

                    Renderer renderer = door.GetComponent<Renderer>();

                    if (renderer == null ||
                        !renderer.enabled ||
                        renderer.sharedMaterials.Length == 0)
                    {
                        doorsHealthy = false;
                        report.AppendLine(
                            "  RESULT: ERROR — visual door is not configured " +
                            "as a visible rendered mesh.");
                    }
                    else
                    {
                        report.AppendLine(
                            "  RESULT: visual door renderer is present.");
                    }
                }

                Transform glass = ResolveExactlyOne(
                    report,
                    byName,
                    glassName,
                    required: true,
                    ref glassHealthy);

                if (glass != null)
                {
                    AppendObjectReport(
                        report,
                        "VISIBLE GLASS",
                        glass,
                        expectedVisible: true);

                    bool correctParent =
                        door != null &&
                        glass.parent == door;

                    report.AppendLine(
                        "  Parent relationship: " +
                        (correctParent ? "PASS" : "ERROR"));
                    report.AppendLine(
                        $"  Expected immediate parent: {doorName}");

                    if (!correctParent)
                    {
                        glassHealthy = false;
                    }

                    Renderer renderer = glass.GetComponent<Renderer>();

                    if (renderer == null ||
                        !renderer.enabled ||
                        renderer.sharedMaterials.Length == 0)
                    {
                        glassHealthy = false;
                        report.AppendLine(
                            "  RESULT: ERROR — glass is not configured as a " +
                            "visible rendered mesh.");
                    }
                    else
                    {
                        bool transparent =
                            renderer.sharedMaterials.Any(
                                IsMaterialTransparent);

                        report.AppendLine(
                            "  Transparent Unity material detected: " +
                            (transparent
                                ? "YES"
                                : "NO — cleanup required"));

                        if (!transparent)
                        {
                            report.AppendLine(
                                "  NOTE: The safe preparer can assign a " +
                                "Unity 6 URP transparent glass material.");
                        }
                    }
                }

                Transform collision = ResolveExactlyOne(
                    report,
                    byName,
                    collisionName,
                    required: true,
                    ref collisionHealthy);

                if (collision != null)
                {
                    AppendObjectReport(
                        report,
                        "COLLISION PROXY",
                        collision,
                        expectedVisible: false);

                    MeshFilter filter = collision.GetComponent<MeshFilter>();
                    Renderer renderer = collision.GetComponent<Renderer>();

                    if (filter == null || filter.sharedMesh == null)
                    {
                        collisionHealthy = false;
                        report.AppendLine(
                            "  RESULT: ERROR — collision proxy has no " +
                            "MeshFilter/shared mesh.");
                    }
                    else
                    {
                        report.AppendLine(
                            "  RESULT: collision mesh data is retained.");
                    }

                    if (renderer != null && renderer.enabled)
                    {
                        report.AppendLine(
                            "  CLEANUP: MeshRenderer is currently enabled and " +
                            "should be disabled on the runtime prefab.");
                    }
                    else
                    {
                        report.AppendLine(
                            "  RESULT: collision renderer is already hidden.");
                    }
                }
            }

            report.AppendLine();
            report.AppendLine("GLOBAL DEBUG / EDITOR-ONLY CHECK");
            report.AppendLine(new string('-', 58));

            List<Transform> collisionObjects = transforms
                .Where(item => item.name.StartsWith(
                    "COL_",
                    StringComparison.Ordinal))
                .ToList();

            List<Transform> fitObjects = transforms
                .Where(item => item.name.StartsWith(
                    "FIT_",
                    StringComparison.Ordinal))
                .ToList();

            List<Renderer> enabledDebugRenderers = truckRoot
                .GetComponentsInChildren<Renderer>(true)
                .Where(renderer =>
                    renderer.enabled &&
                    renderer.gameObject.activeInHierarchy &&
                    renderer.sharedMaterials.Any(material =>
                        material != null &&
                        material.name.IndexOf(
                            "HT_Debug_Col",
                            StringComparison.OrdinalIgnoreCase) >= 0))
                .ToList();

            report.AppendLine($"COL_* objects: {collisionObjects.Count}");
            report.AppendLine(
                "COL_* objects with enabled renderer: " +
                collisionObjects.Count(HasEnabledRenderer));
            report.AppendLine($"FIT_* objects: {fitObjects.Count}");
            report.AppendLine(
                "FIT_* objects currently activeSelf: " +
                fitObjects.Count(item => item.gameObject.activeSelf));
            report.AppendLine(
                "Enabled active renderers using HT_Debug_Col: " +
                enabledDebugRenderers.Count);

            foreach (Renderer renderer in enabledDebugRenderers)
            {
                report.AppendLine(
                    "  - " + GetHierarchyPath(renderer.transform));
            }

            Camera[] cameras =
                truckRoot.GetComponentsInChildren<Camera>(true);
            Light[] lights =
                truckRoot.GetComponentsInChildren<Light>(true);

            report.AppendLine(
                $"Imported Camera components under root: {cameras.Length}");

            foreach (Camera camera in cameras)
            {
                report.AppendLine(
                    "  - " + GetHierarchyPath(camera.transform));
            }

            report.AppendLine(
                $"Imported Light components under root: {lights.Length}");

            foreach (Light light in lights)
            {
                report.AppendLine(
                    "  - " + GetHierarchyPath(light.transform));
            }

            report.AppendLine();
            report.AppendLine("SUMMARY");
            report.AppendLine(new string('-', 58));
            report.AppendLine(
                "Visual Door_* setup: " +
                (doorsHealthy ? "PASS" : "FAIL"));
            report.AppendLine(
                "Glass_* hierarchy/render setup: " +
                (glassHealthy ? "PASS" : "FAIL"));
            report.AppendLine(
                "COL_Door_* mesh data: " +
                (collisionHealthy ? "PASS" : "FAIL"));

            bool safe =
                doorsHealthy &&
                glassHealthy &&
                collisionHealthy;

            report.AppendLine(
                "Safe to create cleaned runtime visual prefab: " +
                (safe ? "YES" : "NO — inspect errors above"));
            report.AppendLine();
            report.AppendLine(
                "No object, renderer, material, mesh, importer setting or " +
                "prefab was modified during this validation.");

            return new TruckValidationResult(
                report.ToString(),
                doorsHealthy,
                glassHealthy,
                collisionHealthy);
        }

        public static string SaveReport(
            string report,
            string rootName)
        {
            const string reportsFolder = "Assets/TruckValidationReports";
            EnsureAssetFolder(reportsFolder);

            string safeName = MakeFileNameSafe(rootName);
            string assetPath =
                reportsFolder + "/" +
                safeName + "_Validation_" +
                DateTime.Now.ToString("yyyyMMdd_HHmmss") +
                ".txt";

            string projectRoot =
                Directory.GetParent(Application.dataPath).FullName;
            string absolutePath = Path.Combine(projectRoot, assetPath);

            File.WriteAllText(absolutePath, report, Encoding.UTF8);
            AssetDatabase.Refresh();

            return assetPath;
        }

        private static void AppendImporterReport(
            StringBuilder report,
            string assetPath)
        {
            report.AppendLine("MODEL IMPORTER");
            report.AppendLine(new string('-', 58));

            if (string.IsNullOrWhiteSpace(assetPath))
            {
                report.AppendLine(
                    "Scene object selected; no source ModelImporter could " +
                    "be inspected.");
                return;
            }

            ModelImporter importer =
                AssetImporter.GetAtPath(assetPath) as ModelImporter;

            if (importer == null)
            {
                report.AppendLine(
                    "Selected asset is not controlled by a ModelImporter.");
                return;
            }

            report.AppendLine($"Import Cameras: {importer.importCameras}");
            report.AppendLine($"Import Lights: {importer.importLights}");
            report.AppendLine($"Global Scale: {importer.globalScale}");
            report.AppendLine(
                $"Material Import Mode: {importer.materialImportMode}");
            report.AppendLine(
                $"Material Location: {importer.materialLocation}");
            report.AppendLine(
                $"Import Animation: {importer.importAnimation}");
            report.AppendLine("Import Cameras desired value: False");
            report.AppendLine("Import Lights desired value: False");
        }

        private static Transform ResolveExactlyOne(
            StringBuilder report,
            Dictionary<string, List<Transform>> byName,
            string objectName,
            bool required,
            ref bool categoryHealthy)
        {
            if (!byName.TryGetValue(objectName, out List<Transform> matches) ||
                matches.Count == 0)
            {
                report.AppendLine($"  {objectName}: MISSING");

                if (required)
                {
                    categoryHealthy = false;
                }

                return null;
            }

            if (matches.Count > 1)
            {
                categoryHealthy = false;
                report.AppendLine(
                    $"  {objectName}: DUPLICATE ({matches.Count})");

                foreach (Transform duplicate in matches)
                {
                    report.AppendLine(
                        "    - " + GetHierarchyPath(duplicate));
                }

                return matches[0];
            }

            report.AppendLine($"  {objectName}: FOUND");
            return matches[0];
        }

        private static void AppendObjectReport(
            StringBuilder report,
            string role,
            Transform transform,
            bool expectedVisible)
        {
            MeshFilter filter = transform.GetComponent<MeshFilter>();
            Renderer renderer = transform.GetComponent<Renderer>();

            Mesh mesh = filter != null
                ? filter.sharedMesh
                : renderer is SkinnedMeshRenderer skinned
                    ? skinned.sharedMesh
                    : null;

            report.AppendLine($"  Role: {role}");
            report.AppendLine(
                "  Full path: " + GetHierarchyPath(transform));
            report.AppendLine(
                "  Immediate parent: " +
                (transform.parent != null
                    ? transform.parent.name
                    : "<none>"));
            report.AppendLine(
                $"  Active Self: {transform.gameObject.activeSelf}");
            report.AppendLine(
                "  Active In Hierarchy: " +
                transform.gameObject.activeInHierarchy);
            report.AppendLine(
                "  Mesh asset: " +
                (mesh != null ? mesh.name : "<none>"));
            report.AppendLine(
                "  Vertices: " +
                (mesh != null ? mesh.vertexCount : 0));
            report.AppendLine(
                "  Triangles: " + CountTriangles(mesh));
            report.AppendLine(
                "  Renderer component: " +
                (renderer != null
                    ? renderer.GetType().Name
                    : "<none>"));
            report.AppendLine(
                "  Renderer enabled: " +
                (renderer != null && renderer.enabled));
            report.AppendLine(
                $"  Expected runtime visibility: {expectedVisible}");

            if (renderer != null)
            {
                report.AppendLine(
                    $"  Shadow Casting: {renderer.shadowCastingMode}");
                report.AppendLine(
                    $"  Receive Shadows: {renderer.receiveShadows}");

                Material[] materials = renderer.sharedMaterials;
                report.AppendLine(
                    $"  Material slots: {materials.Length}");

                for (int index = 0;
                     index < materials.Length;
                     index++)
                {
                    report.AppendLine(
                        $"    [{index}] " +
                        DescribeMaterial(materials[index]));
                }
            }
            else
            {
                report.AppendLine("  Material slots: 0");
            }

            report.AppendLine(
                "  Local Position: " +
                FormatVector(transform.localPosition));
            report.AppendLine(
                "  Local Rotation: " +
                FormatVector(transform.localEulerAngles));
            report.AppendLine(
                "  Local Scale: " +
                FormatVector(transform.localScale));
            report.AppendLine(
                "  Lossy Scale: " +
                FormatVector(transform.lossyScale));
            report.AppendLine(
                "  Negative local scale axis: " +
                HasNegativeAxis(transform.localScale));
            report.AppendLine(
                "  Negative scale anywhere in parent chain: " +
                HasNegativeScaleInChain(transform));
        }

        private static string DescribeMaterial(Material material)
        {
            if (material == null)
            {
                return "<null>";
            }

            bool transparent = IsMaterialTransparent(material);

            return
                material.name +
                " | Shader: " +
                (material.shader != null
                    ? material.shader.name
                    : "<none>") +
                " | Queue: " + material.renderQueue +
                " | Transparent: " + transparent;
        }

        internal static bool IsMaterialTransparent(Material material)
        {
            if (material == null)
            {
                return false;
            }

            if (material.renderQueue >= (int)RenderQueue.Transparent)
            {
                return true;
            }

            if (material.HasProperty("_Surface") &&
                material.GetFloat("_Surface") > 0.5f)
            {
                return true;
            }

            if (material.HasProperty("_Mode") &&
                material.GetFloat("_Mode") > 1.5f)
            {
                return true;
            }

            if (material.shader != null &&
                material.shader.name.IndexOf(
                    "Transparent",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            Color color = Color.white;

            if (material.HasProperty("_BaseColor"))
            {
                color = material.GetColor("_BaseColor");
            }
            else if (material.HasProperty("_Color"))
            {
                color = material.GetColor("_Color");
            }

            return color.a < 0.999f;
        }

        private static int CountTriangles(Mesh mesh)
        {
            if (mesh == null)
            {
                return 0;
            }

            long triangleCount = 0;

            for (int subMesh = 0;
                 subMesh < mesh.subMeshCount;
                 subMesh++)
            {
                if (mesh.GetTopology(subMesh) != MeshTopology.Triangles)
                {
                    continue;
                }

                triangleCount +=
                    (long)mesh.GetIndexCount(subMesh) / 3L;
            }

            return triangleCount > int.MaxValue
                ? int.MaxValue
                : (int)triangleCount;
        }

        private static Dictionary<string, List<Transform>>
            BuildNameLookup(IEnumerable<Transform> transforms)
        {
            Dictionary<string, List<Transform>> lookup =
                new Dictionary<string, List<Transform>>(
                    StringComparer.Ordinal);

            foreach (Transform transform in transforms)
            {
                if (!lookup.TryGetValue(
                        transform.name,
                        out List<Transform> list))
                {
                    list = new List<Transform>();
                    lookup.Add(transform.name, list);
                }

                list.Add(transform);
            }

            return lookup;
        }

        private static bool HasEnabledRenderer(Transform transform)
        {
            return transform
                .GetComponents<Renderer>()
                .Any(renderer => renderer.enabled);
        }

        private static bool HasNegativeAxis(Vector3 scale)
        {
            return scale.x < 0f ||
                   scale.y < 0f ||
                   scale.z < 0f;
        }

        private static bool HasNegativeScaleInChain(Transform transform)
        {
            Transform current = transform;

            while (current != null)
            {
                if (HasNegativeAxis(current.localScale))
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        internal static string GetHierarchyPath(Transform transform)
        {
            if (transform == null)
            {
                return "<null>";
            }

            Stack<string> names = new Stack<string>();
            Transform current = transform;

            while (current != null)
            {
                names.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", names);
        }

        private static string FormatVector(Vector3 value)
        {
            return
                $"({value.x:0.####}, " +
                $"{value.y:0.####}, " +
                $"{value.z:0.####})";
        }

        private static string MakeFileNameSafe(string value)
        {
            char[] invalid = Path.GetInvalidFileNameChars();

            string result = new string(
                value.Select(character =>
                    invalid.Contains(character)
                        ? '_'
                        : character)
                    .ToArray());

            return string.IsNullOrWhiteSpace(result)
                ? "Truck"
                : result;
        }

        internal static void EnsureAssetFolder(string fullPath)
        {
            string normalized = fullPath.Replace('\\', '/');
            string[] parts = normalized.Split('/');

            if (parts.Length == 0 || parts[0] != "Assets")
            {
                throw new ArgumentException(
                    "Asset folder must begin with Assets/.");
            }

            string current = "Assets";

            for (int index = 1;
                 index < parts.Length;
                 index++)
            {
                string next = current + "/" + parts[index];

                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(
                        current,
                        parts[index]);
                }

                current = next;
            }
        }
    }
}
#endif
