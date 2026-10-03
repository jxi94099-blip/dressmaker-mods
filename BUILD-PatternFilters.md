# Pattern Part and Direction Filters 1.0.5

Source files extracted unchanged from the ZIP uploaded to Nexus mod 180.

## Build steps

1. Install Dressmaker and its BepInEx 5 loader. Use the same game assembly version as the uploaded build.
2. Create a C# class-library project targeting .NET Framework 4.7.2, named `PatternCategories`.
3. Include `PatternCategories.cs` and `PatternClassifier.cs` in that project.
4. Add references from `Dressmaker_Data/Managed`: `Assembly-CSharp.dll`, `netstandard.dll`, `UnityEngine.CoreModule.dll`, `UnityEngine.UI.dll`, `Unity.TextMeshPro.dll`, `UnityEngine.InputLegacyModule.dll`, `UnityEngine.UIModule.dll`, `UnityEngine.TextRenderingModule.dll`. Resolve any additional Unity assembly references from that same Managed directory.
5. Add `BepInEx/core/BepInEx.dll` and `BepInEx/core/0Harmony.dll` as references.
6. Build the library. Install `PatternCategories.dll` under `BepInEx/plugins/PatternCategories/`.

Game and loader assemblies are external dependencies and are not redistributed in this source bundle. These build instructions were prepared for review; this project setup has not been rebuilt during this upload session. Rebuilding does not guarantee a byte-identical DLL because compiler versions and build settings may differ.

The plugin adds runtime UI filters. AI-assisted development is disclosed on the mod page.
