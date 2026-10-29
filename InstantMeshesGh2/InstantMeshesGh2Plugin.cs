using Grasshopper2.Framework;
using Grasshopper2.UI.Icon;
using System;
using System.IO;

namespace InstantMeshesGh2
{
  /// <summary>
  /// Grasshopper 2 plug-in entry point. Exactly one Plugin-derived class is
  /// required; Grasshopper 2 discovers the component types (marked with [IoId])
  /// in this assembly automatically. The plug-in identity (id, name, description
  /// and version) is harvested from the assembly attributes.
  /// </summary>
  public sealed class InstantMeshesGh2Plugin : Plugin
  {
    private static readonly Lazy<IIcon> s_icon = new Lazy<IIcon>(LoadIcon);

    /// <summary>
    /// The InstantMeshes logo, shared by the plug-in and its component.
    /// </summary>
    internal static IIcon InstantMeshesIcon => s_icon.Value;

    private static IIcon LoadIcon()
    {
      using (Stream stream = typeof(InstantMeshesGh2Plugin).Assembly.GetManifestResourceStream("InstantMeshesGh2.icon.png"))
      {
        if (null == stream)
          return null;
        return AbstractIcon.FromBitmap(new[] { new Eto.Drawing.Bitmap(stream) });
      }
    }

    public override IIcon Icon => InstantMeshesIcon;
    public override string Author => "Dale Fugier";
    public override sealed string Copyright => $"Copyright © 2013-{DateTime.UtcNow.Year}, Dale Fugier";
    public override string Contact => "https://github.com/dalefugier/InstantMeshes";
    public override string Website => "https://github.com/dalefugier/InstantMeshes";
    public override sealed string LicenceDescription => DefaultLicence;
    public override sealed string LicenceAgreement => "MIT";
  }
}
