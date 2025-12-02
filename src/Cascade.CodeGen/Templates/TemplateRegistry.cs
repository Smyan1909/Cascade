namespace Cascade.CodeGen.Templates;

/// <summary>
/// Manages template registration and retrieval.
/// </summary>
internal class TemplateRegistry
{
    private readonly Dictionary<string, string> _templates = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers a template.
    /// </summary>
    public void Register(string name, string content)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Template name cannot be null or empty", nameof(name));
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Template content cannot be null or empty", nameof(content));

        _templates[name] = content;
    }

    /// <summary>
    /// Gets a template by name.
    /// </summary>
    public string? Get(string name)
    {
        _templates.TryGetValue(name, out var content);
        return content;
    }

    /// <summary>
    /// Checks if a template exists.
    /// </summary>
    public bool Has(string name)
    {
        return _templates.ContainsKey(name);
    }

    /// <summary>
    /// Gets all template names.
    /// </summary>
    public IReadOnlyList<string> GetAllNames()
    {
        return _templates.Keys.ToList();
    }
}

