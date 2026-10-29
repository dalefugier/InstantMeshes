using Grasshopper2.Components;
using Grasshopper2.Types.Numeric;
using Grasshopper2.UI;
using Grasshopper2.UI.Icon;
using GrasshopperIO;
using InstantMeshesCommon;
using Rhino.Geometry;
using System.Threading;

namespace InstantMeshesGh2.Components
{
  /// <summary>
  /// Output mesh formats, with names and descriptions for the Grasshopper 2 UI
  /// </summary>
  public enum MeshFormat
  {
    [UiName("Triangles"), UiInfo("Triangle mesh (6-RoSy, 6-PoSy).")]
    Triangles = (int)InstantMeshesFormat.Triangles,
    [UiName("Quads (2-RoSy)"), UiInfo("Quad mesh (2-RoSy, 4-PoSy).")]
    Quads2 = (int)InstantMeshesFormat.Quads2,
    [UiName("Quads (4-RoSy)"), UiInfo("Quad mesh (4-RoSy, 4-PoSy).")]
    Quads4 = (int)InstantMeshesFormat.Quads4
  }

  [IoId("892f4edc-2466-4eb8-af3f-2985fe379fbe"), Grasshopper2.Author("Dale Fugier")]
  public sealed class InstantMeshesComponent : Component
  {
    /// <summary>
    /// Initializes a new instance of the InstantMeshesComponent class.
    /// </summary>
    public InstantMeshesComponent()
      : base(new Nomen("InstantMeshes", "Construct a field-aligned mesh.", "Mesh", "Topology", 10, Rank.Normal))
    {
    }

    /// <summary>
    /// Deserialization constructor.
    /// </summary>
    public InstantMeshesComponent(IReader reader) : base(reader)
    {
    }

    protected override void AddInputs(InputAdder inputs)
    {
      inputs.AddMesh("Mesh", "M", "Mesh to remesh");
      inputs.AddInteger("Faces", "F", "Target face count").Set(1000);
      inputs.AddInteger("Smooth", "S", "Smoothing steps").Set(2);
      inputs.AddEnum("Format", "T", "Mesh format", MeshFormat.Quads4);
      inputs.AddBoolean("Extrinsic", "E", "Extrinsic or intrinsic").Set(true);
      inputs.AddBoolean("Align", "A", "Align to boundaries").Set(true);
      inputs.AddBoolean("Creases", "C", "Sharp creases").Set(true);
      inputs.AddAngle("Angle", "D", "Crease angle").Set(Angle.FromDegrees(90.0));
    }

    protected override void AddOutputs(OutputAdder outputs)
    {
      outputs.AddMesh("Mesh", "M", "Instant mesh");
    }

    protected override void Process(IDataAccess access)
    {
      access.GetItem(0, out Mesh mesh);
      access.GetItem(1, out int face_count);
      access.GetItem(2, out int smooth_iterations);
      access.GetItem(3, out MeshFormat format);
      access.GetItem(4, out bool extrinsic);
      access.GetItem(5, out bool align_boundaries);
      access.GetItem(6, out bool sharp_creases);
      access.GetItem(7, out Angle crease_angle);

      if (null == mesh || !mesh.IsValid)
      {
        access.AddError("Invalid mesh", "The input mesh is not valid.");
        return;
      }

      InstantMeshesOptions options = new InstantMeshesOptions
      {
        FaceCount = face_count,
        SmoothIterations = smooth_iterations,
        Format = (InstantMeshesFormat)format,
        Extrinsic = extrinsic,
        AlignBoundaries = align_boundaries,
        SharpCreases = sharp_creases,
        CreaseAngle = crease_angle.Degrees
      };

      string error = options.Validate();
      if (null != error)
      {
        access.AddError("Invalid input", error);
        return;
      }

      // Grasshopper 2 cancels the solution when an input changes,
      // which kills a long-running InstantMeshes process.
      CancellationToken token = access.Solution.Token;
      Mesh imported_mesh = InstantMeshesRunner.Remesh(mesh, options, () => token.IsCancellationRequested, out error);
      if (null == imported_mesh)
      {
        if (!token.IsCancellationRequested)
          access.AddError("InstantMeshes failed", error);
        return;
      }
      if (!imported_mesh.IsValid)
        access.AddWarning("Invalid mesh", "InstantMeshes returned an invalid mesh.");

      access.SetItem(0, imported_mesh);
    }

    protected override IIcon IconInternal => InstantMeshesGh2Plugin.InstantMeshesIcon;
  }
}
