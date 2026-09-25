using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Orivy;
using Orivy.Controls;
using Orivy.Studio.Toolbox;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace Orivy.Studio;

/// <summary>
/// The reverse of <see cref="CodeGenerator"/>: reads a Designer-code file (either exactly what
/// <see cref="CodeGenerator.Generate"/> produced, or a hand-edited variant of it) back into a
/// <see cref="DesignSurface"/>, so the code view is a real round trip instead of a one-way export.
///
/// Uses Roslyn (<c>Microsoft.CodeAnalysis.CSharp</c>) to parse the file's actual syntax tree rather
/// than a hand-rolled regex/text scanner — the generated code is ordinary C# with object initializers
/// and method-call statements, exactly what a syntax tree is for, and a real parser doesn't come
/// apart the moment whitespace or statement order shifts slightly from one specific text template.
/// </summary>
public static class CodeImporter
{
    private sealed class NodeInfo
    {
        public required string Name;
        public required string Type;
        public string? Text;
        public float X;
        public float Y;
        public float W;
        public float H;
        public bool HasLocation;
        public bool HasSize;
        public DockStyle Dock;
        public AnchorStyles Anchor = AnchorStyles.Top | AnchorStyles.Left;
        public bool Visible = true;

        // Anything not one of the structural properties above (Margin, Padding, Radius, Border,
        // BackColor, ForeColor, BorderColor, HatchStyle, Maximum, Value, ... — every property a real
        // Designer.cs sets that this importer doesn't special-case by name) is captured here instead
        // of silently dropped, and applied generically through TypeDescriptor/reflection in Rebuild.
        // Without this, e.g. an unset Margin fell back to ElementBase's runtime default (3,3,3,3,
        // mirroring WinForms' own Control.DefaultMargin) instead of whatever the file actually
        // specified, which reads as child controls being subtly "shifted" from where the source
        // positions them — the position values themselves imported fine, but the spacing around them
        // silently didn't.
        public readonly Dictionary<string, ExpressionSyntax> ExtraProperties = new(StringComparer.Ordinal);

        // `x.Items.Add(...)` / `x.Items.AddRange(...)` / `x.Columns.Add(...)` — populating a control's
        // own content collection (ListBox.Items, GridList.Columns/Items, ...) rather than adding a
        // child to the design surface (that's addEdges, keyed off `.Controls` specifically). Recorded
        // here and replayed in Rebuild once the real control instance (and its real collection object)
        // exists — without this, a real Designer.cs's actual items/columns/rows were silently dropped
        // on import, since nothing captured these statements at all.
        public readonly List<(string Member, string Method, List<ExpressionSyntax> Args)> CollectionCalls = new();
    }

