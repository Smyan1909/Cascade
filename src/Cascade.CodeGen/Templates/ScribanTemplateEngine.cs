using Scriban;
using Scriban.Runtime;
using System.Reflection;

namespace Cascade.CodeGen.Templates;

/// <summary>
/// Scriban-based implementation of the template engine.
/// </summary>
public class ScribanTemplateEngine : ITemplateEngine
{
    private readonly TemplateRegistry _registry = new();
    private readonly Dictionary<string, Template> _compiledTemplates = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Creates a new Scriban template engine.
    /// </summary>
    public ScribanTemplateEngine()
    {
        LoadBuiltInTemplates();
    }

    /// <inheritdoc />
    public async Task<string> RenderAsync(string templateName, object model)
    {
        var context = new TemplateContext
        {
            Variables = model is Dictionary<string, object> dict
                ? dict
                : ConvertToDictionary(model)
        };

        return await RenderAsync(templateName, context);
    }

    /// <inheritdoc />
    public async Task<string> RenderAsync(string templateName, TemplateContext context)
    {
        var templateContent = _registry.Get(templateName);
        if (templateContent == null)
            throw new InvalidOperationException($"Template '{templateName}' not found");

        return await Task.Run(() =>
        {
            var template = GetOrCompileTemplate(templateName, templateContent);
            var scriptObject = CreateScriptObject(context);
            var result = template.Render(scriptObject);
            return result ?? string.Empty;
        });
    }

    /// <inheritdoc />
    public string RenderInline(string templateContent, object model)
    {
        var template = Template.Parse(templateContent);
        if (template.HasErrors)
            throw new InvalidOperationException($"Template parse errors: {string.Join(", ", template.Messages.Select(m => m.Message))}");

        var scriptObject = model is Dictionary<string, object> dict
            ? CreateScriptObjectFromDictionary(dict)
            : CreateScriptObjectFromModel(model);

        var result = template.Render(scriptObject);
        return result ?? string.Empty;
    }

    /// <inheritdoc />
    public void RegisterTemplate(string name, string content)
    {
        _registry.Register(name, content);
        _compiledTemplates.Remove(name); // Invalidate cached template
    }

    /// <inheritdoc />
    public void RegisterTemplateFile(string name, string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Template file not found: {filePath}", filePath);

        var content = File.ReadAllText(filePath);
        RegisterTemplate(name, content);
    }

    /// <inheritdoc />
    public bool HasTemplate(string name)
    {
        return _registry.Has(name);
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetTemplateNames()
    {
        return _registry.GetAllNames();
    }

    /// <inheritdoc />
    public ValidationResult ValidateTemplate(string templateContent)
    {
        try
        {
            var template = Template.Parse(templateContent);
            if (template.HasErrors)
            {
                var errors = template.Messages.Select(m => $"{m.Type}: {m.Message} at {m.Span}").ToList();
                return ValidationResult.Failure(errors.ToArray());
            }

            return ValidationResult.Success();
        }
        catch (Exception ex)
        {
            return ValidationResult.Failure(ex.Message);
        }
    }

    private void LoadBuiltInTemplates()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceNames = assembly.GetManifestResourceNames()
            .Where(name => name.Contains("Templates") && name.Contains("BuiltIn") && name.EndsWith(".sbn"))
            .ToList();

        foreach (var resourceName in resourceNames)
        {
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
                continue;

            using var reader = new StreamReader(stream);
            var content = reader.ReadToEnd();
            var templateName = ExtractTemplateName(resourceName);
            _registry.Register(templateName, content);
            // Clear any cached compiled template
            _compiledTemplates.Remove(templateName);
        }
    }

    private string ExtractTemplateName(string resourceName)
    {
        // Extract template name from resource name like "Cascade.CodeGen.Templates.BuiltIn.ActionScript.sbn"
        // or "Cascade.CodeGen.Templates.BuiltIn.ActionScript.sbn"
        var parts = resourceName.Split('.');
        var index = Array.IndexOf(parts, "BuiltIn");
        if (index >= 0 && index + 1 < parts.Length)
        {
            var fileName = parts[index + 1];
            // Remove .sbn extension if present
            if (fileName.EndsWith(".sbn", StringComparison.OrdinalIgnoreCase))
            {
                fileName = fileName.Substring(0, fileName.Length - 4);
            }
            return fileName;
        }
        // Fallback: extract from end
        var fileNameOnly = Path.GetFileNameWithoutExtension(resourceName);
        return fileNameOnly;
    }

    private Template GetOrCompileTemplate(string name, string content)
    {
        if (_compiledTemplates.TryGetValue(name, out var cached))
            return cached;

        var template = Template.Parse(content);
        if (template.HasErrors)
            throw new InvalidOperationException($"Template '{name}' parse errors: {string.Join(", ", template.Messages.Select(m => m.Message))}");

        _compiledTemplates[name] = template;
        return template;
    }

    private ScriptObject CreateScriptObject(TemplateContext context)
    {
        var scriptObject = new ScriptObject();

        // Add all context variables
        foreach (var kvp in context.Variables)
        {
            scriptObject[kvp.Key] = kvp.Value;
        }

        // Add context properties
        scriptObject["namespace"] = context.Namespace ?? "";
        scriptObject["class_name"] = context.ClassName ?? "";
        scriptObject["usings"] = context.Usings;
        scriptObject["references"] = context.References;

        // Add helper methods
        scriptObject.Import("to_camel_case", context.ToCamelCase);
        scriptObject.Import("to_pascal_case", context.ToPascalCase);
        scriptObject.Import("to_snake_case", context.ToSnakeCase);
        scriptObject.Import("to_json", context.ToJson);

        return scriptObject;
    }

    private ScriptObject CreateScriptObjectFromDictionary(Dictionary<string, object> dict)
    {
        var scriptObject = new ScriptObject();
        foreach (var kvp in dict)
        {
            scriptObject[kvp.Key] = kvp.Value;
        }
        return scriptObject;
    }

    private ScriptObject CreateScriptObjectFromModel(object model)
    {
        var scriptObject = new ScriptObject();
        var properties = model.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var prop in properties)
        {
            var value = prop.GetValue(model);
            scriptObject[prop.Name] = value;
        }
        return scriptObject;
    }

    private Dictionary<string, object> ConvertToDictionary(object obj)
    {
        var dict = new Dictionary<string, object>();
        var properties = obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var prop in properties)
        {
            var value = prop.GetValue(obj);
            dict[prop.Name] = value ?? string.Empty;
        }
        return dict;
    }
}

