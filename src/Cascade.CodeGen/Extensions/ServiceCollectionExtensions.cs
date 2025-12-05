using Cascade.CodeGen.Compilation;
using Cascade.CodeGen.Execution;
using Cascade.CodeGen.Generation;
using Cascade.CodeGen.Recording;
using Cascade.CodeGen.Services;
using Cascade.CodeGen.Templates;
using Microsoft.Extensions.DependencyInjection;

namespace Cascade.CodeGen.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCascadeCodeGen(this IServiceCollection services, Action<CodeGenOptions>? configure = null)
    {
        services.AddSingleton<CodeGenOptions>(provider =>
        {
            var options = new CodeGenOptions();
            configure?.Invoke(options);
            return options;
        });

        services.AddSingleton<TemplateRegistry>(provider =>
        {
            var registry = new TemplateRegistry();
            registry.RegisterFromAssembly(typeof(ServiceCollectionExtensions).Assembly, $"{typeof(ServiceCollectionExtensions).Namespace!.Replace(".Extensions", ".Templates.BuiltIn.")}");
            return registry;
        });

        services.AddSingleton<TemplateContextFactory>();
        services.AddSingleton<ITemplateEngine, ScribanTemplateEngine>();
        services.AddSingleton<ICodeGenerator, CodeGenerator>();
        services.AddSingleton<IScriptCompiler, RoslynCompiler>();
        services.AddSingleton<IScriptExecutor, SandboxedExecutor>();
        services.AddSingleton<IGeneratedActionExecutor, GeneratedActionExecutor>();
        services.AddSingleton<IActionRecorder, ActionRecorder>();
        services.AddSingleton<ICodeGenService, CodeGenService>();

        return services;
    }
}