    /// <summary>The name of the <c>partial class</c> the Designer code declares, if any — used to
    /// give the resulting tab a better name than a generic placeholder.</summary>
    public static string? TryGetClassName(string code)
    {
        try
        {
            var tree = CSharpSyntaxTree.ParseText(code);
            return tree.GetCompilationUnitRoot().DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .FirstOrDefault()
                ?.Identifier.Text;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Parses <paramref name="code"/>'s <c>InitializeComponent</c> method and rebuilds
    /// <paramref name="surface"/> from it, replacing whatever the surface currently holds. Returns
    /// the names of any referenced control types that aren't in <see cref="ControlCatalog"/> (skipped,
    /// not fatal). This is the sole load path a design document has — there is no separate project
    /// format to fall back to. <paramref name="filePath"/>, when known (opening a real file rather
    /// than a paste), additionally enables cross-file base-class resolution — see
    /// <see cref="LoadBaseClassChain"/>.
    /// </summary>
    public static IReadOnlyList<string> Import(DesignSurface surface, string code, string? filePath = null)
    {
        var tree = CSharpSyntaxTree.ParseText(code);
        var compilationUnit = tree.GetCompilationUnitRoot();

        var hasInitMethod = compilationUnit.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Any(m => m.Identifier.Text == "InitializeComponent");
        if (!hasInitMethod)
            throw new InvalidOperationException("No InitializeComponent() method found in the pasted code.");

        var knownTypeNames = ControlCatalog.Discover().Select(e => e.DisplayName).ToHashSet(StringComparer.Ordinal);
        var projectClasses = DiscoverProjectClasses(filePath);
        var declaredFields = new HashSet<string>(StringComparer.Ordinal);
        var declaredFieldTypes = new Dictionary<string, string>(StringComparer.Ordinal);
        var nodes = new Dictionary<string, NodeInfo>(StringComparer.Ordinal);
        var declarationOrder = new List<string>();
        // Empty parent name means "the design root" (a plain Controls.Add(x) call).
        var addEdges = new List<(string ParentName, string ChildName)>();
        SKSize? clientSize = null;

        // Cross-file inheritance: a Designer.cs whose InitializeComponent only ever touches fields
        // declared on a BASE class in a different file (e.g. a UserControl-style `partial class
        // Ability` whose own designer file only sets Margin/Size on `labelLevel`/`lblPetName`/
        // `progressHP` — fields that actually live on `CosControlBase` in a sibling file) can't be
        // resolved by a single-file parse: nothing here declares what `labelLevel` even is, so
        // `labelLevel.Margin = ...` never matches anything. Previously that meant either an outright
        // "No control declarations found" (if every statement was like this) or the inherited rows
        // being silently dropped (if the file also declared some of its own — see the Fellow/Ability
        // case that prompted this). Best effort, walked one inheritance level at a time: find this
        // class's own base type by scanning nearby files for another partial of the same class name
        // that actually lists one (a design file's own partial usually doesn't), then parse that base
        // type's own file the same way, seeding nodes/declarationOrder/addEdges *before* this file's
        // own statements run — so a property-only reference to an inherited field resolves exactly
        // like a same-file one would.
        if (!string.IsNullOrEmpty(filePath))
        {
            var className = compilationUnit.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault()?.Identifier.Text;
            if (className != null)
            {
                LoadBaseClassChain(
                    filePath, className, knownTypeNames, declaredFields, declaredFieldTypes,
                    nodes, declarationOrder, addEdges, ref clientSize,
                    visited: new HashSet<string>(StringComparer.Ordinal), depthRemaining: 6);
            }
        }

        ProcessCompilationUnit(compilationUnit, knownTypeNames, declaredFields, declaredFieldTypes, nodes, declarationOrder, addEdges, ref clientSize);

        if (declarationOrder.Count == 0)
            throw new InvalidOperationException("No control declarations found — is this Designer code Orivy Studio generated?");

        // A control field isn't always declared with a catalog type directly — a codebase's own
        // `class ThemedButton : Button` (or a chain of those) is just as likely as a bare `Button`.
        // Only chains fully declared within this same file/paste are resolvable at all here (there's
        // no project-wide symbol resolution), but that covers the common "one subclass, same file"
        // case; ResolveKnownAncestorType below also falls back to a name-suffix guess for anything
        // that still doesn't resolve, so a subclass defined elsewhere isn't necessarily lost either.
        var localBaseTypeOf = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var classDecl in compilationUnit.DescendantNodes().OfType<ClassDeclarationSyntax>())
        {
            var baseType = classDecl.BaseList?.Types.FirstOrDefault();
            if (baseType != null)
                localBaseTypeOf[classDecl.Identifier.Text] = GetSimpleTypeName(baseType.Type);
        }

        return Rebuild(surface, clientSize, nodes, declarationOrder, addEdges, localBaseTypeOf, projectClasses);
    }

    private sealed record ProjectClassInfo(string? BaseType, string? FilePath, string? DesignerPath);

    private static Dictionary<string, ProjectClassInfo> DiscoverProjectClasses(string? filePath)
    {
        var result = new Dictionary<string, ProjectClassInfo>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(filePath))
            return result;

        var startDir = System.IO.Path.GetDirectoryName(filePath);
        if (startDir == null)
            return result;

        var searchRoot = startDir;
        var current = startDir;
        for (var i = 0; i < 4; i++)
        {
            if (current == null) break;
            try
            {
                if (System.IO.Directory.EnumerateFiles(current, "*.csproj").Any() ||
                    System.IO.Directory.EnumerateFiles(current, "*.sln").Any())
                {
                    searchRoot = current;
                    break;
                }
            }
            catch { }
            current = System.IO.Path.GetDirectoryName(current);
        }

        try
        {
            var files = System.IO.Directory.EnumerateFiles(searchRoot, "*.cs", System.IO.SearchOption.AllDirectories);
            foreach (var file in files)
            {
                if (file.Contains("\\bin\\", StringComparison.OrdinalIgnoreCase) ||
                    file.Contains("/bin/", StringComparison.OrdinalIgnoreCase) ||
                    file.Contains("\\obj\\", StringComparison.OrdinalIgnoreCase) ||
                    file.Contains("/obj/", StringComparison.OrdinalIgnoreCase) ||
                    file.Contains("\\.git\\", StringComparison.OrdinalIgnoreCase) ||
                    file.Contains("/.git/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var isDesigner = file.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase);
                try
                {
                    var text = System.IO.File.ReadAllText(file);
                    if (!text.Contains("class ", StringComparison.Ordinal))
                        continue;

                    var tree = CSharpSyntaxTree.ParseText(text);
                    var classes = tree.GetCompilationUnitRoot().DescendantNodes().OfType<ClassDeclarationSyntax>();
                    foreach (var cls in classes)
                    {
                        var name = cls.Identifier.Text;
                        string? baseType = null;
                        if (cls.BaseList?.Types.Count > 0)
                            baseType = GetSimpleTypeName(cls.BaseList.Types[0].Type);

                        if (!result.TryGetValue(name, out var existing))
                        {
                            result[name] = new ProjectClassInfo(baseType, isDesigner ? null : file, isDesigner ? file : null);
                        }
                        else
                        {
                            result[name] = new ProjectClassInfo(
                                baseType ?? existing.BaseType,
                                isDesigner ? existing.FilePath : file,
                                isDesigner ? file : existing.DesignerPath);
                        }
                    }
                }
                catch { }
            }
        }
        catch { }

        return result;
    }

    /// <summary>Walks up this class's inheritance chain (capped at <paramref name="depthRemaining"/>
    /// levels) trying to find each base type's own Designer.cs-equivalent file nearby on disk, and
    /// feeds it through <see cref="ProcessCompilationUnit"/> exactly like the main file — so its
    /// fields/controls are already known by the time the derived class's own statements are
    /// processed. Best-effort throughout: any failure to locate or parse a file just stops the walk
    /// at that level rather than failing the whole import — this is a convenience for the common
    /// "derived type in a feature subfolder, base type one level up" layout, not a real project-wide
    /// symbol resolver.</summary>
    private static void LoadBaseClassChain(
        string filePath,
        string className,
        HashSet<string> knownTypeNames,
        HashSet<string> declaredFields,
        Dictionary<string, string> declaredFieldTypes,
        Dictionary<string, NodeInfo> nodes,
        List<string> declarationOrder,
        List<(string ParentName, string ChildName)> addEdges,
        ref SKSize? clientSize,
        HashSet<string> visited,
        int depthRemaining)
    {
        if (depthRemaining <= 0 || !visited.Add(className))
            return;

        var startDirectory = System.IO.Path.GetDirectoryName(filePath);
        if (startDirectory == null)
            return;

        // A design file's own partial (Ability.designer.cs) usually has no base list at all — the
        // inheritance is declared on the OTHER partial (Ability.cs) instead — so look at every .cs
        // file nearby for any declaration of this exact class name that does list one.
        string? baseTypeName = null;
        foreach (var candidateDir in CandidateDirectories(startDirectory))
        {
            foreach (var file in SafeEnumerateCsFiles(candidateDir))
            {
                if (!TryParseClass(file, className, out var decl, out _) || decl!.BaseList == null)
                    continue;

                baseTypeName = GetSimpleTypeName(decl.BaseList.Types[0].Type);
                break;
            }
            if (baseTypeName != null)
                break;
        }

        // Nothing more to inherit from once the chain bottoms out at a plain catalog type
        // (Container, Element, ...) — those have no Designer.cs of their own to go looking for.
        if (baseTypeName == null || knownTypeNames.Contains(baseTypeName))
            return;

        foreach (var candidateDir in CandidateDirectories(startDirectory))
        {
            foreach (var file in SafeEnumerateCsFiles(candidateDir))
            {
                if (!TryParseClass(file, baseTypeName, out _, out var baseUnit))
                    continue;

                ProcessCompilationUnit(baseUnit!, knownTypeNames, declaredFields, declaredFieldTypes, nodes, declarationOrder, addEdges, ref clientSize);
                LoadBaseClassChain(file, baseTypeName, knownTypeNames, declaredFields, declaredFieldTypes, nodes, declarationOrder, addEdges, ref clientSize, visited, depthRemaining - 1);
                return;
            }
        }
    }

    private static IEnumerable<string> CandidateDirectories(string startDirectory)
    {
        yield return startDirectory;
        var dir = startDirectory;
        for (var i = 0; i < 2; i++)
        {
            dir = System.IO.Path.GetDirectoryName(dir);
            if (dir == null)
                yield break;
            yield return dir;
        }
    }

    private static IEnumerable<string> SafeEnumerateCsFiles(string directory)
    {
        try
        {
            return System.IO.Directory.EnumerateFiles(directory, "*.cs", System.IO.SearchOption.TopDirectoryOnly);
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static bool TryParseClass(string filePath, string className, out ClassDeclarationSyntax? declaration, out CompilationUnitSyntax? unit)
    {
        declaration = null;
        unit = null;
        try
        {
            var text = System.IO.File.ReadAllText(filePath);
            var root = CSharpSyntaxTree.ParseText(text).GetCompilationUnitRoot();
            var found = root.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault(c => c.Identifier.Text == className);
            if (found == null)
                return false;

            declaration = found;
            unit = root;
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Merges one compilation unit's field declarations, field-initializer controls and
    /// <c>InitializeComponent</c> statements into the shared accumulators — the whole body of what
    /// used to be <see cref="Import"/> itself, extracted so <see cref="LoadBaseClassChain"/> can run
    /// it again for a base type's own file before the derived file's statements run.</summary>
    private static void ProcessCompilationUnit(
        CompilationUnitSyntax compilationUnit,
        HashSet<string> knownTypeNames,
        HashSet<string> declaredFields,
        Dictionary<string, string> declaredFieldTypes,
        Dictionary<string, NodeInfo> nodes,
        List<string> declarationOrder,
        List<(string ParentName, string ChildName)> addEdges,
        ref SKSize? clientSize)
    {
        // Recognizes both dialects: the compact object-initializer form CodeGenerator itself emits
        // (`button1 = new Button { Location = ..., ... };`) and the classic WinForms Designer.cs shape
        // most hand-written or Visual-Studio-generated files actually use — `this.` prefixed, a bare
        // `new Type()` declaration followed by separate `this.button1.Property = value;` statements,
        // and `this.Controls.Add(this.button1);`. A real Designer.cs someone already has is exactly
        // the "just files in the folder" case this importer needs to open, not only Studio's own export.
        foreach (var field in compilationUnit.DescendantNodes().OfType<FieldDeclarationSyntax>())
        {
            var fieldTypeName = GetSimpleTypeName(field.Declaration.Type);
            foreach (var variable in field.Declaration.Variables)
            {
                var name = variable.Identifier.Text;
                declaredFields.Add(name);
                declaredFieldTypes[name] = fieldTypeName;
            }
        }

        // A third dialect: a control declared and configured entirely as a field initializer —
        // `private readonly Button _save = new() { Text = "Save", ... };` — with InitializeComponent
        // doing nothing but a plain Controls.Add(_save) for it. Plenty of natural, modern C# for a
        // hand-written Orivy app looks exactly like this instead of the WinForms-style split between
        // a field declaration and a separate InitializeComponent assignment; missing it meant
        // InitializeComponent could easily contain zero recognizable declarations at all.
        foreach (var field in compilationUnit.DescendantNodes().OfType<FieldDeclarationSyntax>())
        {
            var fieldTypeName = GetSimpleTypeName(field.Declaration.Type);

            foreach (var variable in field.Declaration.Variables)
            {
                var (creationType, fieldInitializer) = variable.Initializer?.Value switch
                {
                    ObjectCreationExpressionSyntax oce => (GetSimpleTypeName(oce.Type), oce.Initializer),
                    ImplicitObjectCreationExpressionSyntax ioce => (fieldTypeName, ioce.Initializer),
                    _ => (null, null),
                };

                if (creationType == null || !knownTypeNames.Contains(creationType))
                    continue;

                var fieldName = variable.Identifier.Text;
                var fieldInfo = new NodeInfo { Name = fieldName, Type = creationType };
                nodes[fieldName] = fieldInfo;
                declarationOrder.Add(fieldName);

                if (fieldInitializer != null)
                {
                    foreach (var member in fieldInitializer.Expressions.OfType<AssignmentExpressionSyntax>())
                    {
                        if (member.Left is IdentifierNameSyntax { Identifier.Text: var propertyName })
                            ApplyProperty(fieldInfo, propertyName, member.Right);
                    }
                }
            }
        }

        var initMethod = compilationUnit.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.Text == "InitializeComponent");

        if (initMethod?.Body == null)
            return;

        foreach (var statement in EnumerateDesignStatements(initMethod.Body))
        {
            if (statement is LocalDeclarationStatementSyntax localDeclaration)
            {
                ImportLocalDeclaration(localDeclaration, knownTypeNames, nodes, declarationOrder);
                continue;
            }

            if (statement is not ExpressionStatementSyntax { Expression: var expression })
                continue;

            switch (expression)
            {
                // Studio's own generator always writes the root's size as `ClientSize = ...` (see
                // CodeGenerator), but a real hand-written or Visual-Studio-generated UserControl
                // Designer.cs — exactly what a non-Form component like a WinForms UserControl (and
                // its Orivy equivalent, a root `Container`) actually emits — uses a bare `Size = ...`
                // for that same self-assignment instead. Recognizing only "ClientSize" meant importing
                // one of those files silently kept the design root at its default size instead of the
                // one the file actually specifies (`this` here since the left side of a bare `Size =`
                // has no receiver — it isn't a stray decl and isn't a member access, so it fell through
                // every other case with no effect at all).
                case AssignmentExpressionSyntax { Left: var clientSizeLeft, Right: var sizeExpr }
                    when ResolveTargetName(clientSizeLeft) is "ClientSize" or "Size"
                         && (sizeExpr is ObjectCreationExpressionSyntax { ArgumentList.Arguments.Count: 2 }
                             || sizeExpr is ImplicitObjectCreationExpressionSyntax { ArgumentList.Arguments.Count: 2 }):
                {
                    var args = sizeExpr is ObjectCreationExpressionSyntax sOce
                        ? sOce.ArgumentList!.Arguments
                        : ((ImplicitObjectCreationExpressionSyntax)sizeExpr).ArgumentList!.Arguments;
                    clientSize = new SKSize(
                        ParseFloat(args[0].Expression),
                        ParseFloat(args[1].Expression));
                    break;
                }

                // A declaration: `x = new Type();` / `this.x = new Type();`, or `x = new();`, with or
                // without an object initializer.
                case AssignmentExpressionSyntax
                {
                    Left: var declLeft,
                    Right: var creationExpr
                } when ResolveTargetName(declLeft) is { } declName
                       && (creationExpr is ObjectCreationExpressionSyntax || creationExpr is ImplicitObjectCreationExpressionSyntax):
                {
                    string? typeName = null;
                    InitializerExpressionSyntax? initializer = null;

                    if (creationExpr is ObjectCreationExpressionSyntax oce)
                    {
                        typeName = GetSimpleTypeName(oce.Type);
                        initializer = oce.Initializer;
                    }
                    else if (creationExpr is ImplicitObjectCreationExpressionSyntax ioce)
                    {
                        if (declaredFieldTypes.TryGetValue(declName, out var dft))
                            typeName = dft;
                        initializer = ioce.Initializer;
                    }

                    if (typeName != null && (declaredFields.Contains(declName)
                         || (declaredFields.Count == 0 && knownTypeNames.Contains(typeName))))
                    {
                        if (!nodes.TryGetValue(declName, out var info))
                        {
                            info = new NodeInfo { Name = declName, Type = typeName };
                            nodes[declName] = info;
                            declarationOrder.Add(declName);
                        }
                        else
                        {
                            info.Type = typeName;
                        }

                        if (initializer != null)
                        {
                            foreach (var member in initializer.Expressions.OfType<AssignmentExpressionSyntax>())
                            {
                                if (member.Left is IdentifierNameSyntax { Identifier.Text: var propertyName })
                                    ApplyProperty(info, propertyName, member.Right);
                            }
                        }
                    }
                    break;
                }

                // A separate property-assignment statement for an already-declared control — the
                // classic WinForms shape (`this.button1.Location = new Point(10, 10);`) sets each
                // property one statement at a time instead of in one object initializer.
                case AssignmentExpressionSyntax { Left: MemberAccessExpressionSyntax { Name: IdentifierNameSyntax propName } memberLeft, Right: var propValue }
                    when ResolveTargetName(memberLeft.Expression) is { } targetName && nodes.TryGetValue(targetName, out var existingInfo):
                    ApplyProperty(existingInfo, propName.Identifier.Text, propValue);
                    break;

                // `owner.Items.Add(...)` / `owner.Columns.Add(...)` / `owner.Items.AddRange(...)` — a
                // control's own content collection, not a `.Controls` add (that's the two cases below,
                // which this one's `collectionMember != "Controls"` guard steps aside for). Must be
                // checked before the generic `.Controls`-oriented "Add" case immediately below, since
                // that case matches the same "Add" method name but only knows what to do when the
                // receiver resolves to a `.Controls` collection — anything else it silently no-ops.
                case InvocationExpressionSyntax
                {
                    Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "Add" or "AddRange" } collInvokeTarget,
                    ArgumentList.Arguments: { Count: > 0 } collInvokeArgs
                }
                    when collInvokeTarget.Expression is MemberAccessExpressionSyntax { Name.Identifier.Text: var collectionMember } collOwnerAccess
                         && collectionMember != "Controls"
                         && ResolveTargetName(collOwnerAccess.Expression) is { } collOwnerName
                         && nodes.TryGetValue(collOwnerName, out var collOwnerInfo):
                    collOwnerInfo.CollectionCalls.Add((collectionMember, collInvokeTarget.Name.Identifier.Text, collInvokeArgs.Select(a => a.Expression).ToList()));
                    break;

                case InvocationExpressionSyntax
                {
                    Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "Add" } target,
                    ArgumentList.Arguments: { Count: 1 } addArguments
                }:
                    if (ResolveTargetName(addArguments[0].Expression) is not { } childName)
                        break;
                    if (ResolveControlsParentName(target.Expression) is { } addParentName)
                        addEdges.Add((addParentName, childName));
                    break;

                // Visual Studio's own WinForms designer emits one `AddRange(new Control[] { a, b, c })`
                // per parent instead of individual `.Add()` calls whenever a container ends up with more
                // than one child — a real Designer.cs someone hand-wrote or exported from VS is at least
                // as likely to use this form as individual Add() calls. Missing it doesn't fail loudly:
                // every control still gets declared and typed, so the import "succeeds" with an empty
                // canvas, since nothing ever recorded that any of them belonged under a parent.
                case InvocationExpressionSyntax
                {
                    Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "AddRange" } rangeTarget,
                    ArgumentList.Arguments: { Count: 1 } rangeArguments
                }:
                    if (ResolveControlsParentName(rangeTarget.Expression) is not { } rangeParentName)
                        break;

                    var elements = rangeArguments[0].Expression switch
                    {
                        ArrayCreationExpressionSyntax { Initializer: { } arrayInit } => arrayInit.Expressions,
                        ImplicitArrayCreationExpressionSyntax { Initializer: { } implicitInit } => implicitInit.Expressions,
                        InitializerExpressionSyntax bareInit => bareInit.Expressions,
                        _ => default,
                    };

                    foreach (var element in elements)
                    {
                        if (ResolveTargetName(element) is { } rangeChildName)
                            addEdges.Add((rangeParentName, rangeChildName));
                    }
                    break;
            }
        }
    }

    /// <summary>
    /// Statements the importer understands, in source order, including ones nested in a block or
    /// <c>if</c>/<c>try</c>. Local functions are skipped: their statements are not part of the
    /// control tree. A <c>var button = new Button { ... }</c> is a local declaration, not an
    /// expression statement, so walking only the method's top-level expression statements dropped
    /// every control a hand-written file declared that way and the canvas opened empty.
    /// </summary>
    private static IEnumerable<StatementSyntax> EnumerateDesignStatements(BlockSyntax body)
    {
        var stack = new Stack<StatementSyntax>();
        for (var i = body.Statements.Count - 1; i >= 0; i--)
            stack.Push(body.Statements[i]);

        while (stack.Count > 0)
        {
            var statement = stack.Pop();
            switch (statement)
            {
                case BlockSyntax block:
                    for (var i = block.Statements.Count - 1; i >= 0; i--)
                        stack.Push(block.Statements[i]);
                    break;
                case IfStatementSyntax ifStatement:
                    if (ifStatement.Else != null)
                        stack.Push(ifStatement.Else.Statement);
                    stack.Push(ifStatement.Statement);
                    break;
                case TryStatementSyntax tryStatement:
                    stack.Push(tryStatement.Block);
                    break;
                case LocalFunctionStatementSyntax:
                    break;
                default:
                    yield return statement;
                    break;
            }
        }
    }

    private static void ImportLocalDeclaration(
        LocalDeclarationStatementSyntax localDeclaration,
        HashSet<string> knownTypeNames,
        Dictionary<string, NodeInfo> nodes,
        List<string> declarationOrder)
    {
        var declaredTypeName = GetSimpleTypeName(localDeclaration.Declaration.Type);
        foreach (var variable in localDeclaration.Declaration.Variables)
        {
            string? typeName;
            InitializerExpressionSyntax? initializer;
            switch (variable.Initializer?.Value)
            {
                case ObjectCreationExpressionSyntax objectCreation:
                    typeName = GetSimpleTypeName(objectCreation.Type);
                    initializer = objectCreation.Initializer;
                    break;
                case ImplicitObjectCreationExpressionSyntax implicitCreation when declaredTypeName != "var":
                    typeName = declaredTypeName;
                    initializer = implicitCreation.Initializer;
                    break;
                default:
                    continue;
            }

            if (typeName == null || !IsCatalogTypeName(typeName, knownTypeNames))
                continue;

            var name = variable.Identifier.Text;
            if (!nodes.TryGetValue(name, out var info))
            {
                info = new NodeInfo { Name = name, Type = typeName };
                nodes[name] = info;
                declarationOrder.Add(name);
            }
            else
            {
                info.Type = typeName;
            }

            if (initializer == null)
                continue;

            foreach (var member in initializer.Expressions.OfType<AssignmentExpressionSyntax>())
            {
                if (member.Left is IdentifierNameSyntax { Identifier.Text: var propertyName })
                    ApplyProperty(info, propertyName, member.Right);
            }
        }
    }

    private static bool IsCatalogTypeName(string typeName, HashSet<string> knownTypeNames)
    {
        if (knownTypeNames.Contains(typeName))
            return true;

        foreach (var known in knownTypeNames)
        {
            if (typeName.Length > known.Length && typeName.EndsWith(known, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    /// <summary>Extracts a plain field/variable name from either a bare identifier (<c>button1</c>) or
    /// a one-level <c>this.</c>-qualified access (<c>this.button1</c>) — the two shapes Studio's own
    /// generated code and classic WinForms Designer.cs respectively use to refer to a declared field.
    /// Anything deeper (e.g. <c>this.button1.Location</c>) is a property access, not a target name, and
    /// correctly falls through to null here.</summary>
    private static string? ResolveTargetName(ExpressionSyntax expression) => expression switch
    {
        IdentifierNameSyntax id => id.Identifier.Text,
        MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax, Name: IdentifierNameSyntax name } => name.Identifier.Text,
        _ => null,
    };

    /// <summary>The simple type name a static-member receiver refers to, whether written bare
    /// (<c>GridLength</c>) or namespace-qualified (<c>Orivy.GridLength</c>, itself a nested
    /// <see cref="MemberAccessExpressionSyntax"/> rather than a single identifier).</summary>
    private static string? GetMemberAccessTypeName(ExpressionSyntax expression) => expression switch
    {
        IdentifierNameSyntax id => id.Identifier.Text,
        MemberAccessExpressionSyntax ma => ma.Name.Identifier.Text,
        _ => null,
    };

    /// <summary>Given the receiver of a <c>.Add(...)</c>/<c>.AddRange(...)</c> call (e.g. the
    /// <c>X.Controls</c> in <c>X.Controls.Add(y)</c>), resolves which control's child collection this
    /// is — <c>""</c> for the design root's own <c>Controls</c>, a control's name for a nested one, or
    /// <c>null</c> if this isn't a <c>Controls</c> access at all. Shared by both call shapes since the
    /// "whose Controls is this" question is identical either way.</summary>
    private static string? ResolveControlsParentName(ExpressionSyntax controlsAccess) => controlsAccess switch
    {
        IdentifierNameSyntax { Identifier.Text: "Controls" } => "",
        MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax, Name.Identifier.Text: "Controls" } => "",
        MemberAccessExpressionSyntax { Name.Identifier.Text: "Controls", Expression: var parentExpr } when ResolveTargetName(parentExpr) is { } p => p,
        _ => null,
    };

    /// <summary>The catalog-comparable simple name of a type reference, whether written bare
    /// (<c>Button</c>) or fully qualified (<c>Orivy.Controls.Button</c>, parsed as a
    /// <see cref="QualifiedNameSyntax"/>) — real hand-written Designer.cs is not guaranteed to use the
    /// same unqualified style Studio's own generator always emits.</summary>
    private static string GetSimpleTypeName(TypeSyntax type) => type switch
    {
        QualifiedNameSyntax q => q.Right.Identifier.Text,
        GenericNameSyntax g => g.Identifier.Text,
        IdentifierNameSyntax id => id.Identifier.Text,
        _ => type.ToString(),
    };

    private static void ApplyProperty(NodeInfo info, string propertyName, ExpressionSyntax right)
    {
        switch (propertyName)
        {
            case "Text" when right is LiteralExpressionSyntax { Token.Value: string text }:
                info.Text = text;
                break;
            case "Location" when (right is ObjectCreationExpressionSyntax { ArgumentList.Arguments.Count: 2 }
                               || right is ImplicitObjectCreationExpressionSyntax { ArgumentList.Arguments.Count: 2 }):
                var locArgs = right is ObjectCreationExpressionSyntax locOce
                    ? locOce.ArgumentList!.Arguments
                    : ((ImplicitObjectCreationExpressionSyntax)right).ArgumentList!.Arguments;
                info.X = ParseFloat(locArgs[0].Expression);
                info.Y = ParseFloat(locArgs[1].Expression);
                info.HasLocation = true;
                break;
            case "Size" when (right is ObjectCreationExpressionSyntax { ArgumentList.Arguments.Count: 2 }
                           || right is ImplicitObjectCreationExpressionSyntax { ArgumentList.Arguments.Count: 2 }):
                var sizeArgs = right is ObjectCreationExpressionSyntax sizeOce
                    ? sizeOce.ArgumentList!.Arguments
                    : ((ImplicitObjectCreationExpressionSyntax)right).ArgumentList!.Arguments;
                info.W = ParseFloat(sizeArgs[0].Expression);
                info.H = ParseFloat(sizeArgs[1].Expression);
                info.HasSize = true;
                break;
            case "Dock":
                info.Dock = ParseDock(right);
                break;
            case "Anchor":
                info.Anchor = ParseAnchor(right);
                break;
            case "Visible":
                info.Visible = !right.IsKind(SyntaxKind.FalseLiteralExpression);
                break;
            default:
                info.ExtraProperties[propertyName] = right;
                break;
        }
    }

    /// <summary>Best-effort conversion of a right-hand-side expression into a live value assignable to
    /// <paramref name="targetType"/> — everything <see cref="ApplyProperty"/> doesn't already
    /// special-case structurally (Margin, Padding, Radius, Border, colors, enums, ...) goes through
    /// here at <see cref="Rebuild"/> time via <see cref="TypeDescriptor"/>. Covers the expression
    /// shapes real Designer.cs code actually uses: literals, <c>Namespace.Enum.Member</c> (and
    /// <c>|</c>-combined flags), <c>SKColors.Named</c>, and <c>new Type(args)</c>/<c>new(args)</c> for
    /// any struct with a constructor matching the argument count (<c>Thickness</c>, <c>Radius</c>,
    /// <c>SKColor</c>, <c>SKSize</c>, <c>SKPoint</c>, ...). Returns false for anything else (a method
    /// call, a variable reference, ...) rather than guessing — the property is then just left at
    /// whatever the control's own constructor default is, same as before this existed.</summary>
    private static bool TryConvertValue(ExpressionSyntax expr, Type targetType, out object? value, Func<string, object?>? objectLookup = null)
    {
        var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;
        value = null;

        if (objectLookup != null && expr is IdentifierNameSyntax id && objectLookup(id.Identifier.Text) is { } resolved)
        {
            if (underlying.IsInstanceOfType(resolved))
            {
                value = resolved;
                return true;
            }
        }

        // An untyped element slot (e.g. ListBox.ObjectCollection.AddRange(params object?[] values), a
        // GridList row cell) — the literal's own runtime type is exactly what belongs there, since
        // nothing more specific than "object" is asking for a conversion.
        if (underlying == typeof(object) && expr is LiteralExpressionSyntax { Token.Value: { } literalValue })
        {
            value = literalValue;
            return true;
        }

        if (expr is PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.UnaryMinusExpression } unary)
        {
            if (!TryConvertValue(unary.Operand, underlying, out var inner, objectLookup) || inner == null || !IsNumericType(underlying))
                return false;
            try { value = Convert.ChangeType(-Convert.ToDouble(inner, CultureInfo.InvariantCulture), underlying, CultureInfo.InvariantCulture); return true; }
            catch { return false; }
        }

        switch (expr)
        {
            case LiteralExpressionSyntax { Token.Value: string s } when underlying == typeof(string):
                value = s;
                return true;

            case LiteralExpressionSyntax { Token.Value: bool b } when underlying == typeof(bool):
                value = b;
                return true;

            case LiteralExpressionSyntax when IsNumericType(underlying):
                try { value = Convert.ChangeType(ParseFloat(expr), underlying, CultureInfo.InvariantCulture); return true; }
                catch { return false; }

            case MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.Text: "SKColors" }, Name.Identifier.Text: var colorName }
                when underlying == typeof(SKColor):
                var field = typeof(SKColors).GetField(colorName, BindingFlags.Public | BindingFlags.Static);
                if (field?.GetValue(null) is SKColor namedColor) { value = namedColor; return true; }
                return false;

            case MemberAccessExpressionSyntax member when underlying.IsEnum:
                if (Enum.TryParse(underlying, member.Name.Identifier.Text, out var enumValue)) { value = enumValue; return true; }
                return false;

            // A named static constant on the target type itself (GridLength.Auto, Thickness.Empty,
            // ...) — the general form of the SKColors.Named case above, for any type that exposes one
            // rather than needing a dedicated case per type. The receiver may itself be namespace-
            // qualified (Orivy.GridLength.Auto), so only the rightmost segment is compared.
            case MemberAccessExpressionSyntax { Name.Identifier.Text: var staticMemberName } staticAccess
                when GetMemberAccessTypeName(staticAccess.Expression) == underlying.Name:
                var staticProperty = underlying.GetProperty(staticMemberName, BindingFlags.Public | BindingFlags.Static);
                if (staticProperty?.GetValue(null) is { } staticPropertyValue) { value = staticPropertyValue; return true; }
                var staticField = underlying.GetField(staticMemberName, BindingFlags.Public | BindingFlags.Static);
                if (staticField?.GetValue(null) is { } staticFieldValue) { value = staticFieldValue; return true; }
                return false;

            case BinaryExpressionSyntax { RawKind: (int)SyntaxKind.BitwiseOrExpression } flags when underlying.IsEnum:
                if (TryConvertValue(flags.Left, underlying, out var leftFlag, objectLookup) && TryConvertValue(flags.Right, underlying, out var rightFlag, objectLookup))
                {
                    value = Enum.ToObject(underlying, Convert.ToInt64(leftFlag) | Convert.ToInt64(rightFlag));
                    return true;
                }
                return false;

            case ObjectCreationExpressionSyntax { ArgumentList: { } oceArgs } when underlying == typeof(SKColor):
                return TryBuildSKColor(oceArgs.Arguments, out value);
            case ImplicitObjectCreationExpressionSyntax { ArgumentList: { } ioceColorArgs } when underlying == typeof(SKColor):
                return TryBuildSKColor(ioceColorArgs.Arguments, out value);

            case ObjectCreationExpressionSyntax { ArgumentList: { } oceArgs2 }:
                return TryConstruct(underlying, oceArgs2.Arguments, out value, objectLookup);
            case ImplicitObjectCreationExpressionSyntax { ArgumentList: { } ioceArgs2 }:
                return TryConstruct(underlying, ioceArgs2.Arguments, out value, objectLookup);

            default:
                return false;
        }
    }

    private static bool IsNumericType(Type type) =>
        type == typeof(float) || type == typeof(double) || type == typeof(int) || type == typeof(long) ||
        type == typeof(byte) || type == typeof(short) || type == typeof(uint) || type == typeof(ulong);

    private static bool TryBuildSKColor(SeparatedSyntaxList<ArgumentSyntax> args, out object? value)
    {
        value = null;
        try
        {
            switch (args.Count)
            {
                case 3:
                    value = new SKColor((byte)ParseFloat(args[0].Expression), (byte)ParseFloat(args[1].Expression), (byte)ParseFloat(args[2].Expression));
                    return true;
                case 4:
                    value = new SKColor((byte)ParseFloat(args[0].Expression), (byte)ParseFloat(args[1].Expression), (byte)ParseFloat(args[2].Expression), (byte)ParseFloat(args[3].Expression));
                    return true;
                default:
                    return false;
            }
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Constructs a value of <paramref name="type"/> by finding one of its constructors that
    /// can accept exactly <paramref name="args"/>.Count arguments — either an exact parameter-count
    /// match, or a constructor with trailing optional parameters (<c>GridLength(float value,
    /// GridUnitType unitType = Pixel)</c> called as <c>new GridLength(50)</c>) filled in from their own
    /// defaults — and converting each supplied argument to that parameter's own type (via
    /// <see cref="TryEvaluateGeneric"/>, so a mixed numeric+enum constructor works same as an
    /// all-numeric one like <c>Thickness</c>/<c>Radius</c>/<c>SKSize</c>/<c>SKPoint</c>). Covers every
    /// plain layout/geometry struct Designer.cs code constructs inline without needing a case per type.</summary>
    private static bool TryConstruct(Type type, SeparatedSyntaxList<ArgumentSyntax> args, out object? value, Func<string, object?>? objectLookup = null)
    {
        value = null;
        if (args.Count == 0)
            return false;

        var ctor = type.GetConstructors()
            .Where(c => c.GetParameters().Length >= args.Count && c.GetParameters().Skip(args.Count).All(p => p.HasDefaultValue))
            .OrderBy(c => c.GetParameters().Length)
            .FirstOrDefault();
        if (ctor == null)
            return false;

        var parameters = ctor.GetParameters();
        var values = new object?[parameters.Length];
        try
        {
            for (var i = 0; i < parameters.Length; i++)
            {
                if (i < args.Count)
                {
                    if (!TryEvaluateGeneric(args[i].Expression, parameters[i].ParameterType, out values[i], objectLookup))
                        return false;
                }
                else
                {
                    values[i] = parameters[i].DefaultValue;
                }
            }
            value = ctor.Invoke(values);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Replays the <c>.Add(...)</c>/<c>.AddRange(...)</c> calls <see cref="NodeInfo.CollectionCalls"/>
    /// recorded against <paramref name="targetObject"/>'s own content collections (<c>ListBox.Items</c>,
    /// <c>GridList.Columns</c>/<c>Items</c>, <c>MenuStrip.Items</c>, ...) now that the real collection
    /// object exists to call them on.</summary>
    private static void ApplyCollectionCalls(object targetObject, NodeInfo info, Func<string, object?>? objectLookup = null)
    {
        foreach (var (memberName, methodName, args) in info.CollectionCalls)
        {
            var memberProperty = targetObject.GetType().GetProperty(memberName, BindingFlags.Public | BindingFlags.Instance);
            var collection = memberProperty?.GetValue(targetObject);
            if (collection == null)
                continue;

            var collectionType = collection.GetType();
            var candidateMethods = collectionType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.Name == methodName)
                .ToList();

            try
            {
                if (methodName == "Add")
                {
                    if (args.Count != 1)
                        continue;
                    foreach (var method in candidateMethods.Where(m => m.GetParameters().Length == 1))
                    {
                        if (TryEvaluateGeneric(args[0], method.GetParameters()[0].ParameterType, out var arg, objectLookup))
                        {
                            method.Invoke(collection, new[] { arg });
                            break;
                        }
                    }
                }
                else // AddRange
                {
                    var method = candidateMethods.FirstOrDefault(m =>
                        m.GetParameters().Length == 1
                        && (m.GetParameters()[0].ParameterType.IsArray
                            || (m.GetParameters()[0].ParameterType.IsGenericType
                                && m.GetParameters()[0].ParameterType.GetGenericTypeDefinition() == typeof(IEnumerable<>))));
                    if (method == null)
                        continue;

                    var paramType = method.GetParameters()[0].ParameterType;
                    var elementType = paramType.IsArray ? paramType.GetElementType()! : paramType.GetGenericArguments()[0];

                    var elementExprs = args.Count == 1 && args[0] is ArrayCreationExpressionSyntax or ImplicitArrayCreationExpressionSyntax or InitializerExpressionSyntax
                        ? args[0] switch
                        {
                            ArrayCreationExpressionSyntax { Initializer: { } ai } => ai.Expressions,
                            ImplicitArrayCreationExpressionSyntax { Initializer: { } iai } => iai.Expressions,
                            InitializerExpressionSyntax bi => bi.Expressions,
                            _ => default,
                        }
                        : new SeparatedSyntaxList<ExpressionSyntax>().AddRange(args);

                    var array = Array.CreateInstance(elementType, elementExprs.Count);
                    for (var i = 0; i < elementExprs.Count; i++)
                    {
                        if (!TryEvaluateGeneric(elementExprs[i], elementType, out var elementValue, objectLookup))
                        {
                            array = null;
                            break;
                        }
                        array.SetValue(elementValue, i);
                    }
                    if (array != null)
                        method.Invoke(collection, new object?[] { array });
                }
            }
            catch
            {
                // Best-effort: an item this importer can't fully reconstruct is dropped, not fatal to
                // the rest of the import.
            }
        }
    }

    /// <summary>Superset of <see cref="TryConvertValue"/> for values that appear as method-call
    /// arguments rather than plain property assignments — adds object-initializer construction
    /// (<c>new GridListColumn { Name = ..., Text = ... }</c>, a parameterless ctor plus per-property
    /// assignments, as opposed to <see cref="TryConstruct"/>'s constructor-argument struct shape) and
    /// array literals (<c>new[] { "Alpha", "1" }</c>, a GridList row's cell values).</summary>
    private static bool TryEvaluateGeneric(ExpressionSyntax expr, Type targetType, out object? value, Func<string, object?>? objectLookup = null)
    {
        if (TryConvertValue(expr, targetType, out value, objectLookup))
            return true;

        switch (expr)
        {
            case ObjectCreationExpressionSyntax { Initializer: { } initializer }:
                return TryEvaluateObjectInitializer(targetType, initializer, out value, objectLookup);

            case ImplicitObjectCreationExpressionSyntax { Initializer: { } implicitInitializer }:
                return TryEvaluateObjectInitializer(targetType, implicitInitializer, out value, objectLookup);

            case ArrayCreationExpressionSyntax { Initializer: { } arrayInit } when targetType.IsArray:
                return TryEvaluateArray(targetType.GetElementType()!, arrayInit.Expressions, out value, objectLookup);
            case ImplicitArrayCreationExpressionSyntax { Initializer: { } implicitArrayInit } when targetType.IsArray:
                return TryEvaluateArray(targetType.GetElementType()!, implicitArrayInit.Expressions, out value, objectLookup);
            case InitializerExpressionSyntax bareArrayInit when targetType.IsArray:
                return TryEvaluateArray(targetType.GetElementType()!, bareArrayInit.Expressions, out value, objectLookup);

            default:
                return false;
        }
    }

    private static bool TryEvaluateObjectInitializer(Type targetType, InitializerExpressionSyntax initializer, out object? value, Func<string, object?>? objectLookup = null)
    {
        value = null;
        if (targetType.GetConstructor(Type.EmptyTypes) == null)
            return false;

        try
        {
            var instance = Activator.CreateInstance(targetType)!;
            foreach (var member in initializer.Expressions.OfType<AssignmentExpressionSyntax>())
            {
                if (member.Left is not IdentifierNameSyntax { Identifier.Text: var propertyName })
                    continue;
                var descriptor = TypeDescriptor.GetProperties(instance)[propertyName];
                if (descriptor == null || descriptor.IsReadOnly)
                    continue;
                if (TryEvaluateGeneric(member.Right, descriptor.PropertyType, out var propValue, objectLookup))
                    descriptor.SetValue(instance, propValue);
            }
            value = instance;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryEvaluateArray(Type elementType, SeparatedSyntaxList<ExpressionSyntax> elements, out object? value, Func<string, object?>? objectLookup = null)
    {
        value = null;
        var array = Array.CreateInstance(elementType, elements.Count);
        for (var i = 0; i < elements.Count; i++)
        {
            if (!TryEvaluateGeneric(elements[i], elementType, out var elementValue, objectLookup))
                return false;
            array.SetValue(elementValue, i);
        }
        value = array;
        return true;
    }

    private static void ApplyExtraPropertiesToObject(object target, NodeInfo info, Func<string, object?>? objectLookup = null)
    {
        var properties = TypeDescriptor.GetProperties(target);
        foreach (var (propName, expr) in info.ExtraProperties)
        {
            var descriptor = properties[propName];
            if (descriptor == null || descriptor.IsReadOnly)
                continue;
            if (TryConvertValue(expr, descriptor.PropertyType, out var value, objectLookup))
            {
                try { descriptor.SetValue(target, value); }
                catch { }
            }
        }
    }

    private static string ResolveProjectAncestorType(
        string typeName,
        Dictionary<string, ProjectClassInfo> projectClasses,
        Dictionary<string, string> localBaseTypeOf,
        Dictionary<string, ControlEntry> catalog)
    {
        var current = typeName;
        for (var i = 0; i < 16; i++)
        {
            if (catalog.ContainsKey(current))
                return current;
            if (current is "Container" or "UserControl" or "Panel" or "Form" or "Window")
                return "Container";
            if (current is "Element" or "ElementBase" or "Control")
                return "Element";

            if (localBaseTypeOf.TryGetValue(current, out var nextLocal))
                current = nextLocal;
            else if (projectClasses.TryGetValue(current, out var nextProj) && nextProj.BaseType != null)
                current = nextProj.BaseType;
            else
                break;
        }

        return ResolveKnownAncestorType(current, localBaseTypeOf, catalog);
    }

    private static void LoadChildControlsFromDesigner(
        ElementBase parentControl,
        string designerPath,
        Dictionary<string, ControlEntry> catalog,
        HashSet<string> knownTypeNames,
        HashSet<string> usedNames)
    {
        try
        {
            var text = System.IO.File.ReadAllText(designerPath);
            var subNodes = new Dictionary<string, NodeInfo>(StringComparer.Ordinal);
            var subOrder = new List<string>();
            var subEdges = new List<(string ParentName, string ChildName)>();
            SKSize? subSize = null;
            var subDeclaredFields = new HashSet<string>(StringComparer.Ordinal);
            var subFieldTypes = new Dictionary<string, string>(StringComparer.Ordinal);

            var tree = CSharpSyntaxTree.ParseText(text);
            ProcessCompilationUnit(tree.GetCompilationUnitRoot(), knownTypeNames, subDeclaredFields, subFieldTypes, subNodes, subOrder, subEdges, ref subSize);

            var subInstances = new Dictionary<string, ElementBase>(StringComparer.Ordinal);
            foreach (var name in subOrder)
            {
                var info = subNodes[name];
                if (!catalog.TryGetValue(info.Type, out var entry))
                    continue;

                var child = entry.CreateInstance(seedPlaceholderContent: false);
                DesignSurface.PrepareForDesign(child);
                child.Name = DesignNameValidator.Normalize(info.Name, info.Type, usedNames);
                if (info.Text != null) child.Text = info.Text;
                child.Location = new SKPoint(info.X, info.Y);
                child.Size = new SKSize(info.W, info.H);
                child.Dock = info.Dock;
                child.Anchor = info.Anchor;
                child.Visible = info.Visible;
                ApplyExtraPropertiesToObject(child, info, key => subInstances.TryGetValue(key, out var ci) ? ci : null);
                subInstances[name] = child;
            }

            parentControl.SuspendLayout();
            try
            {
                foreach (var (pName, cName) in subEdges)
                {
                    if (!subInstances.TryGetValue(cName, out var child))
                        continue;
                    if (pName.Length == 0)
                        parentControl.Controls.Add(child);
                    else if (subInstances.TryGetValue(pName, out var innerParent))
                        innerParent.Controls.Add(child);
                }
            }
            finally
            {
                parentControl.ResumeLayout(false);
            }

            foreach (var (name, child) in subInstances)
            {
                if (!subNodes.TryGetValue(name, out var info))
                    continue;
                if (info.HasLocation)
                    child.Location = new SKPoint(info.X, info.Y);
                if (info.HasSize)
                    child.Size = new SKSize(info.W, info.H);
            }
        }
        catch { }
    }

    private static IReadOnlyList<string> Rebuild(
        DesignSurface surface,
        SKSize? clientSize,
        Dictionary<string, NodeInfo> nodes,
        List<string> declarationOrder,
        List<(string ParentName, string ChildName)> addEdges,
        Dictionary<string, string> localBaseTypeOf,
        Dictionary<string, ProjectClassInfo> projectClasses)
    {
        var skipped = new List<string>();
        var catalog = ControlCatalog.Discover().ToDictionary(e => e.DisplayName, StringComparer.Ordinal);
        var instances = new Dictionary<string, ElementBase>(StringComparer.Ordinal);
        var nonElementObjects = new Dictionary<string, object>(StringComparer.Ordinal);
        var usedNames = new HashSet<string>(StringComparer.Ordinal);
        var knownTypeNames = catalog.Keys.ToHashSet(StringComparer.Ordinal);

        object? LookupObject(string key) =>
            nonElementObjects.TryGetValue(key, out var no) ? no : instances.TryGetValue(key, out var inst) ? inst : null;

        foreach (var name in declarationOrder)
        {
            var info = nodes[name];

            // 1. Menu items: MenuItem does not inherit from ElementBase, but is a first-class UI element for MenuStrip / ContextMenuStrip.
            if (string.Equals(info.Type, "MenuItem", StringComparison.OrdinalIgnoreCase))
            {
                var menuItem = new MenuItem(info.Text ?? string.Empty);
                menuItem.Name = DesignNameValidator.Normalize(info.Name, info.Type, usedNames);
                menuItem.Visible = info.Visible;
                if (info.W > 0 && info.H > 0)
                    menuItem.Size = new SKSize(info.W, info.H);
                ApplyExtraPropertiesToObject(menuItem, info, LookupObject);
                nonElementObjects[name] = menuItem;
                continue;
            }

            // 2. ContextMenuStrip: overlay menu component.
            if (string.Equals(info.Type, "ContextMenuStrip", StringComparison.OrdinalIgnoreCase))
            {
                var cms = new ContextMenuStrip();
                cms.Name = DesignNameValidator.Normalize(info.Name, info.Type, usedNames);
                ApplyExtraPropertiesToObject(cms, info, LookupObject);
                nonElementObjects[name] = cms;
                continue;
            }

            // 3. Element controls: resolve through catalog, local base classes, or project custom classes.
            ControlEntry? entry = null;
            if (catalog.TryGetValue(info.Type, out var directEntry))
            {
                entry = directEntry;
            }
            else
            {
                var resolvedName = ResolveKnownAncestorType(info.Type, localBaseTypeOf, catalog);
                if (catalog.TryGetValue(resolvedName, out var ancestorEntry))
                {
                    entry = ancestorEntry;
                }
                else if (projectClasses.TryGetValue(info.Type, out var pci) && pci.BaseType != null)
                {
                    var projectResolved = ResolveProjectAncestorType(pci.BaseType, projectClasses, localBaseTypeOf, catalog);
                    if (catalog.TryGetValue(projectResolved, out var projEntry))
                        entry = projEntry;
                }
            }

            ElementBase control;
            if (entry != null)
            {
                control = entry.CreateInstance(seedPlaceholderContent: false);
            }
            else
            {
                // Fallback for custom controls defined in the project or external libraries:
                // If it has children added to it, treat as Container; otherwise Element.
                var isContainer = addEdges.Any(e => e.ParentName == name)
                    || (projectClasses.TryGetValue(info.Type, out var pc) && pc.BaseType is "Container" or "UserControl" or "Panel" or "Form" or "Window");
                control = isContainer ? new Orivy.Controls.Container() : new Element();
                skipped.Add(info.Type);
            }

            DesignSurface.PrepareForDesign(control);
            control.Name = DesignNameValidator.Normalize(info.Name, info.Type, usedNames);
            if (info.Text != null)
                control.Text = info.Text;
            else if (entry == null && control is not Orivy.Controls.Container)
                control.Text = $"[{info.Type}]";

            control.Location = new SKPoint(info.X, info.Y);
            control.Size = new SKSize(info.W, info.H);
            control.Dock = info.Dock;
            control.Anchor = info.Anchor;
            control.Visible = info.Visible;

            ApplyExtraPropertiesToObject(control, info, LookupObject);

            // If the custom control has its own .Designer.cs file in the project, load its inner child controls!
            if (projectClasses.TryGetValue(info.Type, out var classInfo)
                && !string.IsNullOrEmpty(classInfo.DesignerPath)
                && System.IO.File.Exists(classInfo.DesignerPath))
            {
                LoadChildControlsFromDesigner(control, classInfo.DesignerPath, catalog, knownTypeNames, usedNames);
            }

            instances[name] = control;
        }

        // Replay collection calls now that all controls and non-element items (like MenuItems) exist!
        foreach (var (name, control) in instances)
        {
            if (nodes.TryGetValue(name, out var info))
                ApplyCollectionCalls(control, info, LookupObject);
        }
        foreach (var (name, obj) in nonElementObjects)
        {
            if (nodes.TryGetValue(name, out var info))
                ApplyCollectionCalls(obj, info, LookupObject);
        }

        // Only commit to the live surface once parsing has fully succeeded — a half-built tree from
        // a partially-broken paste is worse than leaving the existing design alone.
        surface.Selection.Clear();
        foreach (var existing in surface.DesignedControls.ToList())
            surface.DesignRoot.Controls.Remove(existing);
        surface.Locked.Clear();
        surface.Groups.Clear();
        surface.DeletedControlNames.Clear();
        surface.AddedControlNames.Clear();

        if (clientSize is { Width: > 0, Height: > 0 })
            surface.DesignRoot.Size = clientSize.Value;

        surface.DesignRoot.SuspendLayout();
        try
        {
            foreach (var (parentName, childName) in addEdges)
            {
                if (!instances.TryGetValue(childName, out var child))
                    continue;

                if (parentName.Length == 0)
                {
                    surface.DesignRoot.Controls.Add(child);
                }
                else if (instances.TryGetValue(parentName, out var parent))
                {
                    parent.Controls.Add(child);
                    surface.Groups.Add(parent);
                }
            }
        }
        finally
        {
            surface.DesignRoot.ResumeLayout(false);
        }

        // Dock/Anchor layout during Controls.Add rewrites right-anchored Location into overflow
        // storage (negative X). The designer must keep the file's coordinates so drag stays 1:1
        // and CodeMerger does not persist those rewritten values over the original source.
        foreach (var (name, control) in instances)
        {
            if (!nodes.TryGetValue(name, out var info))
                continue;
            if (info.HasLocation)
                control.Location = new SKPoint(info.X, info.Y);
            if (info.HasSize)
                control.Size = new SKSize(info.W, info.H);
        }

        surface.Commands.Clear();
        surface.NotifyStructureChanged();
        return skipped;
    }

    /// <summary>Best-effort resolution of an unrecognized declared type down to the nearest catalog
    /// type it's actually built on, so a codebase's own control subclasses import as their closest
    /// known ancestor instead of getting silently dropped. First walks <paramref name="localBaseTypeOf"/>
    /// (base-type chains for classes declared in this same file/paste — the only ones a syntax-only
    /// importer can actually see); if that doesn't reach a catalog name (most often because the
    /// subclass's own declaration lives in a different file this importer never saw), falls back to
    /// the longest catalog type name that <paramref name="typeName"/> ends with (e.g. "ThemedButton"
    /// → "Button") — a heuristic, but subclasses overwhelmingly name themselves after what they extend.
    /// Returns <paramref name="typeName"/> unchanged if neither approach finds anything.</summary>
    private static string ResolveKnownAncestorType(string typeName, Dictionary<string, string> localBaseTypeOf, Dictionary<string, ControlEntry> catalog)
    {
        var current = typeName;
        var guard = 0;
        while (guard++ < 16 && localBaseTypeOf.TryGetValue(current, out var baseType))
        {
            if (catalog.ContainsKey(baseType))
                return baseType;
            current = baseType;
        }

        var bestMatch = catalog.Keys
            .Where(known => typeName.Length > known.Length && typeName.EndsWith(known, StringComparison.Ordinal))
            .OrderByDescending(known => known.Length)
            .FirstOrDefault();

        return bestMatch ?? typeName;
    }

    private static float ParseFloat(ExpressionSyntax expression)
    {
        // Older/VS-generated Designer.cs commonly wraps numeric literals in redundant parens and/or
        // casts (e.g. `((int)(800D))`, seen in ClientSize/Location/Size assignments) — unwrapping them
        // first matters a lot here: falling through to the string-literal parse below on the ORIGINAL
        // (un-unwrapped) expression text (e.g. "((int)(800D))") isn't valid float syntax and silently
        // parsed as 0, which is how a real form's ClientSize ended up looking like it was never set.
        while (true)
        {
            switch (expression)
            {
                case ParenthesizedExpressionSyntax paren:
                    expression = paren.Expression;
                    continue;
                case CastExpressionSyntax cast:
                    expression = cast.Expression;
                    continue;
                default:
                    goto unwrapped;
            }
        }

        unwrapped:
        // Handles a plain numeric literal and a unary-minus literal (e.g. "-12"); anything stranger
        // in a hand-edited file just falls back to 0 rather than throwing.
        return expression switch
        {
            LiteralExpressionSyntax { Token.Value: int i } => i,
            LiteralExpressionSyntax { Token.Value: float f } => f,
            LiteralExpressionSyntax { Token.Value: double d } => (float)d,
            PrefixUnaryExpressionSyntax { OperatorToken.Text: "-", Operand: var operand } => -ParseFloat(operand),
            _ => float.TryParse(expression.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0f,
        };
    }

    /// <summary>Parses a <c>DockStyle.Xxx</c> member access (as emitted by <see cref="CodeGenerator"/>);
    /// anything else in a hand-edited file just falls back to <see cref="DockStyle.None"/>.</summary>
    private static DockStyle ParseDock(ExpressionSyntax expression) =>
        expression is MemberAccessExpressionSyntax { Name.Identifier.Text: var name } && Enum.TryParse<DockStyle>(name, out var dock)
            ? dock
            : DockStyle.None;

    /// <summary>Parses a single <c>AnchorStyles.Xxx</c> member access or an <c>A | B | ...</c> chain of
    /// them (as emitted by <see cref="CodeGenerator"/>); anything unrecognized in a hand-edited file
    /// just falls back to <see cref="AnchorStyles.None"/> for that term.</summary>
    private static AnchorStyles ParseAnchor(ExpressionSyntax expression)
    {
        if (expression is BinaryExpressionSyntax { OperatorToken.Text: "|" } binary)
            return ParseAnchor(binary.Left) | ParseAnchor(binary.Right);

        return expression is MemberAccessExpressionSyntax { Name.Identifier.Text: var name } && Enum.TryParse<AnchorStyles>(name, out var anchor)
            ? anchor
            : AnchorStyles.None;
    }
}
