using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace InstantMeshesCommon
{
  /// <summary>
  /// Output mesh formats supported by InstantMeshes
  /// </summary>
  public enum InstantMeshesFormat
  {
    /// <summary>Triangles (6-RoSy, 6-PoSy)</summary>
    Triangles = 0,
    /// <summary>Quads (2-RoSy, 4-PoSy)</summary>
    Quads2 = 1,
    /// <summary>Quads (4-RoSy, 4-PoSy)</summary>
    Quads4 = 2
  }

  /// <summary>
  /// Settings for one InstantMeshes run
  /// </summary>
  public sealed class InstantMeshesOptions
  {
    /// <summary>Target face count of the output mesh</summary>
    public int FaceCount { get; set; } = 1000;

    /// <summary>Number of smoothing steps</summary>
    public int SmoothIterations { get; set; } = 2;

    /// <summary>Output mesh format</summary>
    public InstantMeshesFormat Format { get; set; } = InstantMeshesFormat.Quads4;

    /// <summary>True for extrinsic, false for intrinsic field smoothing</summary>
    public bool Extrinsic { get; set; } = true;

    /// <summary>Align the field to mesh boundaries</summary>
    public bool AlignBoundaries { get; set; } = true;

    /// <summary>Preserve sharp creases</summary>
    public bool SharpCreases { get; set; } = true;

    /// <summary>Crease angle threshold in degrees</summary>
    public double CreaseAngle { get; set; } = 90.0;

    /// <summary>
    /// Returns null if the options are valid, otherwise a message describing the problem.
    /// </summary>
    public string Validate()
    {
      if (FaceCount < 1)
        return "Face count must be: 1 <= Faces";
      if (SmoothIterations < 0)
        return "Smoothing steps must be: 0 <= Smooth";
      if (!Enum.IsDefined(typeof(InstantMeshesFormat), Format))
        return "Mesh format must be: 0 <= Format <= 2";
      if (!(CreaseAngle >= 0.0 && CreaseAngle <= 180.0))
        return "Crease angle must be: 0 <= Angle <= 180.0";
      return null;
    }
  }

  /// <summary>
  /// Runs InstantMeshes.exe, which is installed next to this assembly.
  /// </summary>
  public static class InstantMeshesRunner
  {
    /// <summary>
    /// Full path to InstantMeshes.exe
    /// </summary>
    public static string ExecutablePath
    {
      get
      {
        string directory = Path.GetDirectoryName(typeof(InstantMeshesRunner).Assembly.Location);
        return Path.Combine(directory ?? string.Empty, "InstantMeshes.exe");
      }
    }

    /// <summary>
    /// Remeshes a mesh with InstantMeshes.
    /// </summary>
    /// <param name="mesh">The mesh to remesh. It is not modified.</param>
    /// <param name="options">InstantMeshes settings.</param>
    /// <param name="isCancelled">
    /// Polled while InstantMeshes runs. Returning true kills the process. May be null.
    /// </param>
    /// <param name="error">Set to a message describing the failure when null is returned.</param>
    /// <returns>The new mesh, or null on failure.</returns>
    public static Mesh Remesh(Mesh mesh, InstantMeshesOptions options, Func<bool> isCancelled, out string error)
    {
      if (null == mesh || !mesh.IsValid)
      {
        error = "Input mesh is invalid";
        return null;
      }

      error = options.Validate();
      if (null != error)
        return null;

      string im_path = ExecutablePath;
      if (!File.Exists(im_path))
      {
        error = "InstantMeshes.exe not found";
        return null;
      }

      string tmp_path = Path.GetTempPath();
      Guid guid = Guid.NewGuid();
      string temp_obj = Path.Combine(tmp_path, $"input{guid}.obj");
      string temp_output_obj = Path.Combine(tmp_path, $"output{guid}.obj");

      try
      {
        try
        {
          FileObj.Write(new[] { mesh }, temp_obj);
        }
        catch (Exception e)
        {
          error = $"Failed to write input mesh: {e.Message}";
          return null;
        }

        error = Run(im_path, BuildArguments(options, temp_obj, temp_output_obj), isCancelled);
        if (null != error)
          return null;

        if (!File.Exists(temp_output_obj))
        {
          error = "Mesh creation failed";
          return null;
        }

        Mesh imported_mesh;
        try
        {
          imported_mesh = FileObj.Read(temp_output_obj);
        }
        catch (Exception e)
        {
          error = $"Failed to read mesh from InstantMeshes: {e.Message}";
          return null;
        }
        if (imported_mesh.Faces.Count == 0)
        {
          error = "An empty mesh was returned from InstantMeshes";
          return null;
        }

        if (imported_mesh.SolidOrientation() == -1)
          imported_mesh.Flip(true, true, true);

        return imported_mesh;
      }
      finally
      {
        DeleteFile(temp_obj);
        DeleteFile(temp_output_obj);
      }
    }

    /// <summary>
    /// Builds the InstantMeshes.exe command line.
    /// </summary>
    private static string BuildArguments(InstantMeshesOptions options, string inputPath, string outputPath)
    {
      int rosy, posy;
      switch (options.Format)
      {
        case InstantMeshesFormat.Triangles: rosy = 6; posy = 6; break;
        case InstantMeshesFormat.Quads2: rosy = 2; posy = 4; break;
        default: rosy = 4; posy = 4; break;
      }

      // Pure quad output is a quad-dominant mesh subdivided once,
      // which quadruples the face count, so ask for a quarter as many.
      int face_count = 4 == posy ? Math.Max(1, options.FaceCount / 4) : options.FaceCount;

      // Format numbers with the invariant culture, InstantMeshes cannot parse "22,5"
      string args = FormattableString.Invariant($"\"{inputPath}\" -f {face_count} -r {rosy} -p {posy} -S {options.SmoothIterations}");
      if (!options.Extrinsic)
        args += " -i";
      if (options.AlignBoundaries)
        args += " -b";
      if (options.SharpCreases)
        args += FormattableString.Invariant($" -c {options.CreaseAngle}");
      args += $" -o \"{outputPath}\"";
      return args;
    }

    /// <summary>
    /// Runs InstantMeshes.exe and waits for it to exit, or until isCancelled returns true.
    /// Returns null on success, otherwise a message describing the failure.
    /// </summary>
    private static string Run(string path, string arguments, Func<bool> isCancelled)
    {
      ProcessStartInfo start_info = new ProcessStartInfo()
      {
        FileName = path,
        Arguments = arguments,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        WindowStyle = ProcessWindowStyle.Hidden,
        CreateNoWindow = true
      };

      // InstantMeshes reports argument errors on stdout and runtime errors on stderr.
      // Both streams are read asynchronously so neither pipe can fill up and block it.
      List<string> output = new List<string>();
      List<string> errors = new List<string>();

      using (Process process = new Process { StartInfo = start_info })
      {
        process.OutputDataReceived += (sender, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) output.Add(e.Data); };
        process.ErrorDataReceived += (sender, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) errors.Add(e.Data); };

        try
        {
          process.Start();
        }
        catch (Exception e)
        {
          return $"Failed to start InstantMeshes process: {e.Message}";
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        while (!process.WaitForExit(100))
        {
          if (null != isCancelled && isCancelled())
          {
            try
            {
              process.Kill();
            }
            catch (InvalidOperationException)
            {
              // Already exited
            }
            process.WaitForExit();
            return "InstantMeshes was cancelled";
          }
        }

        // Waits for the asynchronous output handlers to finish
        process.WaitForExit();

        if (0 == process.ExitCode)
          return null;

        string message = errors.Find(IsErrorLine) ?? output.Find(IsErrorLine) ?? (errors.Count > 0 ? errors[0] : null);
        return null != message
          ? $"InstantMeshes failed: {message.Trim()}"
          : $"InstantMeshes failed with exit code {process.ExitCode}";
      }
    }

    private static bool IsErrorLine(string line)
    {
      return line.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// Deletes a temporary file, ignoring failures
    /// </summary>
    private static void DeleteFile(string path)
    {
      try
      {
        if (File.Exists(path))
          File.Delete(path);
      }
      catch (IOException)
      {
      }
      catch (UnauthorizedAccessException)
      {
      }
    }
  }
}
