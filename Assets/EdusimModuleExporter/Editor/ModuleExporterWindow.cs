#if EDUSIM_HAS_ADDRESSABLES
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Edusim.ModuleExporter
{
    /// <summary>
    /// Edusim > Module Exporter — run inside a MODULE project (not the launcher).
    /// v0.2: guided sections (identity, scenes, scripts, delivery) all feed one
    /// ExportPlan. Shared-catalog mode hands .unitypackages + manifest to the
    /// launcher maintainer; self-hosted mode builds bundles + a per-module
    /// catalog here (preview — today's launcher can't load it yet).
    /// </summary>
    public class ModuleExporterWindow : EditorWindow
    {
        // ---- serialized state — private fields need [SerializeField] to survive domain reloads ----

        [Tooltip("lowercase, no spaces, e.g. firedrill")]
        [SerializeField] private string _moduleId = "";
        [Tooltip("shown in the launcher library")]
        [SerializeField] private string _title = "";
        [Tooltip("recorded in the manifest and in package file names")]
        [SerializeField] private string _version = "1.0.0";
        [Tooltip("\"OVR\" or \"XRToolkit\" — empty until chosen")]
        [SerializeField] private string _sdkType = "";
        [Tooltip("wrapped around namespace-less scripts; auto-derived from module id until hand-edited")]
        [SerializeField] private string _namespaceName = "";
        [SerializeField] private bool _namespaceEdited; // user took over -> stop auto-deriving

        [Tooltip("row 0 is the scene the launcher's Play button loads")]
        [SerializeField] private List<SceneAsset> _scenes = new() { null };

        [SerializeField] private DefaultAsset _scriptsFolder;
        // Parallel lists because Dictionary<string, bool> does not serialize.
        [SerializeField] private List<string> _foundScripts = new();
        [SerializeField] private List<bool> _scriptOn = new();
        [SerializeField] private List<string> _missingNamespace = new();
        [SerializeField] private List<string> _thirdParty = new();
        [SerializeField] private List<string> _editorScripts = new();
        [SerializeField] private List<string> _asmdefs = new();
        [SerializeField] private bool _scriptsFoldout = true;

        [SerializeField] private CatalogMode _mode = CatalogMode.SharedCatalog;
        [SerializeField] private bool _overrideServerUrl;
        [SerializeField] private string _serverBaseUrl = ToolchainSpec.DefaultServerBaseUrl;
        [SerializeField] private string _outputRoot = "ModuleExport";

        [SerializeField] private List<string> _findings = new();
        [SerializeField] private bool _hasValidated;
        [SerializeField] private bool _foldToolchain = true;
        [SerializeField] private bool _foldScriptFindings = true;
        [SerializeField] private bool _foldSceneFindings = true;
        [SerializeField] private string _lastResult = "";

        [SerializeField] private Vector2 _mainScroll;
        [SerializeField] private Vector2 _scriptScroll;
        [SerializeField] private Vector2 _findingScroll;

        // Not serialized on purpose: cheap to recompute, invalidated by Validate/Apply.
        [NonSerialized] private ToolchainAligner.AlignmentPlan _alignPlan;

        private static readonly GUIContent[] SdkOptions =
        {
            new GUIContent("— choose —"),
            new GUIContent("OVR", "Meta Interaction SDK rig (OVRCameraRig) — launcher's OVR→OVR switch is a no-op"),
            new GUIContent("XRToolkit", "XR Interaction Toolkit rig (XR Origin) — launcher suspends OVR and starts the XR loader"),
        };

        private static readonly GUIContent[] ModeOptions =
        {
            new GUIContent("Shared catalog — import into launcher (recommended)"),
            new GUIContent("Self-hosted catalog (preview — needs launcher support)"),
        };

        [MenuItem("Edusim/Module Exporter")]
        public static void Open()
        {
            var w = GetWindow<ModuleExporterWindow>("Module Exporter");
            w.minSize = new Vector2(500, 640);
        }

        private void OnEnable()
        {
            if (_scenes.Count == 0)
                _scenes.Add(null); // always offer a Main row
        }

        private void OnGUI()
        {
            // Fixed label column — the dynamic default gets cramped in a wide window.
            EditorGUIUtility.labelWidth = 150f;
            _mainScroll = EditorGUILayout.BeginScrollView(_mainScroll);
            DrawIdentity();
            DrawScenes();
            DrawScripts();
            DrawDelivery();
            DrawActions();
            DrawResults();
            EditorGUILayout.EndScrollView();
        }

        // Thin separator + bold title — calmer than stacked bold labels and ad-hoc Space() calls.
        private static void SectionHeader(string title)
        {
            EditorGUILayout.Space(10);
            var line = GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(line, new Color(0.5f, 0.5f, 0.5f, 0.25f));
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }

        // ---------- section 1: identity ----------

        private void DrawIdentity()
        {
            EditorGUILayout.LabelField("Module identity", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            _moduleId = EditorGUILayout.TextField(new GUIContent("Module id", "lowercase, no spaces, e.g. firedrill"), _moduleId);
            if (EditorGUI.EndChangeCheck() && !_namespaceEdited)
                _namespaceName = ModuleScriptScanner.DeriveNamespace(_moduleId); // follow the id until the user takes over

            _title = EditorGUILayout.TextField(new GUIContent("Title", "shown in the launcher library"), _title);
            _version = EditorGUILayout.TextField(new GUIContent("Version", "recorded in the manifest and in package file names"), _version);

            int sdkIndex = _sdkType == "OVR" ? 1 : _sdkType == "XRToolkit" ? 2 : 0;
            sdkIndex = EditorGUILayout.Popup(
                new GUIContent("SDK type", "which VR stack the scenes use — drives the launcher's SDK switch before load"),
                sdkIndex, SdkOptions);
            _sdkType = sdkIndex == 1 ? "OVR" : sdkIndex == 2 ? "XRToolkit" : "";

            EditorGUI.BeginChangeCheck();
            _namespaceName = EditorGUILayout.TextField(
                new GUIContent("Namespace", "wrapped around scripts that lack one — avoids collisions inside the launcher's Assembly-CSharp"),
                _namespaceName);
            if (EditorGUI.EndChangeCheck())
                _namespaceEdited = _namespaceName != ModuleScriptScanner.DeriveNamespace(_moduleId); // retyping the derived value re-enables auto
        }

        // ---------- section 2: scenes ----------

        private void DrawScenes()
        {
            SectionHeader("Scenes");

            var addresses = PlannedAddresses();
            int removeAt = -1;
            for (int i = 0; i < _scenes.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                _scenes[i] = (SceneAsset)EditorGUILayout.ObjectField(
                    new GUIContent(i == 0 ? "Main" : $"Scene {i + 1}",
                        i == 0 ? "the scene the launcher's Play button loads" : "extra scene packed into the same module"),
                    _scenes[i], typeof(SceneAsset), false);

                string path = _scenes[i] != null ? AssetDatabase.GetAssetPath(_scenes[i]) : null;
                string address = path != null && addresses.TryGetValue(path, out var a) ? a : "";
                EditorGUILayout.LabelField(new GUIContent(address, "the Addressables address the launcher loads this scene by"),
                    EditorStyles.miniLabel, GUILayout.Width(160));

                if (GUILayout.Button(new GUIContent("−", "remove this scene"), GUILayout.Width(22)))
                    removeAt = i;
                EditorGUILayout.EndHorizontal();
            }
            if (removeAt >= 0)
                _scenes.RemoveAt(removeAt);

            if (GUILayout.Button("+ Add scene"))
                _scenes.Add(null);

            EditorGUILayout.LabelField("Multi-scene modules share one label — one install fetches all scenes.", EditorStyles.miniLabel);
        }

        // ---------- section 3: scripts ----------

        private void DrawScripts()
        {
            SectionHeader("Scripts");
            _scriptsFolder = (DefaultAsset)EditorGUILayout.ObjectField(
                new GUIContent("Scripts folder", "optional — narrows the scan to this module's first-party scripts"),
                _scriptsFolder, typeof(DefaultAsset), false);

            if (GUILayout.Button("Detect scripts"))
                DetectScripts();

            if (_foundScripts.Count == 0)
                return;

            while (_scriptOn.Count < _foundScripts.Count) // defensive resync after odd serialization
                _scriptOn.Add(true);

            EditorGUILayout.LabelField(ScanSummary(), EditorStyles.miniBoldLabel);

            _scriptsFoldout = EditorGUILayout.Foldout(_scriptsFoldout, $"Detected scripts ({_foundScripts.Count})", true);
            if (_scriptsFoldout)
            {
                _scriptScroll = EditorGUILayout.BeginScrollView(_scriptScroll, GUILayout.MaxHeight(170));
                for (int i = 0; i < _foundScripts.Count; i++)
                {
                    string path = _foundScripts[i];
                    string label = Path.GetFileName(path);
                    if (_missingNamespace.Contains(path)) label += " (no namespace)";
                    if (_thirdParty.Contains(path)) label += " (3rd-party?)";
                    if (_editorScripts.Contains(path)) label += " (Editor)";
                    _scriptOn[i] = EditorGUILayout.ToggleLeft(new GUIContent(label, path), _scriptOn[i]);
                }
                EditorGUILayout.EndScrollView();
            }

            if (_asmdefs.Count > 0)
                EditorGUILayout.HelpBox(
                    "Assembly definitions found:\n" + string.Join("\n", _asmdefs) + "\n" +
                    "Scripts under an asmdef will NOT merge into the launcher's Assembly-CSharp — remove the asmdef or move the scripts.",
                    MessageType.Warning);

            // Hidden entirely when there is nothing to wrap — a disabled
            // "Apply namespace to 0 script(s)" button is just noise.
            int n = ApplyNamespaceTargets().Count;
            if (n > 0 && GUILayout.Button($"Apply namespace to {n} script(s)"))
                RunApplyNamespace();
        }

        private string ScanSummary()
        {
            string s = $"{_foundScripts.Count} scripts";
            if (_missingNamespace.Count > 0) s += $" · {_missingNamespace.Count} without namespace";
            if (_thirdParty.Count > 0) s += $" · {_thirdParty.Count} third-party suspects";
            if (_editorScripts.Count > 0) s += $" · {_editorScripts.Count} editor scripts";
            return s;
        }

        private void DetectScripts()
        {
            var result = ModuleScriptScanner.FindModuleScripts(ScenePaths(),
                _scriptsFolder != null ? AssetDatabase.GetAssetPath(_scriptsFolder) : null);

            var previousPaths = _foundScripts;
            var previousOn = _scriptOn;
            _foundScripts = new List<string>(result.scripts);
            _missingNamespace = new List<string>(result.missingNamespace);
            _thirdParty = new List<string>(result.thirdPartySuspects);
            _editorScripts = new List<string>(result.editorScripts);
            _asmdefs = new List<string>(result.asmdefs);

            _scriptOn = new List<bool>(_foundScripts.Count);
            foreach (var path in _foundScripts)
            {
                int prev = previousPaths.IndexOf(path);
                // default ON, third-party suspects OFF; a re-scan keeps the user's earlier choices
                _scriptOn.Add(prev >= 0 && prev < previousOn.Count ? previousOn[prev] : !_thirdParty.Contains(path));
            }
            _lastResult = $"Detected {_foundScripts.Count} script(s).";
        }

        private List<string> ApplyNamespaceTargets()
        {
            var targets = new List<string>(); // selected ∩ missingNamespace
            for (int i = 0; i < _foundScripts.Count && i < _scriptOn.Count; i++)
                if (_scriptOn[i] && _missingNamespace.Contains(_foundScripts[i]))
                    targets.Add(_foundScripts[i]);
            return targets;
        }

        private void RunApplyNamespace()
        {
            var targets = ApplyNamespaceTargets();
            if (!EditorUtility.DisplayDialog("Apply namespace",
                $"Wrap {targets.Count} file(s) in 'namespace {_namespaceName}' in place.\n\n" +
                "Safe for scene references — Unity binds scripts by GUID — but any NON-wrapped script that references these types by unqualified name will need a using directive.",
                "Apply", "Cancel"))
                return;

            ModuleScriptScanner.ApplyNamespace(targets, _namespaceName);
            DetectScripts(); // re-scan so the (no namespace) markers clear
        }

        // ---------- section 4: delivery ----------

        private void DrawDelivery()
        {
            SectionHeader("Delivery");

            _mode = (CatalogMode)EditorGUILayout.Popup(
                new GUIContent("Catalog mode", "how the content reaches the launcher"), (int)_mode, ModeOptions);

            EditorGUILayout.HelpBox(_mode == CatalogMode.SharedCatalog
                ? "Produces scripts + content .unitypackages and module_manifest.json under ToLauncher/. " +
                  "The launcher maintainer imports both; content is then built and uploaded FROM the launcher project."
                : "Builds Addressables bundles + a per-module catalog in THIS project, into Upload/ServerData/{id}/. " +
                  "Today's launcher CANNOT load a self-hosted catalog yet — use shared catalog for real deliveries.",
                MessageType.Info);

            if (_overrideServerUrl)
                _serverBaseUrl = EditorGUILayout.TextField(
                    new GUIContent("Server base URL", "custom content server — bundles load from {url}/{moduleId}/ at runtime"),
                    _serverBaseUrl);
            else
                EditorGUILayout.LabelField(new GUIContent("Server base URL"),
                    new GUIContent(ToolchainSpec.DefaultServerBaseUrl, "fixed for the fleet — tick Override to change"));
            _overrideServerUrl = EditorGUILayout.ToggleLeft(
                new GUIContent("Override server URL", "advanced — the fleet always uses the default"), _overrideServerUrl);

            _outputRoot = EditorGUILayout.TextField(
                new GUIContent("Output root", "project-relative folder all export output lands under"), _outputRoot);

            var p = PathPreviewPlan();
            EditorGUILayout.LabelField("Output", ModuleExportPipeline.OutputDir(p), EditorStyles.miniLabel);
            EditorGUILayout.LabelField("To launcher", ModuleExportPipeline.ToLauncherDir(p), EditorStyles.miniLabel);
            if (_mode == CatalogMode.SelfHosted)
                EditorGUILayout.LabelField("Upload", ModuleExportPipeline.UploadDir(p), EditorStyles.miniLabel);
        }

        // ---------- section 5: actions ----------

        private void DrawActions()
        {
            SectionHeader("Export");
            bool ready = !string.IsNullOrWhiteSpace(_moduleId) && ScenePaths().Count > 0 && _foundScripts.Count > 0;
            using (new EditorGUI.DisabledScope(!ready))
            {
                // Single compact row for the steps; default button height on purpose.
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(new GUIContent("1 · Validate", "check the project against the launcher toolchain spec")))
                    RunValidate();
                if (GUILayout.Button(new GUIContent("2 · Scripts .unitypackage", "export the selected scripts, GUIDs preserved")))
                    RunExportScripts();
                if (_mode == CatalogMode.SharedCatalog)
                {
                    if (GUILayout.Button(new GUIContent("3 · Content .unitypackage", "export the scenes + their asset dependencies")))
                        RunExportContent();
                }
                else if (GUILayout.Button(new GUIContent("3 · Build bundles", "configure + build the per-module Addressables catalog (preview)")))
                    RunConfigureAndBuild();
                EditorGUILayout.EndHorizontal();
                if (GUILayout.Button(new GUIContent("Export everything  (1 → 2 → 3 + manifest)", "the full pipeline in one click")))
                    RunAll();
            }
            if (!ready)
                EditorGUILayout.HelpBox("Fill in module id, add at least one scene, and run Detect scripts.", MessageType.Info);
        }

        private void RunValidate()
        {
            _findings = ModuleExportPipeline.Validate(BuildPlan());
            _hasValidated = true;
            _alignPlan = null; // re-diff the manifest next time it is drawn
            _lastResult = _findings.Count == 0
                ? "" // the green box below says it
                : $"Validation finished with {_findings.Count} finding(s). TOOLCHAIN findings must be fixed; others are strong recommendations.";
        }

        private void RunExportScripts()
        {
            string file = ModuleExportPipeline.ExportScriptsPackage(BuildPlan());
            _lastResult = $"Scripts package: {file}\nThe launcher maintainer imports it (Assets > Import Package), then rebuilds the launcher APK.";
        }

        private void RunExportContent()
        {
            string file = ModuleExportPipeline.ExportContentPackage(BuildPlan());
            _lastResult = $"Content package: {file}\nHand it to the launcher maintainer alongside the scripts package.";
        }

        private void RunConfigureAndBuild()
        {
            var plan = BuildPlan();
            ModuleExportPipeline.ConfigureAddressables(plan);
            _lastResult = ModuleExportPipeline.BuildContent(out string error)
                ? $"Content built. Upload {ModuleExportPipeline.UploadDir(plan)} to your server so it is reachable at {plan.serverBaseUrl.TrimEnd('/')}/{plan.moduleId}/"
                : $"Content build failed: {error}";
        }

        private void RunAll()
        {
            var plan = BuildPlan();
            _findings = ModuleExportPipeline.Validate(plan);
            _hasValidated = true;
            _alignPlan = null;
            bool hasToolchainError = _findings.Exists(f => f.StartsWith("TOOLCHAIN"));
            if (hasToolchainError &&
                !EditorUtility.DisplayDialog("Toolchain mismatch",
                    "This project does not match the launcher toolchain spec — bundles may not load in the launcher. Export anyway?",
                    "Export anyway", "Stop"))
            {
                _lastResult = "Export stopped — fix the TOOLCHAIN findings first.";
                return;
            }

            string scriptsPkg = ModuleExportPipeline.ExportScriptsPackage(plan);

            if (_mode == CatalogMode.SharedCatalog)
            {
                string contentPkg = ModuleExportPipeline.ExportContentPackage(plan);
                string manifest = ModuleExportPipeline.WriteManifest(plan, scriptsPkg, contentPkg);
                _lastResult =
                    $"Export complete. Hand everything in {ModuleExportPipeline.ToLauncherDir(plan)} to the launcher maintainer:\n" +
                    $"1) Import {Path.GetFileName(scriptsPkg)} (Assets > Import Package)\n" +
                    $"2) Import {Path.GetFileName(contentPkg)}\n" +
                    "3) Follow Docs/Adding-A-Module.md: create the Addressables group entries + a ModuleDefinitionSO " +
                    $"from {Path.GetFileName(manifest)}, build content in the launcher, upload ServerData/<Platform>/ to the server.";
            }
            else
            {
                ModuleExportPipeline.ConfigureAddressables(plan);
                if (!ModuleExportPipeline.BuildContent(out string error))
                {
                    _lastResult = $"Content build failed: {error}";
                    return;
                }
                string manifest = ModuleExportPipeline.WriteManifest(plan, scriptsPkg, null);
                _lastResult =
                    "Export complete. Deliver two things:\n" +
                    $"1) {scriptsPkg}  → launcher maintainer imports it, rebuilds APK\n" +
                    $"2) {ModuleExportPipeline.UploadDir(plan)}  → upload to {plan.serverBaseUrl.TrimEnd('/')}/{plan.moduleId}/\n" +
                    $"Manifest ({manifest}) has the catalog URL + scene key for the launcher's ModuleDefinitionSO.\n" +
                    "NOTE: today's launcher cannot load a self-hosted catalog — preview only.";
            }
        }

        // ---------- section 6: results & findings ----------

        private void DrawResults()
        {
            if (!string.IsNullOrEmpty(_lastResult))
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.HelpBox(_lastResult, MessageType.None);
            }

            if (_hasValidated && _findings.Count == 0)
            {
                EditorGUILayout.Space(6);
                Color prev = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.55f, 1f, 0.55f); // green = safe to ship
                EditorGUILayout.HelpBox("Validation passed — no findings.", MessageType.Info);
                GUI.backgroundColor = prev;
            }
            if (_findings.Count == 0)
                return;

            var toolchain = new List<string>();
            var scripts = new List<string>();
            var scenes = new List<string>();
            foreach (var f in _findings)
            {
                if (f.StartsWith("TOOLCHAIN")) toolchain.Add(f);
                else if (f.StartsWith("SCRIPT")) scripts.Add(f);
                else scenes.Add(f);
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField($"Findings ({_findings.Count})", EditorStyles.boldLabel);
            // capped height so the action buttons stay visible above
            _findingScroll = EditorGUILayout.BeginScrollView(_findingScroll, GUILayout.MaxHeight(220));
            _foldToolchain = DrawFindingGroup(_foldToolchain, "Toolchain (errors)", toolchain, MessageType.Error);
            _foldScriptFindings = DrawFindingGroup(_foldScriptFindings, "Scripts", scripts, MessageType.Warning);
            _foldSceneFindings = DrawFindingGroup(_foldSceneFindings, "Scenes", scenes, MessageType.Warning);
            EditorGUILayout.EndScrollView();

            if (toolchain.Count > 0)
                DrawAlignPackages();
        }

        private static bool DrawFindingGroup(bool fold, string label, List<string> items, MessageType type)
        {
            if (items.Count == 0)
                return fold;
            fold = EditorGUILayout.Foldout(fold, $"{label} ({items.Count})", true);
            if (!fold)
                return fold;
            // One compact icon + wrapped line per finding — stacked HelpBoxes
            // turned 36 findings into a wall.
            GUIContent icon = EditorGUIUtility.IconContent(
                type == MessageType.Error ? "console.erroricon.sml" : "console.warnicon.sml");
            foreach (var f in items)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(icon, GUILayout.Width(18), GUILayout.Height(15));
                // Strip the routing prefix — the group header already names it.
                int cut = f.IndexOf(": ", StringComparison.Ordinal);
                EditorGUILayout.LabelField(cut > 0 ? f.Substring(cut + 2) : f, EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.EndHorizontal();
            }
            return fold;
        }

        // ---------- fix package versions ----------

        private void DrawAlignPackages()
        {
            if (_alignPlan == null)
                _alignPlan = ToolchainAligner.Plan(); // cached — recomputed after each Validate/Apply
            if (_alignPlan.changes.Count == 0 && _alignPlan.additions.Count == 0)
                return; // only the editor itself mismatches — nothing this button could rewrite

            if (!GUILayout.Button(new GUIContent("Fix package versions…",
                "rewrites Packages/manifest.json to the launcher-spec versions (backup kept)"), GUILayout.Width(190)))
                return;

            var lines = new List<string>();
            lines.AddRange(_alignPlan.changes);
            lines.AddRange(_alignPlan.additions);
            string summary = string.Join("\n", lines);

            // The one thing no button can fix: the editor build itself. Aligning
            // 2022.3-era packages under a newer editor usually fails to resolve
            // (URP 14 requires the 2022.3 editor), so warn before offering it.
            if (!_alignPlan.editorMatches &&
                !EditorUtility.DisplayDialog("Editor version mismatch",
                    $"This project runs Unity {Application.unityVersion}; the launcher toolchain is Unity {ToolchainSpec.UnityVersion}.\n\n" +
                    "The exporter cannot change the editor — install 2022.3.62f3 via Unity Hub and open the project with it first.\n\n" +
                    "Aligning 2022.3-era packages while on a newer editor usually fails to resolve or compile.\n\n" +
                    "Align the packages anyway?",
                    "Align anyway", "Cancel"))
                return;

            if (!EditorUtility.DisplayDialog("Align package versions",
                "Packages/manifest.json will be rewritten (backup saved next to it):\n\n" + summary +
                "\n\nUnity will re-resolve and re-import. Scripts written against newer package APIs may then show compile errors to fix by hand.",
                "Apply", "Cancel"))
                return;

            _lastResult = ToolchainAligner.Apply(_alignPlan, out string error)
                ? "manifest.json updated — backup at Packages/manifest.json.backup. Unity is resolving packages; re-run Validate after the re-import."
                : $"Package alignment failed: {error}";
            _alignPlan = null;
        }

        // ---------- plan plumbing ----------

        /// <summary>One plan, built the same way for every action.</summary>
        private ExportPlan BuildPlan()
        {
            return new ExportPlan
            {
                moduleId = _moduleId.Trim(),
                title = _title,
                version = _version,
                sdkType = _sdkType,
                namespaceName = _namespaceName,
                serverBaseUrl = EffectiveServerUrl,
                outputRoot = string.IsNullOrWhiteSpace(_outputRoot) ? "ModuleExport" : _outputRoot,
                mode = _mode,
                scenes = ModuleExportPipeline.PlanScenes(_moduleId.Trim(), ScenePaths()),
                scriptPaths = SelectedScripts(),
            };
        }

        // Scenes/scripts left empty on purpose — this plan only feeds the path preview.
        private ExportPlan PathPreviewPlan()
        {
            return new ExportPlan
            {
                moduleId = string.IsNullOrWhiteSpace(_moduleId) ? "<module-id>" : _moduleId.Trim(),
                version = _version,
                mode = _mode,
                serverBaseUrl = EffectiveServerUrl,
                outputRoot = string.IsNullOrWhiteSpace(_outputRoot) ? "ModuleExport" : _outputRoot,
                scenes = new List<SceneEntry>(),
                scriptPaths = new List<string>(),
            };
        }

        private string EffectiveServerUrl =>
            _overrideServerUrl && !string.IsNullOrWhiteSpace(_serverBaseUrl) ? _serverBaseUrl : ToolchainSpec.DefaultServerBaseUrl;

        private List<string> ScenePaths()
        {
            var paths = new List<string>();
            foreach (var s in _scenes)
                if (s != null)
                    paths.Add(AssetDatabase.GetAssetPath(s));
            return paths;
        }

        private List<string> SelectedScripts()
        {
            var selected = new List<string>();
            for (int i = 0; i < _foundScripts.Count && i < _scriptOn.Count; i++)
                if (_scriptOn[i])
                    selected.Add(_foundScripts[i]);
            return selected;
        }

        private Dictionary<string, string> PlannedAddresses()
        {
            var map = new Dictionary<string, string>();
            var paths = ScenePaths();
            if (string.IsNullOrWhiteSpace(_moduleId) || paths.Count == 0)
                return map;
            foreach (var entry in ModuleExportPipeline.PlanScenes(_moduleId.Trim(), paths))
                map[entry.scenePath] = entry.address;
            return map;
        }
    }
}

#else // Addressables package missing — show an install hint instead of compile errors

using UnityEditor;
using UnityEngine;

namespace Edusim.ModuleExporter
{
    public class ModuleExporterWindow : EditorWindow
    {
        [MenuItem("Edusim/Module Exporter")]
        public static void Open()
        {
            var w = GetWindow<ModuleExporterWindow>("Module Exporter");
            w.minSize = new Vector2(420, 180);
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "The Addressables package is not installed in this project.\n\n" +
                "The Module Exporter requires com.unity.addressables 1.22.3 " +
                "(the launcher toolchain version).\n\n" +
                "Install it, then reopen this window.",
                MessageType.Error);

            if (GUILayout.Button("Open Package Manager", GUILayout.Height(30)))
                UnityEditor.PackageManager.UI.Window.Open("com.unity.addressables");

            if (GUILayout.Button("Add com.unity.addressables 1.22.3 to manifest", GUILayout.Height(30)))
                UnityEditor.PackageManager.Client.Add("com.unity.addressables@1.22.3");
        }
    }
}

#endif // EDUSIM_HAS_ADDRESSABLES
