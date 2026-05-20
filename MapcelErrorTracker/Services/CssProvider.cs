namespace MapcelErrorTracker.Services;

public class CssProvider
{
    private readonly string _css;

    public CssProvider(IWebHostEnvironment env)
    {
        var cssPath = Path.Combine(env.WebRootPath, "css", "tailwind.css");
        _css = File.Exists(cssPath) ? File.ReadAllText(cssPath) : string.Empty;
    }

    public string Css => _css;
}
