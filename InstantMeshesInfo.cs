using System;
using System.Drawing;
using System.Reflection;
using Grasshopper.Kernel;

namespace InstantMeshes
{
  public class InstantMeshesInfo : GH_AssemblyInfo
  {
    public override string Name => "InstantMeshes";
    public override Bitmap Icon
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
    public override string Description => "Field-aligned mesh generator";
    public override Guid Id => new Guid("06cbea6f-9c7a-46e1-b0e2-fc0f114ecfd0");
    public override string AuthorName => "Wenzel Jakob";
    public override string AuthorContact => "https://github.com/wjakob/instant-meshes";
  }
}
