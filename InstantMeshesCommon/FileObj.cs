using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace InstantMeshesCommon
{
  /// <summary>
  /// Minimal Wavefront OBJ reader and writer for exchanging meshes with InstantMeshes.exe.
  /// Both methods throw on I/O errors or malformed data, so the caller can report what went wrong.
  /// </summary>
  public static class FileObj
  {
    private static readonly char[] s_whitespace = { ' ', '\t' };

    /// <summary>
    /// Writes meshes to an OBJ file. Only vertices and faces are written.
    /// The input meshes are not modified.
    /// </summary>
    /// <remarks>
    /// Vertices are written once per topology vertex, so faces that meet at an unwelded
    /// edge still share vertices. InstantMeshes treats every unshared vertex as a boundary,
    /// so an unwelded mesh would otherwise be remeshed as thousands of separate faces.
    /// </remarks>
    public static void Write(IEnumerable<Mesh> meshes, string filename)
    {
      using (StreamWriter sw = new StreamWriter(filename))
      {
        sw.NewLine = "\n";
        sw.WriteLine("# OBJ");

        // OBJ indices are 1-based and global across the file
        long vertex_offset = 1;
        foreach (Mesh m in meshes)
        {
          if (null == m)
            continue;

          Rhino.Geometry.Collections.MeshTopologyVertexList tv = m.TopologyVertices;
          int tvc = tv.Count;
          for (int i = 0; i < tvc; i++)
          {
            // Use a mesh vertex for its double precision location
            Point3d p = m.Vertices.Point3dAt(tv.MeshVertexIndices(i)[0]);
            sw.WriteLine($"v {Format(p.X)} {Format(p.Y)} {Format(p.Z)}");
          }

          int vc = m.Vertices.Count;
          int fc = m.Faces.Count;
          List<long> corners = new List<long>(4);
          for (int i = 0; i < fc; i++)
          {
            MeshFace f = m.Faces[i];
            if (!f.IsValid(vc))
              continue;

            // Skip corners that collapse onto the same topology vertex
            corners.Clear();
            int count = f.IsQuad ? 4 : 3;
            for (int j = 0; j < count; j++)
            {
              long index = tv.TopologyVertexIndex(f[j]) + vertex_offset;
              if (!corners.Contains(index))
                corners.Add(index);
            }
            if (corners.Count >= 3)
              sw.WriteLine("f " + string.Join(" ", corners));
          }

          vertex_offset += tvc;
        }
      }
    }

    /// <summary>
    /// Reads an OBJ file into a single mesh. Supports "v", "vn" and "f" records, including
    /// v/vt/vn face tokens, negative (relative) indices, polygons with more than four sides
    /// and line continuations. All other records are ignored.
    /// </summary>
    public static Mesh Read(string filename)
    {
      List<Point3d> vertices = new List<Point3d>();
      List<Vector3f> normals = new List<Vector3f>();
      List<int[]> faces = new List<int[]>();

      using (StreamReader sr = new StreamReader(filename))
      {
        int line_number = 0;
        string line;
        while ((line = sr.ReadLine()) != null)
        {
          line_number++;

          // A trailing backslash continues the record on the next line
          while (line.EndsWith("\\"))
          {
            string next = sr.ReadLine();
            line = line.Substring(0, line.Length - 1);
            if (null == next)
              break;
            line_number++;
            line += " " + next;
          }

          int comment = line.IndexOf('#');
          if (comment >= 0)
            line = line.Substring(0, comment);

          string[] tokens = line.Split(s_whitespace, StringSplitOptions.RemoveEmptyEntries);
          if (tokens.Length == 0)
            continue;

          switch (tokens[0])
          {
            case "v":
              vertices.Add(new Point3d(
                ParseDouble(tokens, 1, line_number),
                ParseDouble(tokens, 2, line_number),
                ParseDouble(tokens, 3, line_number)));
              break;

            case "vn":
              normals.Add(new Vector3f(
                (float)ParseDouble(tokens, 1, line_number),
                (float)ParseDouble(tokens, 2, line_number),
                (float)ParseDouble(tokens, 3, line_number)));
              break;

            case "f":
              if (tokens.Length < 4)
                throw new FormatException($"Line {line_number}: face has fewer than three vertices.");
              int[] face = new int[tokens.Length - 1];
              for (int i = 1; i < tokens.Length; i++)
                face[i - 1] = ParseVertexIndex(tokens[i], vertices.Count, line_number);
              faces.Add(face);
              break;
          }
        }
      }

      Mesh mesh = new Mesh();
      foreach (Point3d p in vertices)
        mesh.Vertices.Add(p);

      foreach (int[] face in faces)
      {
        foreach (int index in face)
        {
          if (index < 0 || index >= vertices.Count)
            throw new FormatException($"Face references vertex {index + 1}, but the file has {vertices.Count} vertices.");
        }

        if (face.Length == 3)
          mesh.Faces.AddFace(face[0], face[1], face[2]);
        else if (face.Length == 4)
          mesh.Faces.AddFace(face[0], face[1], face[2], face[3]);
        else
        {
          // Fan-triangulate polygons
          for (int i = 1; i < face.Length - 1; i++)
            mesh.Faces.AddFace(face[0], face[i], face[i + 1]);
        }
      }

      // Use the file's normals only if there is one per vertex
      if (normals.Count == vertices.Count)
        mesh.Normals.AddRange(normals.ToArray());

      mesh.Faces.CullDegenerateFaces();
      mesh.Compact();

      if (mesh.Normals.Count != mesh.Vertices.Count)
        mesh.Normals.ComputeNormals();

      // An angle of PI radians welds every shared edge
      if (mesh.IsValid)
        mesh.Weld(Math.PI);

      return mesh;
    }

    private static string Format(double value)
    {
      return value.ToString("R", CultureInfo.InvariantCulture);
    }

    private static double ParseDouble(string[] tokens, int index, int lineNumber)
    {
      if (index >= tokens.Length)
        throw new FormatException($"Line {lineNumber}: expected {index} values after '{tokens[0]}'.");
      if (!double.TryParse(tokens[index], NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
        throw new FormatException($"Line {lineNumber}: '{tokens[index]}' is not a number.");
      return value;
    }

    /// <summary>
    /// Parses the vertex index from a face token ("v", "v/vt", "v//vn" or "v/vt/vn")
    /// and returns it as a 0-based index. Negative indices are relative to the vertices read so far.
    /// </summary>
    private static int ParseVertexIndex(string token, int vertexCount, int lineNumber)
    {
      int slash = token.IndexOf('/');
      string value = slash >= 0 ? token.Substring(0, slash) : token;
      if (!int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int index) || 0 == index)
        throw new FormatException($"Line {lineNumber}: '{token}' is not a valid face vertex.");
      return index > 0 ? index - 1 : vertexCount + index;
    }
  }
}
