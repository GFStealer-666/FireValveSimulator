# Edusim Module Exporter

An editor tool that turns a standalone simulation project into an Edusim launcher module.

> **Requires `com.unity.addressables` 1.22.3** in the target project (Window → Package Manager
> → Unity Registry → Addressables). Installed as a UPM folder, the dependency resolves
> automatically; installed via .unitypackage it does not — without Addressables the
> Edusim → Module Exporter menu opens an install-hint window instead of the tool.

Install it **in the module project** (not the launcher): copy this folder into that project's
`Packages/` directory. A menu appears: **Edusim → Module Exporter**.

## Catalog modes (v0.2)

| Mode | What it produces | Works with today's launcher? |
|---|---|---|
| **Shared catalog** (default) | `ToLauncher/` — scripts + content `.unitypackage`s + `module_manifest.json`, all imported into the launcher, which builds the content into its own single shared catalog | **Yes** |
| **Self-hosted catalog** (preview) | `Upload/ServerData/{moduleId}/` — bundles + a per-module remote catalog built in this project | **No** — needs a launcher-side per-module catalog loader that does not exist yet |

Everything lands under one folder per export: `ModuleExport/{moduleId}_{version}/`.

## What it produces (shared mode)

| Deliverable | Goes to | Becomes |
|---|---|---|
| `{moduleId}_scripts_{version}.unitypackage` | the launcher maintainer | imported into the launcher project → compiled into the APK |
| `{moduleId}_content_{version}.unitypackage` | the launcher maintainer | scenes + assets imported into the launcher, built into its Addressables catalog there |
| `module_manifest.json` | the launcher maintainer | the values for the module's `ModuleDefinitionSO` and Addressables entries (scene addresses, label, sdkType) |

## The flow

1. **Identity** — module id drives every derived name; the namespace auto-derives from it
   (`firedrill` → `Edusim.Modules.Firedrill`).
2. **Scenes** — list every scene of the module; row 0 is what the launcher's Play button loads.
   Addresses follow the launcher convention: main = `{moduleId}_scene`, extras =
   `{moduleId}_scene_{variant}`. All scenes share one label — one install fetches all of them.
3. **Detect scripts** — finds the union of the scenes' transitive script dependencies and the
   optional scripts folder. Third-party-looking scripts default to excluded; asmdefs are flagged
   (scripts under one won't merge into the launcher's Assembly-CSharp).
   **Apply namespace** wraps the selected namespace-less scripts in the module namespace in
   place — safe for scene references (Unity binds scripts by GUID).
4. **Validate** — checks Unity + package versions against the launcher toolchain spec
   (reporting installed vs required), scans scripts for standalone-app habits
   (`PlayerPrefs.DeleteAll`, `Application.Quit`, build-index scene loads), and sanity-checks
   every scene (exactly one rig, has a `ReturnToHubButton`). A **Fix package versions**
   button rewrites `Packages/manifest.json` to the spec versions (backup kept). The Unity
   editor version itself cannot be fixed by the tool — install 2022.3.62f3 via Unity Hub
   and open the project with it *first*; only then does package alignment resolve cleanly.
5. **Export everything** — validate → scripts package → content package (or Addressables build
   in self-hosted mode) → manifest. `.meta` files travel with both packages, which preserves
   GUIDs — that is what lets content built in the launcher bind to the imported scripts.

## Launcher side (once per module, shared mode)

1. Import the scripts `.unitypackage`, then the content `.unitypackage`, rebuild the APK.
2. Follow `Docs/Adding-A-Module.md`: create the `Remote_<Name>` Addressables group, set the
   scene addresses + label from `module_manifest.json`, create a `ModuleDefinitionSO`
   (sdkType, sceneAddress, downloadLabel come straight from the manifest), add it to
   `DefaultCatalog.asset`.
3. Build Addressables content in the launcher, upload `ServerData/<Platform>/` to the server.
4. Union-merge the module's tags/layers/capabilities into the launcher's ProjectSettings.

## Rules this cannot bend

- **Code changes always mean a new launcher APK.** Content-only changes are a re-build + re-upload.
- **The toolchain spec is law.** Bundles built on a different Unity version will not load in the launcher.
- The module scene must ship its own complete rig (OVR *or* XR Origin, never both) and a
  **wired** `ReturnToHubButton`.
- The content server is fixed for the fleet (`https://poc-edusim.cheesetech.cloud/api/download`);
  override it only for private test servers.
