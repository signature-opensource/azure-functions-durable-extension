﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

namespace DurableFunctionsAnalyzer.analyzers.entity
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    class StaticFunctionAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "DF0306";

        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.EntityStaticAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.EntityStaticAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.EntityStaticAnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = "Entity";
        public const DiagnosticSeverity severity = DiagnosticSeverity.Warning;

        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, severity, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeAttributeClassName, SyntaxKind.Attribute);
        }
        
        private static void AnalyzeAttributeClassName(SyntaxNodeAnalysisContext context)
        {
            var attributeExpression = context.Node as AttributeSyntax;
            if (attributeExpression != null && attributeExpression.ChildNodes().First().ToString() == "EntityTrigger")
            {
                if (SyntaxNodeUtils.TryGetMethodDeclaration(out SyntaxNode methodDeclaration, attributeExpression))
                {
                    var staticKeyword = methodDeclaration.ChildTokens().Where(x => x.IsKind(SyntaxKind.StaticKeyword));
                    if (!staticKeyword.Any())
                    {
                        var methodName = methodDeclaration.ChildTokens().Where(x => x.IsKind(SyntaxKind.IdentifierToken)).First();
                        var diagnostic = Diagnostic.Create(Rule, methodName.GetLocation(), methodName);

                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }
    }
}
