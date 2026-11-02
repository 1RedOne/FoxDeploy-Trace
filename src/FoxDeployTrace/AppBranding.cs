namespace FoxDeployTrace;

internal static class AppBranding
{
    public const string BlogUrl = "https://www.foxdeploy.com";
    public const string ProjectUrl = "https://github.com/1RedOne/FoxDeploy-Trace";
    public const string Copyright = "Copyright (c) 2026 FoxDeploy / Stephen Owen";

    public static Icon LoadIcon()
    {
        using var stream = typeof(AppBranding).Assembly.GetManifestResourceStream("FoxDeployTrace.App.ico")
            ?? throw new InvalidOperationException("The embedded FDTrace icon is missing.");
        using var icon = new Icon(stream, new Size(32, 32));
        return (Icon)icon.Clone();
    }

    public static Bitmap LoadLogo()
    {
        using var stream = typeof(AppBranding).Assembly.GetManifestResourceStream("FoxDeployTrace.Logo.png")
            ?? throw new InvalidOperationException("The embedded FDTrace logo is missing.");
        using var image = Image.FromStream(stream);
        return new Bitmap(image);
    }
}
