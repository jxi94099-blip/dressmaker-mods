# One-Key Fabric Placement on Mannequin 1.3.0

Source extracted unchanged from the ZIP uploaded to Nexus mod 181.

## Build steps

1. Install Dressmaker and its BepInEx 5 loader. Use the same game assembly version as the uploaded build.
2. Create a C# class-library project targeting .NET Framework 4.7.2, named `OneKeyMannequin`.
3. Include `OneKeyMannequin.cs`.
4. Add references from `Dressmaker_Data/Managed`: `Assembly-CSharp.dll`, `netstandard.dll`, `UnityEngine.CoreModule.dll`, `UnityEngine.UI.dll`, `UnityEngine.InputLegacyModule.dll`, `UnityEngine.UIModule.dll`. Resolve any additional Unity assembly references from the same Managed directory.
5. Add `BepInEx/core/BepInEx.dll` and `BepInEx/core/0Harmony.dll` as references.
6. Build the library. Install `OneKeyMannequin.dll` under `BepInEx/plugins/OneKeyMannequin/`.

Game and loader assemblies are external dependencies and are not redistributed in this source bundle. These build instructions were prepared for review; this project setup has not been rebuilt during this upload session. Rebuilding does not guarantee a byte-identical DLL because compiler versions and build settings may differ.

Default hotkeys: NUMPAD * placement, NUMPAD - cutting/recovery, NUMPAD / pattern recall, NUMPAD + undo cutting under the cursor. AI-assisted development is disclosed on the mod page.
