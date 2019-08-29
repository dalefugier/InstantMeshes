using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using Grasshopper;
using Grasshopper.Kernel;
using Rhino.Geometry;

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
      args.AddIntegerParameter("Format", "F", "Mesh format: 0 = Triangles (6-RoSy, 6-PoSy), 1 = Quads (2-RoSy, 4-PoSy), 2 = Quads (4-RoSy,4-PoSy)", GH_ParamAccess.item, 2);
      args.AddBooleanParameter("Extrinsic", "E", "Extrinsic or intrinsic", GH_ParamAccess.item, true);
      args.AddBooleanParameter("Align", "A", "Align to boundaries", GH_ParamAccess.item, true);
      args.AddBooleanParameter("Creases", "C", "Sharp creases", GH_ParamAccess.item, true);
      args.AddNumberParameter("Angle", "A", "Crease angle in degrees", GH_ParamAccess.item, 90.0);

      //args[7].Optional = true;
    }

    protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager args)
    {
      args.AddMeshParameter("Mesh", "M", "Instant mesh", GH_ParamAccess.item);
    }

    protected override void SolveInstance(IGH_DataAccess access)
    {
      #region Process input

      var mesh = new Mesh();
      var target_face_count = 1000;
      var smooth_iterations = 2;
      var mesh_format = 2;
      var extrinsic = true;
      var align_boundaries = true;
      var sharp_creases = true;
      var crease_angle = 90.0;

      if (!access.GetData(0, ref mesh)) return;
      if (!access.GetData(1, ref target_face_count)) return;
      if (!access.GetData(2, ref smooth_iterations)) return;
      if (!access.GetData(3, ref mesh_format)) return;
      if (!access.GetData(4, ref extrinsic)) return;
      if (!access.GetData(5, ref align_boundaries)) return;
      if (!access.GetData(6, ref sharp_creases)) return;
      if (!access.GetData(7, ref crease_angle)) return;

      if (!mesh.IsValid)
      {
        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Input mesh is invalid");
        return;
      }
      if (mesh_format < 0 || mesh_format > 2)
      {
        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Mesh format must be: 0 <= Format <= 2");
        return;
      }
      if (crease_angle < 0.0 || crease_angle > 180.0)
      {
        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Crease angle must be: 0 < Angle < 180.0");
        return;
      }

      #endregion


      #region Setup paths

      
      var gha_path = AssemblyDirectory;
      var im_path = Path.Combine(gha_path, "InstantMeshes.exe");
      if (!File.Exists(im_path))
      {
        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "InstantMeshes.exe not found");
        return;
      }

      var tmp_path = Path.GetTempPath();
      var guid = Guid.NewGuid();

      var temp_obj = Path.Combine(tmp_path, $"input{guid}.obj");
      if (File.Exists(temp_obj))
        File.Delete(temp_obj);

      var temp_output_obj = Path.Combine(tmp_path, $"output{guid}.obj");
      if (File.Exists(temp_output_obj))
        File.Delete(temp_output_obj);

      FileObj.Write(new[] { mesh }, temp_obj);

      #endregion


      #region Configure and run InstantMeshes

      var mx = 2;
      if (mesh_format == 0)
        mx = 6;
      else if (mesh_format == 1)
        mx = 2;
      else if (mesh_format == 2)
        mx = 4;

      var my = 4;
      if (mesh_format == 0)
        my = 6;
      else if (mesh_format == 1 || mesh_format == 2)
        my = 4;

      var b = align_boundaries == true ? "-b" : string.Empty;
      var i = extrinsic == true ? string.Empty : "-i";
      var c = sharp_creases == true ? $"-c {crease_angle}" : string.Empty;

      var args = $"\"{temp_obj}\" -f {target_face_count} -p {my} -r {mx} {i} {c} {b} -S {smooth_iterations} -o \"{temp_output_obj}\"";

      var start_info = new ProcessStartInfo()
      {
        FileName = im_path,
        Arguments = args,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        WindowStyle = ProcessWindowStyle.Hidden,
        CreateNoWindow = true
      };

      var process = new Process
      {
        StartInfo = start_info,
        EnableRaisingEvents = true
      };
      try
      {
        process.Start();
      }
      catch
      {
        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Failed to start InstantMeshes process");
        return;

      }

      var s_out = process.StandardOutput;
      var s_err = process.StandardError;
      try
      {
        string str;
        while ((str = s_out.ReadLine()) != null && !s_out.EndOfStream)
        {
          s_out.BaseStream.Flush();
        }
        while ((str = s_err.ReadLine()) != null && !s_err.EndOfStream)
        {
          s_err.BaseStream.Flush();
        }

      }
      finally
      {
        s_out.Close();
        s_err.Close();
      }

      #endregion


      #region Return meshes from InstantMeshes

      if (!File.Exists(temp_output_obj))
      {
        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Mesh creation failed");
        return;
      }

      var retries = 0;
      while (IsFileLocked(new FileInfo(temp_output_obj)) || retries >= 4)
      {
        Thread.Sleep(500);
        retries++;
      }

      var imported_mesh = FileObj.Read(temp_output_obj);
      if (imported_mesh == null)
      {
        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "An invalid mesh was returned from InstantMeshes");
        return;
      }

      if (imported_mesh.SolidOrientation() == -1)
        imported_mesh.Flip(true, true, true);

      #endregion


      if (File.Exists(temp_output_obj))
        File.Delete(temp_output_obj);

      if (File.Exists(temp_obj))
        File.Delete(temp_obj);

      access.SetData(0, imported_mesh);
    }

    /// <summary>
    /// Returns the path to this assembly
    /// </summary>
    private string AssemblyDirectory
    {
      get
      {
        var info = Instances.ComponentServer.FindAssemblyByObject(ComponentGuid);
        return (null != info) ? Path.GetDirectoryName(info.Location) : null;
      }
    }

    /// <summary>
    /// Returns true if the specified file is locked by another process
    /// </summary>
    private bool IsFileLocked(FileInfo file)
    {
      FileStream stream = null;
      try
      {
        stream = file.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None);
      }
      catch (IOException)
      {
        return true;
      }
      finally
      {
        stream?.Close();
      }
      return false;
    }


    public override GH_Exposure Exposure => GH_Exposure.quarternary;

    protected override Bitmap Icon
    {
      get
      {
        const string resource = "InstantMeshes.InstantMeshes.ico";
        var size = new Size(24, 24);
        var assembly = Assembly.GetExecutingAssembly();
        var icon = Rhino.UI.DrawingUtilities.IconFromResource(resource, size, assembly);
        return icon.ToBitmap();
      }
    }

    public override Guid ComponentGuid => new Guid("30392475-f7c9-4317-b8e8-4480cd13cdfd");
  }
}
