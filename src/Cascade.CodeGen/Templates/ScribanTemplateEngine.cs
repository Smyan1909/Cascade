using Scriban;
using Scriban.Runtime;

namespace Cascade.CodeGen.Templates;

public sealed class ScribanTemplateEngine : ITemplateEngine
{
    private readonly TemplateRegistry _registry;

    public ScribanTemplateEngine(TemplateRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    public Task<string> RenderAsync(string templateName, object model, CancellationToken cancellationToken = default)
    {
        var context = new TemplateContext();
        context.Variables["model"] = model;
        return RenderAsync(templateName, context, cancellationToken);
    }

    public async Task<string> RenderAsync(string templateName, TemplateContext context, CancellationToken cancellationToken = default)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var content = _registry.GetContent(templateName);
        var template = Template.Parse(content, templateName);

        if (template.HasErrors)
        {
            var message = string.Join(Environment.NewLine, template.Messages.Select(m => m.Message));
            throw new InvalidOperationException($"Template '{templateName}' failed to parse: {message}");
        }

        var scriptObject = new ScriptObject();
        scriptObject.Import(context);
        scriptObject.Import(context.Variables);

        var scribanContext = new Scriban.TemplateContext
        {
            MemberRenamer = member => member.Name
        };
        scribanContext.PushGlobal(scriptObject);

        return await template.RenderAsync(scribanContext).ConfigureAwait(false);
    }

    public string RenderInline(string templateContent, object model)
    {
        var template = Template.Parse(templateContent ?? string.Empty);
        if (template.HasErrors)
        {
            var message = string.Join(Environment.NewLine, template.Messages.Select(m => m.Message));
            throw new InvalidOperationException($"Template failed to parse: {message}");
        }

        var scriptObject = new ScriptObject();
        scriptObject.Import(model);
        var context = new Scriban.TemplateContext();
        context.PushGlobal(scriptObject);
        return template.Render(context);
    }

    public void RegisterTemplate(string name, string content) => _registry.Register(name, content);
    public void RegisterTemplateFile(string name, string filePath) => _registry.RegisterFile(name, filePath);
    public bool HasTemplate(string name) => _registry.Contains(name);
    public IReadOnlyList<string> GetTemplateNames() => _registry.GetNames();

    public TemplateValidationResult ValidateTemplate(string templateContent)
    {
        var template = Template.Parse(templateContent ?? string.Empty);
        if (template.HasErrors)
        {
            return TemplateValidationResult.Fail(string.Join(Environment.NewLine, template.Messages.Select(m => m.Message)));
        }

        return TemplateValidationResult.Ok();
    }
}

