using Grasshopper.Kernel;
using System;
using System.Drawing;
using System.Reflection;

namespace InstantMeshes
{
  public class InstantMeshesInfo : GH_AssemblyInfo
  {
    public override string Name => "InstantMeshes";
    public override Bitmap Icon
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
    public override string Description => "Field-aligned mesh generator for Grasshopper";
    public override Guid Id => new Guid("06cbea6f-9c7a-46e1-b0e2-fc0f114ecfd0");
    public override string AuthorName => "Dale Fugier";
    public override string AuthorContact => "https://github.com/dalefugier/InstantMeshes";
    public override string Version => "9.0.26300.9401";
  }
}
