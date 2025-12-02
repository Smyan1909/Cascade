namespace Cascade.CodeGen.Templates;

/// <summary>
/// Interface for template rendering engines.
/// </summary>
public interface ITemplateEngine
{
    /// <summary>
    /// Renders a template by name with a model object.
    /// </summary>
    Task<string> RenderAsync(string templateName, object model);

    /// <summary>
    /// Renders a template by name with a template context.
    /// </summary>
    Task<string> RenderAsync(string templateName, TemplateContext context);

    /// <summary>
    /// Renders an inline template string with a model object.
    /// </summary>
    string RenderInline(string templateContent, object model);

    /// <summary>
    /// Registers a template by name with content.
    /// </summary>
    void RegisterTemplate(string name, string content);

    /// <summary>
    /// Registers a template from a file.
    /// </summary>
    void RegisterTemplateFile(string name, string filePath);

    /// <summary>
    /// Checks if a template with the given name is registered.
    /// </summary>
    bool HasTemplate(string name);

    /// <summary>
    /// Gets all registered template names.
    /// </summary>
    IReadOnlyList<string> GetTemplateNames();

    /// <summary>
    /// Validates template syntax.
    /// </summary>
    ValidationResult ValidateTemplate(string templateContent);
}

