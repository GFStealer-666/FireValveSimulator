// Deliberately NOT gated behind EDUSIM_HAS_ADDRESSABLES: aligning package
// versions is exactly what a project with the WRONG (or missing) Addressables
// needs, so this must compile without it.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor.PackageManager;
using UnityEngine;

namespace Edusim.ModuleExporter
{
    /// <summary>
    /// Rewrites the module project's Packages/manifest.json so its package
    /// versions match <see cref="ToolchainSpec"/>. The one thing it cannot fix
    /// is the Unity editor version itself — that is a Unity Hub install, and on
    /// a newer editor the 2022.3-era packages usually fail to resolve at all
    /// (URP 14 requires the 2022.3 editor). The caller warns accordingly.
    /// </summary>
    public static class ToolchainAligner
    {
        public class AlignmentPlan
        {
            public bool editorMatches;
            public string manifestPath;
            /// <summary>Human-readable, e.g. "com.unity.addressables 2.9.1 → 1.22.3".</summary>
            public List<string> changes = new List<string>();
            /// <summary>Human-readable, e.g. "com.unity.xr.openxr 1.14.3 (will be added)".</summary>
            public List<string> additions = new List<string>();

            internal List<(string id, string version)> setVersions = new List<(string, string)>();
            internal List<(string id, string version)> addPackages = new List<(string, string)>();
        }

        /// <summary>
        /// Diffs the project manifest against the toolchain spec. Never writes.
        /// </summary>
        public static AlignmentPlan Plan()
        {
            var plan = new AlignmentPlan
            {
                editorMatches = Application.unityVersion == ToolchainSpec.UnityVersion,
                manifestPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Packages/manifest.json")
            };
            if (!File.Exists(plan.manifestPath))
                return plan; // nothing to rewrite — Validate already screams about this project

            string text = File.ReadAllText(plan.manifestPath);
            foreach (var (id, required) in ToolchainSpec.RequiredPackages)
            {
                var m = Regex.Match(text, "\"" + Regex.Escape(id) + "\"\\s*:\\s*\"([^\"]+)\"");
                if (!m.Success)
                {
                    plan.additions.Add($"{id} {required} (will be added)");
                    plan.addPackages.Add((id, required));
                }
                else if (m.Groups[1].Value != required)
                {
                    plan.changes.Add($"{id} {m.Groups[1].Value} → {required}");
                    plan.setVersions.Add((id, required));
                }
            }
            return plan;
        }

        /// <summary>
        /// Applies the plan: backs up manifest.json (manifest.json.backup),
        /// rewrites/adds the dependency entries, then asks the Package Manager
        /// to re-resolve (which triggers the re-import). Returns false with an
        /// error message instead of throwing.
        /// </summary>
        public static bool Apply(AlignmentPlan plan, out string error)
        {
            error = null;
            try
            {
                if (plan == null || !File.Exists(plan.manifestPath))
                {
                    error = "Packages/manifest.json not found.";
                    return false;
                }

                string text = File.ReadAllText(plan.manifestPath);
                File.WriteAllText(plan.manifestPath + ".backup", text); // last pre-align state

                foreach (var (id, version) in plan.setVersions)
                {
                    text = Regex.Replace(text,
                        "\"" + Regex.Escape(id) + "\"\\s*:\\s*\"[^\"]+\"",
                        "\"" + id + "\": \"" + version + "\"");
                }

                foreach (var (id, version) in plan.addPackages)
                {
                    var deps = Regex.Match(text, "\"dependencies\"\\s*:\\s*\\{");
                    if (!deps.Success)
                    {
                        error = "manifest.json has no \"dependencies\" object.";
                        return false;
                    }
                    int insertAt = deps.Index + deps.Length;
                    int j = insertAt;
                    while (j < text.Length && char.IsWhiteSpace(text[j]))
                        j++;
                    // An empty dependencies object must not gain a trailing comma.
                    bool empty = j < text.Length && text[j] == '}';
                    string entry = "\n    \"" + id + "\": \"" + version + "\"" + (empty ? "\n  " : ",");
                    text = text.Insert(insertAt, entry);
                }

                File.WriteAllText(plan.manifestPath, text);
                Debug.Log($"[ModuleExporter] manifest.json aligned: {plan.setVersions.Count} version(s) rewritten, {plan.addPackages.Count} package(s) added. Backup: manifest.json.backup");
                Client.Resolve(); // kicks the re-resolve + re-import immediately
                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                return false;
            }
        }
    }
}
