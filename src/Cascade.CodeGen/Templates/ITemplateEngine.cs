namespace Cascade.CodeGen.Templates;

public interface ITemplateEngine
{
    Task<string> RenderAsync(string templateName, object model, CancellationToken cancellationToken = default);
    Task<string> RenderAsync(string templateName, TemplateContext context, CancellationToken cancellationToken = default);
    string RenderInline(string templateContent, object model);

    void RegisterTemplate(string name, string content);
    void RegisterTemplateFile(string name, string filePath);
    bool HasTemplate(string name);
    IReadOnlyList<string> GetTemplateNames();

    TemplateValidationResult ValidateTemplate(string templateContent);
}

