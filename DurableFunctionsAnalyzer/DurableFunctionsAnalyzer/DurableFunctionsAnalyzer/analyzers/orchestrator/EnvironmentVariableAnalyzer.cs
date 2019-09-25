﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace DurableFunctionsAnalyzer.analyzers.orchestrator
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class EnvironmentVariableAnalyzer: DiagnosticAnalyzer
    {
        public const string DiagnosticId = "DF0106";

        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.EnvironmentVariableAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.DeterministicAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.DeterministicAnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = "OrchestratorCodeConstraints";
        public const DiagnosticSeverity severity = DiagnosticSeverity.Warning;

        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, severity, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeIdentifierEnvironmentVariable, SyntaxKind.IdentifierName);
        }

        private static void AnalyzeIdentifierEnvironmentVariable(SyntaxNodeAnalysisContext context)
        {
            var identifierName = context.Node as IdentifierNameSyntax;
            if (identifierName != null)
            {
                var identifierText = identifierName.Identifier.ValueText;
                if (identifierText == "GetEnvironmentVariable" || identifierText == "GetEnvironmentVariables" || identifierText == "ExpandEnvironmentVariables")
                {
                    var memberAccessExpression = identifierName.Parent;
                    var invocationExpression = memberAccessExpression.Parent;
                    var memberSymbol = context.SemanticModel.GetSymbolInfo(memberAccessExpression).Symbol;

                    if (!memberSymbol?.ToString().StartsWith("System.Environment") ?? true)
                    {
                        return;
                    }
                    else if (!SyntaxNodeUtils.IsInsideOrchestrator(identifierName) && !SyntaxNodeUtils.IsMarkedDeterministic(identifierName))
                    {
                        return;
                    }
                    else
                    {
                        var diagnostic = Diagnostic.Create(Rule, invocationExpression.GetLocation(), memberAccessExpression);

                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }
    }
}
