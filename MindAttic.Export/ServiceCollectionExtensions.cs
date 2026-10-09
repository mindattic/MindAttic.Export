using Microsoft.Extensions.DependencyInjection;
using MindAttic.Export.Renderers;

namespace MindAttic.Export;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers the five renderers and <see cref="ManuscriptExporter"/>. Every front
    /// door (CLI, Prose Hub, any app) registers the identical graph (HOUSE-LAW-6).</summary>
    public static IServiceCollection AddMindAtticExport(this IServiceCollection services)
    {
        services.AddSingleton<IManuscriptRenderer, DocxRenderer>();
        services.AddSingleton<IManuscriptRenderer, EpubRenderer>();
        services.AddSingleton<IManuscriptRenderer, PdfRenderer>();
        services.AddSingleton<IManuscriptRenderer, TextRenderer>();
        services.AddSingleton<IManuscriptRenderer, MarkdownRenderer>();
        services.AddSingleton<ManuscriptExporter>();
        return services;
    }
}
