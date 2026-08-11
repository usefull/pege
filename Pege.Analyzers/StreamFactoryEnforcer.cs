using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class StreamFactoryEnforcer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "PG001";
    private static readonly string Title = "Прямое создание потока запрещено";
    private static readonly string MessageFormat = "Класс '{0}' является потомком Stream и должен создаваться только через StreamFactory";
    private static readonly string Description = "Запрещает вызов конструкторов для наследников класса Stream вне фабрики.";
    private static readonly string Category = "Architecture";

    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
#pragma warning disable RS2008 // Включить отслеживание выпуска анализатора
        DiagnosticId, Title, MessageFormat, Category,
#pragma warning restore RS2008 // Включить отслеживание выпуска анализатора
        DiagnosticSeverity.Error,
        isEnabledByDefault: true, description: Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // Регистрируем проверку на создание любого объекта (выражение 'new')
        context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);
    }

    private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context)
    {
        var objectCreation = (ObjectCreationExpressionSyntax)context.Node;

        // Получаем семантическую информацию о классе, внутри которого пишется этот код
        var enclosingClassSyntax = objectCreation.FirstAncestorOrSelf<ClassDeclarationSyntax>();
        if (enclosingClassSyntax != null)
        {
            // Превращаем синтаксис класса в полноценный объект типа (Symbol)
            var enclosingClassSymbol = context.SemanticModel.GetDeclaredSymbol(enclosingClassSyntax);

            if (enclosingClassSymbol != null)
            {
                // Получаем полное имя класса, включая все пространства имен (например, "MyApplication.Streaming.StreamFactory")
                string fullClassName = enclosingClassSymbol.ToDisplayString();

                if (fullClassName == "Pege.Streaming.StreamFactory")
                {
                    return; // Фабрике создавать объекты разрешено
                }
            }
        }

        // Получаем информацию о типе, который пытаются создать через "new"
        var typeInfo = context.SemanticModel.GetTypeInfo(objectCreation);

        if (!(typeInfo.Type is INamedTypeSymbol typeSymbol)) return;

        // Идем вверх по дереву наследования создаваемого класса
        var currentType = typeSymbol.BaseType;
        while (currentType != null)
        {
            // Проверяем и имя, и то, что базовый класс — это именно НАШ базовый Stream из нужного namespace
            // ToDisplayString() для generic-класса вернет строку вида "MyApplication.Streaming.Stream<TInfo, TStatus, TChunk>"
            // Поэтому мы проверяем, с чего начинается полное имя типа.
            string baseTypeFullName = currentType.ToDisplayString();

            if (baseTypeFullName.StartsWith("Pege.Streaming.Stream<"))
            {
                // Нашли запрещенного наследника вне фабрики -> генерируем ошибку компиляции!
                var diagnostic = Diagnostic.Create(Rule, objectCreation.GetLocation(), typeSymbol.Name);
                context.ReportDiagnostic(diagnostic);
                break;
            }

            currentType = currentType.BaseType;
        }
    }
}
