// Compiled only when com.unity.addressables is installed (see the asmdef's
// versionDefines). Without it, ModuleExporterWindow shows an install hint
// instead of the project failing to compile.
#if EDUSIM_HAS_ADDRESSABLES
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace Edusim.ModuleExporter
{
    /// <summary>
    /// How the module's content reaches devices.
    /// </summary>
    public enum CatalogMode
    {
        SharedCatalog,   // default: deliverables imported into the launcher; content built THERE into its single shared catalog
        SelfHosted       // preview: per-module remote catalog built here; requires a launcher-side catalog-loading feature that does not exist yet
    }

    /// <summary>
    /// One scene of the module and the Addressables address it will get.
    /// Serializable so the manifest can carry the full scene list.
    /// </summary>
    [Serializable]
    public class SceneEntry
    {
        public string scenePath;   // "Assets/.../Foo.unity"
        public string sceneName;   // file name without extension
        public string address;     // derived, see PlanScenes
        public bool isMain;
    }

    /// <summary>
    /// Everything the pipeline needs to know about one export, gathered by the
    /// window. Plain data — build one, pass it to every pipeline call.
    /// </summary>
    public class ExportPlan
    {
        public string moduleId;
        public string title;
        public string version;
        public string sdkType;              // "OVR" or "XRToolkit" (matches the launcher SO's enum names)
        public string namespaceName;        // the shared namespace the module's scripts live in
        public string serverBaseUrl;        // default ToolchainSpec.DefaultServerBaseUrl
        public string outputRoot;           // project-relative, default "ModuleExport"
        public CatalogMode mode;
        public List<SceneEntry> scenes;     // from PlanScenes; index 0 need not be main — use the isMain flag
        public List<string> scriptPaths;    // explicit first-party scripts (from the scanner, user-filtered)
    }

    /// <summary>
    /// The export pipeline behind the window. Default (SharedCatalog) deliverables,
    /// all landing in {outputRoot}/{moduleId}_{version}/ToLauncher/:
    ///   1. {moduleId}_scripts_{version}.unitypackage  -> imported into the launcher (code -> APK)
    ///   2. {moduleId}_content_{version}.unitypackage  -> imported into the launcher, which
    ///      builds it into its own single shared Addressables catalog
    /// plus a module_manifest.json describing what was built.
    /// SelfHosted mode (PREVIEW) instead builds a per-module remote catalog HERE into
    /// {outputRoot}/{moduleId}_{version}/Upload/ServerData/{moduleId}/ — only usable
    /// by a future launcher build that can load per-module catalogs.
    /// </summary>
    public static class ModuleExportPipeline
    {
        [Serializable]
        public class ModuleManifest
        {
            public string moduleId;
            public string title;
            public string version;
            public string unityVersion;
            public string sceneAddress;
            public string downloadLabel;
            public string catalogFileName;
            public string remoteCatalogUrl;
            public string scriptsPackageFile;
            public string builtUtc;
            // Fields below were ADDED later — the names above are a published
            // contract with the launcher intake and must never be renamed.
            public string sdkType;
            public string catalogMode;      // "shared" | "selfHosted"
            public string namespaceName;
            public string serverBaseUrl;
            public string contentPackageFile;
            public List<SceneEntry> scenes;
        }

        // ---------- planning ----------

        /// <summary>
        /// Assigns launcher-convention addresses to a list of scene paths.
        /// First path is the main scene -> "{moduleId}_scene"; every other scene
        /// gets "{moduleId}_scene_{variant}" derived from its file name
        /// (launcher examples: greendek_scene_reception, chatbot_scene_1).
        /// </summary>
        public static List<SceneEntry> PlanScenes(string moduleId, IList<string> scenePaths)
        {
            var result = new List<SceneEntry>();
            if (scenePaths == null)
                return result;

            var taken = new HashSet<string>();
            for (int i = 0; i < scenePaths.Count; i++)
            {
                string path = scenePaths[i];
                var entry = new SceneEntry
                {
                    scenePath = path,
                    sceneName = Path.GetFileNameWithoutExtension(path),
                    isMain = i == 0
                };

                if (i == 0)
                {
                    entry.address = SceneAddress(moduleId);
                }
                else
                {
                    string variant = VariantFrom(moduleId, path);
                    string address = variant.Length > 0 ? $"{SceneAddress(moduleId)}_{variant}" : null;
                    // Name yields nothing (or repeats) -> fall back to the 1-based position.
                    if (address == null || taken.Contains(address))
                        address = $"{SceneAddress(moduleId)}_{i + 1}";
                    entry.address = address;
                }

                taken.Add(entry.address);
                result.Add(entry);
            }
            return result;
        }

        // Scene file name -> address variant: lowercase, moduleId prefix stripped,
        // ASCII alphanumerics only (addresses end up in SOs and URLs), runs of
        // anything else collapsed to "_".
        private static string VariantFrom(string moduleId, string scenePath)
        {
            string name = Path.GetFileNameWithoutExtension(scenePath).ToLowerInvariant();
            string id = string.IsNullOrEmpty(moduleId) ? "" : moduleId.ToLowerInvariant();
            if (id.Length > 0)
            {
                if (name.StartsWith(id + "_", StringComparison.Ordinal))
                    name = name.Substring(id.Length + 1);
                else if (name.StartsWith(id, StringComparison.Ordinal))
                    name = name.Substring(id.Length);
            }
            return Regex.Replace(name, "[^a-z0-9]+", "_").Trim('_');
        }

        // ---------- output layout ----------

        /// <summary>Root of one export: {outputRoot}/{moduleId}_{version}.</summary>
        public static string OutputDir(ExportPlan p)
        {
            string root = string.IsNullOrEmpty(p.outputRoot) ? "ModuleExport" : p.outputRoot;
            return $"{root.Replace('\\', '/').TrimEnd('/')}/{p.moduleId}_{p.version}";
        }

        /// <summary>Everything the launcher maintainer receives (packages + manifest).</summary>
        public static string ToLauncherDir(ExportPlan p) => OutputDir(p) + "/ToLauncher";

        /// <summary>SelfHosted only: what gets uploaded to {serverBaseUrl}/{moduleId}.</summary>
        public static string UploadDir(ExportPlan p) => OutputDir(p) + $"/Upload/ServerData/{p.moduleId}";

        // ---------- validation ----------

        /// <summary>
        /// Checks the plan against the launcher toolchain and scene/script rules.
        /// Every finding is prefixed "TOOLCHAIN: ", "SCRIPT: " or "SCENE: " —
        /// the window groups (and colors) by that prefix.
        /// </summary>
        public static List<string> Validate(ExportPlan p)
        {
            var warnings = new List<string>();

            // --- toolchain ---
            if (Application.unityVersion != ToolchainSpec.UnityVersion)
                warnings.Add($"TOOLCHAIN: this project is Unity {Application.unityVersion}; the launcher requires {ToolchainSpec.UnityVersion}. Bundles built here will NOT load — port the project first.");

            string packagesRoot = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Packages");
            string lockPath = Path.Combine(packagesRoot, "packages-lock.json");
            string manifestPath = Path.Combine(packagesRoot, "manifest.json");
            string lockJson = File.Exists(lockPath) ? File.ReadAllText(lockPath) : "";
            string manifestJson = File.Exists(manifestPath) ? File.ReadAllText(manifestPath) : "";
            foreach (var (id, required) in ToolchainSpec.RequiredPackages)
            {
                string installed = InstalledPackageVersion(id, lockJson, manifestJson);
                if (installed == null)
                    warnings.Add($"TOOLCHAIN: package {id} not found (launcher uses {required}).");
                else if (installed != required)
                    warnings.Add($"TOOLCHAIN: {id} is {installed} here; the launcher requires {required} — align it.");
            }

            if (p.mode == CatalogMode.SelfHosted)
                warnings.Add("TOOLCHAIN: Self-hosted catalog mode is a PREVIEW — today's launcher only loads its own shared catalog and cannot load this output. Use it only against a launcher build that has per-module catalog support.");

            // --- scripts ---
            if (p.scriptPaths == null || p.scriptPaths.Count == 0)
            {
                warnings.Add("SCRIPT: no scripts selected — run 'Detect scripts' first.");
            }
            else
            {
                foreach (string file in p.scriptPaths)
                {
                    if (string.IsNullOrEmpty(file) || !file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || !File.Exists(file))
                        continue;
                    string text = File.ReadAllText(file);
                    foreach (var (pattern, why) in ToolchainSpec.ForbiddenApiPatterns)
                    {
                        if (text.Contains(pattern))
                            warnings.Add($"SCRIPT: {Path.GetFileName(file)} uses '{pattern}' — {why}.");
                    }
                    if (!text.Contains("namespace "))
                        warnings.Add($"SCRIPT: {Path.GetFileName(file)} has no namespace — use 'Apply namespace' to wrap it in {p.namespaceName}.");
                }
            }

            // --- scenes ---
            if (p.scenes == null || p.scenes.Count == 0)
            {
                warnings.Add("SCENE: no scenes selected.");
            }
            else
            {
                bool many = p.scenes.Count > 1;
                foreach (var s in p.scenes)
                {
                    if (s == null || string.IsNullOrEmpty(s.scenePath) || !File.Exists(s.scenePath))
                        continue;
                    string where = many ? s.sceneName + ": " : "";
                    string sceneText = File.ReadAllText(s.scenePath);
                    bool hasOvr = sceneText.Contains("CenterEyeAnchor") || sceneText.Contains("OVRCameraRig");
                    bool hasXri = sceneText.Contains("XR Origin") || sceneText.Contains("XROrigin");
                    if (hasOvr && hasXri)
                        warnings.Add($"SCENE: {where}contains BOTH an OVR rig and an XR Origin — keep exactly one.");
                    if (!hasOvr && !hasXri)
                        warnings.Add($"SCENE: {where}no OVR rig or XR Origin found by name — make sure the scene ships its own complete rig.");
                    if (!sceneText.Contains("ReturnToHubButton"))
                        warnings.Add($"SCENE: {where}no ReturnToHubButton found — users need a way back to the launcher hub.");
                }
            }

            if (p.sdkType != "OVR" && p.sdkType != "XRToolkit")
                warnings.Add("SCENE: sdkType not chosen — the launcher needs OVR or XRToolkit to run the right SDK switch.");

            return warnings;
        }

        // The resolved version of an installed package, "unknown" if the package
        // is present but the version could not be parsed, null if absent.
        // packages-lock.json holds RESOLVED versions (manifest may hold ranges or
        // urls) — prefer it. Regex, not a JSON lib: JsonUtility can't read
        // dictionaries and these two files are the only JSON we ever consume.
        private static string InstalledPackageVersion(string packageId, string lockJson, string manifestJson)
        {
            var m = Regex.Match(lockJson,
                "\"" + Regex.Escape(packageId) + "\"\\s*:\\s*\\{[^{}]*?\"version\"\\s*:\\s*\"([^\"]+)\"",
                RegexOptions.Singleline);
            if (m.Success)
                return m.Groups[1].Value;

            m = Regex.Match(manifestJson, "\"" + Regex.Escape(packageId) + "\"\\s*:\\s*\"([^\"]+)\"");
            if (m.Success)
                return m.Groups[1].Value;

            string quoted = "\"" + packageId + "\"";
            return (lockJson.Contains(quoted) || manifestJson.Contains(quoted)) ? "unknown" : null;
        }

        // ---------- deliverable 1: scripts package ----------

        /// <summary>
        /// Exports exactly p.scriptPaths (a user-curated set — no folder recursion)
        /// to {moduleId}_scripts_{version}.unitypackage in ToLauncherDir.
        /// </summary>
        public static string ExportScriptsPackage(ExportPlan p)
        {
            string dir = ToLauncherDir(p);
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, $"{p.moduleId}_scripts_{p.version}.unitypackage");
            // ExportPackage keeps .meta GUIDs — the whole reason bundles built
            // from these scenes can bind to scripts imported into the launcher.
            AssetDatabase.ExportPackage(p.scriptPaths.ToArray(), file,
                ExportPackageOptions.Default);
            Debug.Log($"[ModuleExporter] Scripts package written: {file} ({p.scriptPaths.Count} scripts).");
            return file;
        }

        // ---------- deliverable 2 (SharedCatalog): content package ----------

        /// <summary>
        /// Exports the scenes plus everything they depend on (art, prefabs, audio,
        /// ScriptableObjects…) to {moduleId}_content_{version}.unitypackage in
        /// ToLauncherDir. The launcher imports it and builds the Addressables
        /// content itself, into its single shared catalog.
        /// </summary>
        public static string ExportContentPackage(ExportPlan p)
        {
            string dir = ToLauncherDir(p);
            Directory.CreateDirectory(dir);

            var scenePaths = new List<string>();
            foreach (var s in p.scenes)
                scenePaths.Add(s.scenePath);

            // Scripts are deliberately EXCLUDED: they travel in the scripts package,
            // and scenes/prefabs reference them by GUID, so the launcher re-binds
            // them after the scripts import. .asmdef files would fight the
            // launcher's own assembly layout.
            var include = new HashSet<string>();
            foreach (string dep in AssetDatabase.GetDependencies(scenePaths.ToArray(), true))
            {
                if (!dep.StartsWith("Assets/", StringComparison.Ordinal))
                    continue; // Packages/ content is not ours to ship
                if (dep.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (dep.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase))
                    continue;
                include.Add(dep);
            }
            foreach (string scenePath in scenePaths)
                include.Add(scenePath); // GetDependencies lists the roots too, but be explicit

            var assets = new List<string>(include);
            string file = Path.Combine(dir, $"{p.moduleId}_content_{p.version}.unitypackage");
            AssetDatabase.ExportPackage(assets.ToArray(), file, ExportPackageOptions.Default);
            Debug.Log($"[ModuleExporter] Content package written: {file} ({assets.Count} assets).");
            return file;
        }

        // ---------- deliverable 2 (SelfHosted PREVIEW): addressables content ----------

        /// <summary>
        /// SelfHosted only: configures this project's Addressables to build a
        /// per-module remote catalog into UploadDir. Throws in SharedCatalog mode —
        /// there, content is built in the launcher, never here.
        /// </summary>
        public static AddressableAssetSettings ConfigureAddressables(ExportPlan p)
        {
            if (p.mode != CatalogMode.SelfHosted)
                throw new InvalidOperationException(
                    "ConfigureAddressables is SelfHosted-only. In SharedCatalog mode content is NOT " +
                    "built in the module project — export the content .unitypackage instead and build " +
                    "Addressables inside the launcher project.");

            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            var profileId = settings.activeProfileId;

            string remoteBuild = UploadDir(p); // already forward-slashed
            string remoteLoad = $"{p.serverBaseUrl.TrimEnd('/')}/{p.moduleId}";
            SetProfileValue(settings, profileId, AddressableAssetSettings.kRemoteBuildPath, remoteBuild);
            SetProfileValue(settings, profileId, AddressableAssetSettings.kRemoteLoadPath, remoteLoad);

            // Each module ships its own catalog, named stably per version.
            settings.BuildRemoteCatalog = true;
            settings.OverridePlayerVersion = $"{p.moduleId}_{p.version}";
            settings.RemoteCatalogBuildPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteBuildPath);
            settings.RemoteCatalogLoadPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteLoadPath);

            string groupName = $"module_{p.moduleId}";
            var group = settings.FindGroup(groupName);
            if (group == null)
            {
                group = settings.CreateGroup(groupName, false, false, false, null,
                    typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            }
            var schema = group.GetSchema<BundledAssetGroupSchema>();
            schema.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteBuildPath);
            schema.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteLoadPath);

            // CreateOrMoveEntry is idempotent — re-exporting an existing module
            // just refreshes the same group and entries in place.
            foreach (var s in p.scenes)
            {
                string sceneGuid = AssetDatabase.AssetPathToGUID(s.scenePath);
                var entry = settings.CreateOrMoveEntry(sceneGuid, group);
                entry.address = s.address;
                entry.SetLabel(groupName, true, true);
            }

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ModuleExporter] Addressables configured: group {groupName}, {p.scenes.Count} scene(s), remote load {remoteLoad}");
            return settings;
        }

        // Don't silently clobber a profile someone set up by hand — say what we replace.
        private static void SetProfileValue(AddressableAssetSettings settings, string profileId, string variableName, string newValue)
        {
            string existing = settings.profileSettings.GetValueByName(profileId, variableName);
            if (!string.IsNullOrEmpty(existing) && existing != newValue)
                Debug.LogWarning($"[ModuleExporter] Profile '{variableName}' was '{existing}' — replacing with '{newValue}'.");
            settings.profileSettings.SetValue(profileId, variableName, newValue);
        }

        /// <summary>SelfHosted only: builds the Addressables content configured above.</summary>
        public static bool BuildContent(out string error)
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                Debug.LogWarning("[ModuleExporter] Active build target is not Android — these bundles will only work on the current target platform.");

            AddressableAssetSettings.BuildPlayerContent(out var result);
            error = result != null ? result.Error : null;
            bool ok = string.IsNullOrEmpty(error);
            Debug.Log(ok
                ? "[ModuleExporter] Content build succeeded."
                : $"[ModuleExporter] Content build FAILED: {error}");
            return ok;
        }

        // ---------- manifest ----------

        /// <summary>
        /// Writes module_manifest.json into ToLauncherDir. sceneAddress stays the
        /// MAIN scene's address (back-compat); the full list rides in `scenes`.
        /// </summary>
        public static string WriteManifest(ExportPlan p, string scriptsPackageFile, string contentPackageFile)
        {
            bool selfHosted = p.mode == CatalogMode.SelfHosted;

            string mainAddress = SceneAddress(p.moduleId);
            if (p.scenes != null)
            {
                foreach (var s in p.scenes)
                {
                    if (s.isMain) { mainAddress = s.address; break; }
                }
            }

            var manifest = new ModuleManifest
            {
                moduleId = p.moduleId,
                title = p.title,
                version = p.version,
                unityVersion = Application.unityVersion,
                sceneAddress = mainAddress,
                downloadLabel = $"module_{p.moduleId}",
                // Empty in shared mode = the module lives inside the launcher's own catalog.
                catalogFileName = selfHosted ? $"catalog_{p.moduleId}_{p.version}.json" : "",
                remoteCatalogUrl = selfHosted ? $"{p.serverBaseUrl.TrimEnd('/')}/{p.moduleId}/catalog_{p.moduleId}_{p.version}.json" : "",
                scriptsPackageFile = string.IsNullOrEmpty(scriptsPackageFile) ? "" : Path.GetFileName(scriptsPackageFile),
                // InvariantCulture, always — the dev machines run the Thai Buddhist
                // calendar locale, where default formatting yields year 2569.
                builtUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                sdkType = p.sdkType,
                catalogMode = selfHosted ? "selfHosted" : "shared",
                namespaceName = p.namespaceName,
                serverBaseUrl = p.serverBaseUrl,
                contentPackageFile = string.IsNullOrEmpty(contentPackageFile) ? "" : Path.GetFileName(contentPackageFile),
                scenes = p.scenes ?? new List<SceneEntry>()
            };

            string dir = ToLauncherDir(p);
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, "module_manifest.json");
            File.WriteAllText(file, JsonUtility.ToJson(manifest, prettyPrint: true));
            Debug.Log($"[ModuleExporter] Manifest written: {file}");
            return file;
        }

        /// <summary>The main scene's address: "{moduleId}_scene".</summary>
        public static string SceneAddress(string moduleId) => $"{moduleId}_scene";
    }
}

#endif // EDUSIM_HAS_ADDRESSABLES
