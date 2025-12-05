using System.Reflection;

namespace Cascade.CodeGen.Templates;

/// <summary>
/// Simple in-memory registry storing template content by name.
/// Loads embedded Scriban templates at startup.
/// </summary>
public sealed class TemplateRegistry
{
    private readonly Dictionary<string, string> _templates = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _sync = new();

    public TemplateRegistry()
    {
    }

    public TemplateRegistry RegisterFromAssembly(Assembly assembly, string resourcePrefix)
    {
        if (assembly is null)
        {
            throw new ArgumentNullException(nameof(assembly));
        }

        var resources = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(resourcePrefix, StringComparison.OrdinalIgnoreCase) && name.EndsWith(".sbn", StringComparison.OrdinalIgnoreCase));

        foreach (var resource in resources)
        {
            using var stream = assembly.GetManifestResourceStream(resource);
            if (stream is null)
            {
                continue;
            }

            using var reader = new StreamReader(stream);
            var content = reader.ReadToEnd();
            var simpleName = resource
                .Substring(resourcePrefix.Length)
                .Replace(".sbn", string.Empty, StringComparison.OrdinalIgnoreCase);
            Register(simpleName, content);
        }

        return this;
    }

    public TemplateRegistry Register(string name, string content)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Template name is required.", nameof(name));
        }

        lock (_sync)
        {
            _templates[name] = content ?? string.Empty;
        }

        return this;
    }

    public TemplateRegistry RegisterFile(string name, string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Template file not found.", filePath);
        }

        return Register(name, File.ReadAllText(filePath));
    }

    public string GetContent(string name)
    {
        if (!_templates.TryGetValue(name, out var content))
        {
            throw new InvalidOperationException($"Template '{name}' is not registered.");
        }

        return content;
    }

    public bool Contains(string name) => _templates.ContainsKey(name);

    public IReadOnlyList<string> GetNames() => _templates.Keys.ToList();
}

