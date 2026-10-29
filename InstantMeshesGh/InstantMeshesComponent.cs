using Grasshopper.Kernel;
using InstantMeshesCommon;
using Rhino.Geometry;
using System;
using System.Drawing;
using System.Reflection;

namespace InstantMeshes
{
  public class InstantMeshesComponent : GH_Component
  {
    public InstantMeshesComponent()
      : base("InstantMeshes", "IM", "Construct a field-aligned mesh.", "Mesh", "Triangulation")
    {
    }

    protected override void RegisterInputParams(GH_Component.GH_InputParamManager args)
    {
      args.AddMeshParameter("Mesh", "M", "Mesh", GH_ParamAccess.item);
      args.AddIntegerParameter("Faces", "F", "Target face count", GH_ParamAccess.item, 1000);
      args.AddIntegerParameter("Smooth", "S", "Smoothing steps", GH_ParamAccess.item, 2);
      args.AddIntegerParameter("Format", "T", "Mesh format: 0 = Triangles (6-RoSy, 6-PoSy), 1 = Quads (2-RoSy, 4-PoSy), 2 = Quads (4-RoSy,4-PoSy)", GH_ParamAccess.item, 2);
      args.AddBooleanParameter("Extrinsic", "E", "Extrinsic or intrinsic", GH_ParamAccess.item, true);
      args.AddBooleanParameter("Align", "A", "Align to boundaries", GH_ParamAccess.item, true);
      args.AddBooleanParameter("Creases", "C", "Sharp creases", GH_ParamAccess.item, true);
      args.AddNumberParameter("Angle", "D", "Crease angle in degrees", GH_ParamAccess.item, 90.0);

      //args[7].Optional = true;
    }

    protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager args)
    {
      args.AddMeshParameter("Mesh", "M", "Instant mesh", GH_ParamAccess.item);
    }

    protected override void SolveInstance(IGH_DataAccess access)
    {
      Mesh mesh = new Mesh();
      int target_face_count = 1000;
      int smooth_iterations = 2;
      int mesh_format = 2;
      bool extrinsic = true;
      bool align_boundaries = true;
      bool sharp_creases = true;
      double crease_angle = 90.0;

      if (!access.GetData(0, ref mesh)) return;
      if (!access.GetData(1, ref target_face_count)) return;
      if (!access.GetData(2, ref smooth_iterations)) return;
      if (!access.GetData(3, ref mesh_format)) return;
      if (!access.GetData(4, ref extrinsic)) return;
      if (!access.GetData(5, ref align_boundaries)) return;
      if (!access.GetData(6, ref sharp_creases)) return;
      if (!access.GetData(7, ref crease_angle)) return;

      InstantMeshesOptions options = new InstantMeshesOptions
      {
        FaceCount = target_face_count,
        SmoothIterations = smooth_iterations,
        Format = (InstantMeshesFormat)mesh_format,
        Extrinsic = extrinsic,
        AlignBoundaries = align_boundaries,
        SharpCreases = sharp_creases,
        CreaseAngle = crease_angle
      };

      // Pressing Escape cancels a long-running InstantMeshes process
      Mesh imported_mesh = InstantMeshesRunner.Remesh(mesh, options, GH_Document.IsEscapeKeyDown, out string error);
      if (null == imported_mesh)
      {
        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, error);
        return;
      }
      if (!imported_mesh.IsValid)
        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "InstantMeshes returned an invalid mesh");

      access.SetData(0, imported_mesh);
    }

    public override GH_Exposure Exposure => GH_Exposure.quarternary;

    protected override Bitmap Icon
    {
      get
      {
        const string resource = "InstantMeshes.Resources.InstantMeshes.ico";
        Size size = new Size(24, 24);
        Assembly assembly = Assembly.GetExecutingAssembly();
        Icon icon = Rhino.UI.DrawingUtilities.IconFromResource(resource, size, assembly);
        return icon.ToBitmap();
      }
    }

    public override Guid ComponentGuid => new Guid("30392475-f7c9-4317-b8e8-4480cd13cdfd");
  }
}
