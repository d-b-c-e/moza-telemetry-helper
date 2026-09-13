namespace MozaTelemetry.App;

public static class AppIcon
{
    public static Icon Load(int size)
    {
        using var stream = typeof(AppIcon).Assembly.GetManifestResourceStream("MozaTelemetry.AppIcon.ico")
            ?? throw new InvalidOperationException("The application icon resource is missing.");
        using var icon = new Icon(stream, new Size(size, size));
        return (Icon)icon.Clone();
    }
}
