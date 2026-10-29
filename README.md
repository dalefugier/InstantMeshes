# InstantMeshes

[<img width="170" height="166" src="https://github.com/wjakob/instant-meshes/raw/master/resources/icon.png">](https://github.com/wjakob/instant-meshes)

Grasshopper and Grasshopper 2 for Windows plug-ins that use the [Instant Meshes](https://github.com/wjakob/instant-meshes) open source meshing utility.

InstantMeshes is available in Rhino via the [PackageManager](https://www.rhino3d.com/features/package-manager/).

## Prerequisites

The following tools are required to build InstantMeshes:

- [Microsoft Visual Studio 2026](https://visualstudio.microsoft.com/) or the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Rhino 9 for Windows](https://www.rhino3d.com/)

##  Compiling

1. Clone the repository. At a command prompt, enter the following command:
```
git clone https://github.com/dalefugier/InstantMeshes
```
2. Open the `InstantMeshes.sln` solution file in Visual Studio.
3. Press <kbd>F7</kbd>, or click *Build > Build Solution*  to build the solution.

The solution contains three projects, which all build into `bin\net10.0`:

| Project | Output | Description |
|---|---|---|
| `InstantMeshesCommon` | `InstantMeshesCommon.dll` | Shared code: OBJ file I/O and running `InstantMeshes.exe` |
| `InstantMeshesGh` | `InstantMeshes.gha` | Grasshopper plug-in |
| `InstantMeshesGh2` | `InstantMeshesGh2.rhp` | Grasshopper 2 plug-in |

To debug, set `InstantMeshesGh` or `InstantMeshesGh2` as the startup project and press <kbd>F5</kbd>. Rhino starts and opens Grasshopper or Grasshopper 2.

## License
This source code is licensed under the [MIT License](https://github.com/dalefugier/InstantMeshes/blob/master/LICENSE).

[Instant Meshes](https://github.com/wjakob/instant-meshes/blob/master/LICENSE.txt) has its own separate license agreement.

