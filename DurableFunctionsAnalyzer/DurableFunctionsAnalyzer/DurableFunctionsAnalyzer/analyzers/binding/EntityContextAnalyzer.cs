﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace DurableFunctionsAnalyzer.analyzers.binding
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    class EntityContextAnalyzer: DiagnosticAnalyzer
    {
        public const string DiagnosticId = "DF0202";

        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.EntityContextAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.EntityContextAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.EntityContextAnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = "EntityContextAnalyzer";
        public const DiagnosticSeverity severity = DiagnosticSeverity.Error;

        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, severity, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(FindEntityTriggers, SyntaxKind.Attribute);
        }

        public void FindEntityTriggers(SyntaxNodeAnalysisContext context)
        {
            var attribute = context.Node as AttributeSyntax;

            if (string.Equals(attribute.ToString(), "EntityTrigger"))
            {
                if (SyntaxNodeUtils.TryGetParameterNodeNextToAttribute(out SyntaxNode parameterNode, attribute, context))
                {
                    var paramTypeName = parameterNode.ToString();
                    if (!string.Equals(paramTypeName, "IDurableEntityContext"))
                    {
                        var diagnostic = Diagnostic.Create(Rule, parameterNode.GetLocation(), paramTypeName);

                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }
    }
}
