namespace Edusim.ModuleExporter
{
    /// <summary>
    /// The fleet contract: every module project must match the launcher's
    /// toolchain, or its bundles will not load in the launcher APK.
    /// Update these values ONLY when the launcher itself upgrades.
    /// </summary>
    public static class ToolchainSpec
    {
        public const string UnityVersion = "2022.3.62f3";

        /// <summary>The launcher's content server. Fixed for the fleet — the UI
        /// defaults to this and only lets advanced users override it.</summary>
        public const string DefaultServerBaseUrl = "https://poc-edusim.cheesetech.cloud/api/download";

        // package id -> required version (checked against the module project's manifest)
        public static readonly (string id, string version)[] RequiredPackages =
        {
            ("com.unity.addressables", "1.22.3"),
            ("com.unity.render-pipelines.universal", "14.0.12"),
            ("com.unity.xr.interaction.toolkit", "2.6.5"),
            ("com.unity.xr.openxr", "1.14.3"),
        };

        // Patterns that mean "this script still behaves like a standalone app"
        // — they break the launcher when the module runs inside it.
        public static readonly (string pattern, string why)[] ForbiddenApiPatterns =
        {
            ("PlayerPrefs.DeleteAll", "wipes the launcher's and every other module's saved data"),
            ("Application.Quit", "closes the whole launcher, not the module"),
            ("Application.LoadLevel", "deprecated; loads launcher scenes by build index"),
            ("SceneManager.LoadScene(0", "build index 0 is the launcher hub, not your menu"),
            ("buildIndex + 1", "build-index scene flow breaks under Addressables (buildIndex is -1)"),
        };
    }
}
