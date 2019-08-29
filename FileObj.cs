using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Rhino;
using Rhino.Geometry;

namespace InstantMeshes
{
  internal static class FileObj
  {
    public static void Write(IEnumerable<Mesh> meshes, string filename)
    {
      try
      {
        var sw = new StreamWriter(filename);
        // Write header
        sw.WriteLine("#OBJ");
        // Write mesh geometry
        long vert_counter = 1;
        foreach (var m in meshes)
        {
          // Write all Verticies: v 1 2 3
          var vc = m.Vertices.Count;
          for (var i = 0; i < vc; i++)
            sw.WriteLine($"v {m.Vertices[i].X.ToString(CultureInfo.InvariantCulture)} {m.Vertices[i].Y.ToString(CultureInfo.InvariantCulture)} {m.Vertices[i].Z.ToString(CultureInfo.InvariantCulture)}");
          // Write All vertex normals: vn 1 2 3
          var nc = m.Normals.Count;
          m.Normals.UnitizeNormals();
          for (var i = 0; i < nc; i++)
            sw.WriteLine($"vn {m.Normals[i].X.ToString(CultureInfo.InvariantCulture)} {m.Normals[i].Y.ToString(CultureInfo.InvariantCulture)} {m.Normals[i].Z.ToString(CultureInfo.InvariantCulture)}");
          // Write All Faces: f 1 2 3 / 4
          var fc = m.Faces.Count;
          for (var i = 0; i < fc; i++)
          {
            if (m.Faces[i].IsTriangle)
              sw.WriteLine(
                  $"f {m.Faces[i].A + vert_counter} {m.Faces[i].B + vert_counter} {m.Faces[i].C + vert_counter}");
            if (m.Faces[i].IsQuad)
              sw.WriteLine(
                  $"f {m.Faces[i].A + vert_counter} {m.Faces[i].B + vert_counter} {m.Faces[i].C + vert_counter} {m.Faces[i].D + vert_counter}");
          }
          vert_counter += m.Vertices.Count;
        }
        sw.Close();
      }
      catch (Exception e)
      {
        RhinoApp.WriteLine(e.Message);
      }
    }

    public static Mesh Read(string filename)
    {
      try
      {
        using (var sr = new StreamReader(filename))
        {
          var obj_file = sr.ReadToEnd();
          var obj = new Mesh();

          #region ADD NORMALS
          //n vector normals
          const string normal_reg = @"vn( +[\d|\.|\+|\-|e]+)( [\d|\.|\+|\-|e]+)( [\d|\.|\+|\-|e]+)";
          foreach (Match m in Regex.Matches(obj_file, normal_reg))
          {
            var n = new Vector3d(double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture),
            double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture));
            if (n.IsValid)
              obj.Normals.Add(n);
          }
          #endregion

          #region ADD VERTS
          //v add verts
          const string vertex_reg = @"v( +[\d|\.|\+|\-|e]+)( [\d|\.|\+|\-|e]+)( [\d|\.|\+|\-|e]+)";
          var verts = Regex.Matches(obj_file, vertex_reg);
          if (verts.Count > 0)
          {
            foreach (Match m in verts)
            {
              var vc = obj.Vertices.Count;
              var p = new Point3d(double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture),
              double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture),
              double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture));
              if (p.IsValid)
                obj.Vertices.SetVertex(vc, p.X, p.Y, p.Z);

            }
            //obj.Vertices.CombineIdentical(true, true);

          }
          #endregion

          #region ADD FACES
          // HUNT FOR FACES

          // f vertex vertex vertex eg f 1 2 3
          const string fvvv_reg = @"f( +[\d]+)( [\d]+)( [\d]+)( [\d]+)?";
          var fvvv = Regex.Matches(obj_file, fvvv_reg);

          if (fvvv.Count > 0)
          {
            foreach (Match m in fvvv)
            {
              var mf = new MeshFace
              {
                A = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) - 1,
                B = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) - 1,
                C = int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture) - 1
              };
              if (m.Groups.Count == 5)
                mf.D = int.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture) - 1;
              obj.Faces.AddFace(mf);
            }
          }
          else
          {
            //f quad based face structure v//n v//n v//n v//n
            const string fvnq_reg = @"(^f )[\d].+";
            const RegexOptions options = RegexOptions.Multiline;
            var fvnq = Regex.Matches(obj_file, fvnq_reg, options);
            if (fvnq.Count > 0)
            {
              foreach (Match m in fvnq)
              {
                const string faces_req = @" [\d]+";
                var faces = Regex.Matches(m.Value, faces_req);

                if (faces.Count <= 0)
                  continue;

                var mf = new MeshFace
                {
                  A = int.Parse(faces[0].Value, CultureInfo.InvariantCulture) - 1,
                  B = int.Parse(faces[1].Value, CultureInfo.InvariantCulture) - 1,
                  C = int.Parse(faces[2].Value, CultureInfo.InvariantCulture) - 1
                };

                if (faces.Count == 4)
                {
                  mf.D = int.Parse(faces[3].Value, CultureInfo.InvariantCulture) - 1;
                }
                else
                {
                  mf.D = mf.C;
                }

                obj.Faces.AddFace(mf);

              }

            }
          }
          #endregion


          sr.Close();

          obj.Faces.CullDegenerateFaces();
          obj.Compact();


          if (obj.Normals.Count == 0)
            obj.Normals.ComputeNormals();

          if (obj.IsValid)
            obj.Weld(22.5);

          return obj;
        }
      }
      catch
      {
        return null;
      }
    }
  }
}


