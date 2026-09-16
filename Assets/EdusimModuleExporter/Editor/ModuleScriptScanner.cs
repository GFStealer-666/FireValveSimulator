// Deliberately NOT gated behind EDUSIM_HAS_ADDRESSABLES: script discovery and
// namespace wrapping touch no Addressables API, so they must keep compiling
// (and stay usable from the window's fallback UI) even when the Addressables
// package is missing from the module project.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Edusim.ModuleExporter
{
    /// <summary>
    /// Result of <see cref="ModuleScriptScanner.FindModuleScripts"/>. All paths
    /// are project-relative AssetDatabase style ("Assets/...", forward slashes).
    /// The subset lists contain entries of <see cref="scripts"/>.
    /// </summary>
    public class ScriptScanResult
    {
        /// <summary>All discovered first-party candidate .cs files, deduped and sorted.</summary>
        public List<string> scripts = new List<string>();

        /// <summary>Subset of <see cref="scripts"/> with no namespace declaration —
        /// collision risk once merged into the launcher's Assembly-CSharp.</summary>
        public List<string> missingNamespace = new List<string>();

        /// <summary>Subset that looks like Asset-Store/vendor code. Still ships if
        /// needed, but shown separately so the user can exclude code they don't own.</summary>
        public List<string> thirdPartySuspects = new List<string>();

        /// <summary>Subset under a /Editor/ folder — won't ship in the player.
        /// Flagged for the user, never dropped silently.</summary>
        public List<string> editorScripts = new List<string>();

        /// <summary>Any .asmdef found among/above the discovered scripts or in the
        /// scanned folder. Scripts under an asmdef compile into that assembly, not
        /// the launcher's Assembly-CSharp, so the merge model breaks — caller warns.</summary>
        public List<string> asmdefs = new List<string>();
    }

    /// <summary>
    /// Discovers which scripts a module actually needs (scene dependency closure
    /// plus the declared scripts folder) and wraps un-namespaced scripts in a
    /// shared namespace. Exported scripts compile into the launcher's
    /// Assembly-CSharp alongside every other module, so an un-namespaced
    /// "GameManager" is a guaranteed collision eventually.
    /// </summary>
    public static class ModuleScriptScanner
    {
        // Path fragments that usually mean "vendor code the module author doesn't
        // own" — surfaced separately in the UI, never excluded automatically.
        private static readonly string[] ThirdPartyMarkers =
        {
            "/Plugins/",
            "/Standard Assets/",
            "/ThirdParty",
            "/Third Party",
            "/AssetStore",
            "/Asset Store",
            "/Oculus/",
            "/TextMesh Pro/",
            "/Samples/",
        };

        // Statement-level namespace declaration; comments are stripped before matching.
        private static readonly Regex NamespaceDeclaration =
            new Regex(@"^\s*namespace\s+[A-Za-z_]", RegexOptions.Compiled);

        /// <summary>
        /// Finds every first-party candidate script: the union of the scenes'
        /// transitive dependencies and a recursive scan of
        /// <paramref name="scriptsFolder"/> (which may be null/empty to skip).
        /// Only paths under "Assets/" are kept — dependencies under Packages/ are
        /// excluded because engine/toolchain packages are guaranteed by
        /// <see cref="ToolchainSpec"/> and module UPM code can't travel via
        /// .unitypackage anyway.
        /// </summary>
        public static ScriptScanResult FindModuleScripts(IEnumerable<string> scenePaths, string scriptsFolder)
        {
            var result = new ScriptScanResult();
            var seenScripts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenAsmdefs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Source (a): everything the scenes actually reference, transitively.
            if (scenePaths != null)
            {
                foreach (var scenePath in scenePaths)
                {
                    if (string.IsNullOrEmpty(scenePath))
                        continue;
                    foreach (var dep in AssetDatabase.GetDependencies(scenePath, true))
                    {
                        string path = Normalize(dep);
                        if (IsAssetsScript(path) && seenScripts.Add(path))
                            result.scripts.Add(path);
                    }
                }
            }

            // Source (b): the declared scripts folder — catches scripts no scene
            // serializes a reference to (AddComponent at runtime, utilities).
            if (!string.IsNullOrEmpty(scriptsFolder) && Directory.Exists(scriptsFolder))
            {
                foreach (var file in Directory.GetFiles(scriptsFolder, "*.cs", SearchOption.AllDirectories))
                {
                    string path = Normalize(file);
                    if (IsAssetsScript(path) && seenScripts.Add(path))
                        result.scripts.Add(path);
                }
                foreach (var file in Directory.GetFiles(scriptsFolder, "*.asmdef", SearchOption.AllDirectories))
                {
                    string path = Normalize(file);
                    if (seenAsmdefs.Add(path))
                        result.asmdefs.Add(path);
                }
            }

            result.scripts.Sort(StringComparer.Ordinal);

            // Subset lists inherit the sorted order of result.scripts.
            foreach (var script in result.scripts)
            {
                if (!HasNamespace(script))
                    result.missingNamespace.Add(script);
                if (LooksThirdParty(script))
                    result.thirdPartySuspects.Add(script);
                if (script.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0)
                    result.editorScripts.Add(script);

                CollectAsmdefsUpward(script, seenAsmdefs, result.asmdefs);
            }

            result.asmdefs.Sort(StringComparer.Ordinal);
            return result;
        }

        /// <summary>
        /// True if the file contains a statement-level namespace declaration.
        /// Pragmatic line scan (strips // comments, tracks /* */ blocks) —
        /// deliberately not a full C# parser.
        /// </summary>
        public static bool HasNamespace(string scriptPath)
        {
            if (string.IsNullOrEmpty(scriptPath) || !File.Exists(scriptPath))
                return false;

            bool inBlockComment = false;
            foreach (var raw in File.ReadAllLines(scriptPath))
            {
                string line = raw;
                if (inBlockComment)
                {
                    int end = line.IndexOf("*/", StringComparison.Ordinal);
                    if (end < 0)
                        continue;
                    line = line.Substring(end + 2);
                    inBlockComment = false;
                }

                // Strip comments left-to-right so "// /*" doesn't open a phantom block.
                while (true)
                {
                    int lineComment = line.IndexOf("//", StringComparison.Ordinal);
                    int blockOpen = line.IndexOf("/*", StringComparison.Ordinal);
                    if (lineComment >= 0 && (blockOpen < 0 || lineComment < blockOpen))
                    {
                        line = line.Substring(0, lineComment);
                        break;
                    }
                    if (blockOpen < 0)
                        break;
                    int blockClose = line.IndexOf("*/", blockOpen + 2, StringComparison.Ordinal);
                    if (blockClose < 0)
                    {
                        line = line.Substring(0, blockOpen);
                        inBlockComment = true;
                        break;
                    }
                    line = line.Remove(blockOpen, blockClose + 2 - blockOpen);
                }

                if (NamespaceDeclaration.IsMatch(line))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Derives the shared module namespace from a module id:
        /// "fire_valve-simulator" → "Edusim.Modules.FireValveSimulator".
        /// Splits on non-alphanumeric runs, capitalizes each part; if any part
        /// starts with a digit the whole name part is prefixed with "M" (a C#
        /// identifier can't start with a digit). Empty/null id → "Edusim.Modules.Module".
        /// </summary>
        public static string DeriveNamespace(string moduleId)
        {
            const string prefix = "Edusim.Modules.";
            if (string.IsNullOrWhiteSpace(moduleId))
                return prefix + "Module";

            var name = new StringBuilder(moduleId.Length);
            bool digitLedPart = false;
            bool atPartStart = true;
            foreach (char c in moduleId)
            {
                if (!char.IsLetterOrDigit(c))
                {
                    atPartStart = true; // any non-alphanumeric run is a separator
                    continue;
                }
                if (atPartStart)
                {
                    if (char.IsDigit(c))
                        digitLedPart = true;
                    name.Append(char.ToUpperInvariant(c));
                    atPartStart = false;
                }
                else
                {
                    name.Append(c);
                }
            }

            if (name.Length == 0)
                return prefix + "Module"; // id was all separators
            if (digitLedPart)
                name.Insert(0, 'M');
            return prefix + name;
        }

        /// <summary>
        /// Wraps each file's ENTIRE content (usings included — legal inside a
        /// namespace) in <paramref name="namespaceName"/>. Skips files that
        /// already declare a namespace, contain assembly-level attributes
        /// (those must precede a namespace), or don't exist. Preserves each
        /// file's newline flavor and UTF-8 BOM. Returns the number of files
        /// modified; refreshes the AssetDatabase once if anything changed.
        /// </summary>
        public static int ApplyNamespace(IEnumerable<string> scriptPaths, string namespaceName)
        {
            int modified = 0;
            int skipped = 0;

            if (scriptPaths != null)
            {
                foreach (var scriptPath in scriptPaths)
                {
                    if (string.IsNullOrEmpty(scriptPath) || !File.Exists(scriptPath) || HasNamespace(scriptPath))
                    {
                        skipped++;
                        continue;
                    }

                    // Raw bytes so the BOM decision survives the round trip.
                    byte[] bytes = File.ReadAllBytes(scriptPath);
                    bool hadBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
                    int offset = hadBom ? 3 : 0;
                    string content = Encoding.UTF8.GetString(bytes, offset, bytes.Length - offset);

                    if (content.Contains("[assembly:"))
                    {
                        // Assembly-level attributes must precede any namespace —
                        // wrapping this file would not compile. Leave it to the user.
                        Debug.LogWarning($"[ModuleExporter] Skipped {scriptPath}: contains assembly-level attributes.");
                        skipped++;
                        continue;
                    }

                    // Safety property: introducing a namespace does NOT break
                    // Unity's serialized references (scenes bind MonoBehaviours by
                    // .meta GUID + class name, and the file name still matches the
                    // class name) — but it DOES break unqualified cross-references
                    // from scripts NOT being wrapped, which is why the caller wraps
                    // the whole selected set in one operation with one shared namespace.
                    string nl = content.Contains("\r\n") ? "\r\n" : "\n";
                    string[] lines = content.Split('\n');
                    for (int i = 0; i < lines.Length; i++)
                        lines[i] = lines[i].TrimEnd('\r');
                    bool endsWithNewline = content.EndsWith("\n", StringComparison.Ordinal);
                    int lineCount = endsWithNewline ? lines.Length - 1 : lines.Length;

                    var sb = new StringBuilder(content.Length + 128);
                    sb.Append("namespace ").Append(namespaceName).Append(nl);
                    sb.Append('{').Append(nl);
                    for (int i = 0; i < lineCount; i++)
                    {
                        if (lines[i].Length > 0)
                            sb.Append("    "); // indent non-empty lines only
                        sb.Append(lines[i]).Append(nl);
                    }
                    sb.Append('}');
                    if (endsWithNewline)
                        sb.Append(nl);

                    File.WriteAllText(scriptPath, sb.ToString(), new UTF8Encoding(hadBom));
                    modified++;
                }
            }

            if (modified > 0)
                AssetDatabase.Refresh(); // one refresh for the whole batch, not per file
            Debug.Log($"[ModuleExporter] Namespace '{namespaceName}': {modified} script(s) wrapped, {skipped} skipped (already namespaced, assembly attributes, or missing).");
            return modified;
        }

        // ---------- helpers ----------

        // AssetDatabase style: forward slashes only.
        private static string Normalize(string path) => path.Replace('\\', '/');

        private static bool IsAssetsScript(string path) =>
            path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) &&
            path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase);

        private static bool LooksThirdParty(string path)
        {
            foreach (var marker in ThirdPartyMarkers)
            {
                if (path.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        // Walks from the script's folder up to (and including) Assets/ collecting
        // .asmdef files — an asmdef anywhere above pulls the script out of
        // Assembly-CSharp, which the launcher's merge model relies on.
        private static void CollectAsmdefsUpward(string scriptPath, HashSet<string> seen, List<string> results)
        {
            int slash = scriptPath.LastIndexOf('/');
            string dir = slash < 0 ? null : scriptPath.Substring(0, slash);
            while (!string.IsNullOrEmpty(dir) && dir.StartsWith("Assets", StringComparison.OrdinalIgnoreCase))
            {
                if (Directory.Exists(dir))
                {
                    foreach (var file in Directory.GetFiles(dir, "*.asmdef", SearchOption.TopDirectoryOnly))
                    {
                        string path = Normalize(file);
                        if (seen.Add(path))
                            results.Add(path);
                    }
                }
                if (string.Equals(dir, "Assets", StringComparison.OrdinalIgnoreCase))
                    break;
                slash = dir.LastIndexOf('/');
                dir = slash < 0 ? null : dir.Substring(0, slash);
            }
        }
    }
}
