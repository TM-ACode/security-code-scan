﻿using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using SecurityCodeScan.Analyzers.Locale;
using SecurityCodeScan.Analyzers.Taint;
using SecurityCodeScan.Analyzers.Utils;
using SecurityCodeScan.Config;
using CSharp = Microsoft.CodeAnalysis.CSharp;
using CSharpSyntax = Microsoft.CodeAnalysis.CSharp.Syntax;
using VB = Microsoft.CodeAnalysis.VisualBasic;
using VBSyntax = Microsoft.CodeAnalysis.VisualBasic.Syntax;

namespace SecurityCodeScan.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class WeakPasswordValidatorPropertyAnalyzerCSharp : TaintAnalyzerExtensionCSharp
    {
        private readonly WeakPasswordValidatorPropertyAnalyzer Analyzer = new WeakPasswordValidatorPropertyAnalyzer();

        public WeakPasswordValidatorPropertyAnalyzerCSharp()
        {
            TaintAnalyzerCSharp.RegisterExtension(this);
        }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(ctx => Analyzer.VisitAssignmentExpression(ctx, CSharpSyntaxNodeHelper.Default), CSharp.SyntaxKind.SimpleAssignmentExpression);
        }

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => Analyzer.SupportedDiagnostics;

        public override void VisitEnd(SyntaxNode node, ExecutionState state)
        {
            Analyzer.CheckState(state);
        }

        public override void VisitAssignment(CSharpSyntax.AssignmentExpressionSyntax node,
                                             ExecutionState                          state,
                                             MethodBehavior                          behavior,
                                             ISymbol                                 symbol,
                                             VariableState                           variableRightState)
        {
            if (node != null)
                Analyzer.TagVariables(symbol, variableRightState);
        }
    }

    [DiagnosticAnalyzer(LanguageNames.VisualBasic)]
    public class WeakPasswordValidatorPropertyAnalyzerVisualBasic : TaintAnalyzerExtensionVisualBasic
    {
        private readonly WeakPasswordValidatorPropertyAnalyzer Analyzer = new WeakPasswordValidatorPropertyAnalyzer();

        public WeakPasswordValidatorPropertyAnalyzerVisualBasic()
        {
            TaintAnalyzerVisualBasic.RegisterExtension(this);
        }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(ctx => Analyzer.VisitAssignmentExpression(ctx, VBSyntaxNodeHelper.Default),
                                             VB.SyntaxKind.SimpleAssignmentStatement,
                                             VB.SyntaxKind.NamedFieldInitializer);
        }

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => Analyzer.SupportedDiagnostics;

        public override void VisitEnd(SyntaxNode node, ExecutionState state)
        {
            Analyzer.CheckState(state);
        }

        public override void VisitAssignment(VB.VisualBasicSyntaxNode node,
                                             ExecutionState           state,
                                             MethodBehavior           behavior,
                                             ISymbol                  symbol,
                                             VariableState            variableRightState)
        {
            if (node is VBSyntax.AssignmentStatementSyntax || node is VBSyntax.NamedFieldInitializerSyntax)
                Analyzer.TagVariables(symbol, variableRightState);
        }
    }

    internal class WeakPasswordValidatorPropertyAnalyzer
    {
        private static readonly DiagnosticDescriptor RulePasswordLength                  = LocaleUtil.GetDescriptor("SCS0032"); // RequiredLength's value is too small
        private static readonly DiagnosticDescriptor RulePasswordValidators              = LocaleUtil.GetDescriptor("SCS0033"); // Not enough properties set
        private static readonly DiagnosticDescriptor RuleRequiredPasswordValidators      = LocaleUtil.GetDescriptor("SCS0034"); // Required property must be set

        public ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(RulePasswordLength,
                                                                                                  RulePasswordValidators,
                                                                                                  RuleRequiredPasswordValidators);

        public void VisitAssignmentExpression(SyntaxNodeAnalysisContext ctx, SyntaxNodeHelper nodeHelper)
        {
            SyntaxNode right = nodeHelper.GetAssignmentRightNode(ctx.Node);
            SyntaxNode left = nodeHelper.GetAssignmentLeftNode(ctx.Node);

            var symbol = ctx.SemanticModel.GetSymbolInfo(left).Symbol;

            var content = right.GetText().ToString();

            // Only if it is the RequiredLength property of a PasswordValidator
            if (!AnalyzerUtil.SymbolMatch(symbol, type: "PasswordValidator", name: "RequiredLength") ||
                content == string.Empty)
            {
                return;
            }

            var requiredLength = ConfigurationManager.Instance.GetProjectConfiguration(ctx.Options.AdditionalFiles).PasswordValidatorRequiredLength;
            // Validates that the value is an int and that it is over the minimum value required
            if (!int.TryParse(right.GetText().ToString(), out var numericValue) ||
                numericValue >= requiredLength)
            {
                return;
            }

            var diagnostic = Diagnostic.Create(RulePasswordLength, ctx.Node.GetLocation(), requiredLength);
            ctx.ReportDiagnostic(diagnostic);
        }

        public  void CheckState(ExecutionState state)
        {
            // For every variables registered in state
            foreach (var variableState in state.VariableStates)
            {
                var st = variableState.Value;

                // Only if it is the constructor of the PasswordValidator instance
                if (!AnalyzerUtil.SymbolMatch(state.GetSymbol(st.Node), "PasswordValidator", ".ctor"))
                    continue;

                var minimumRequiredProperties = ConfigurationManager
                                                .Instance.GetProjectConfiguration(state.AnalysisContext.Options.AdditionalFiles)
                                                .MinimumPasswordValidatorProperties;
                // If the PasswordValidator instance doesn't have enough properties set
                if (st.Tags.Count < minimumRequiredProperties)
                {
                    state.AnalysisContext.ReportDiagnostic(Diagnostic.Create(RulePasswordValidators,
                                                                             variableState.Value.Node.GetLocation(), minimumRequiredProperties));
                }

                var requiredProperties = ConfigurationManager.Instance.GetProjectConfiguration(state.AnalysisContext.Options.AdditionalFiles)
                                                             .PasswordValidatorRequiredProperties;

                if (!st.Tags.Contains(VariableTag.RequiredLengthIsSet) && requiredProperties.Contains("RequiredLength"))
                {
                    state.AnalysisContext.ReportDiagnostic(Diagnostic.Create(RuleRequiredPasswordValidators,
                                                                             variableState.Value.Node.GetLocation(), "RequiredLength"));
                }

                if (!st.Tags.Contains(VariableTag.RequireDigitIsSet) && requiredProperties.Contains("RequireDigit"))
                {
                    state.AnalysisContext.ReportDiagnostic(Diagnostic.Create(RuleRequiredPasswordValidators,
                                                                             variableState.Value.Node.GetLocation(), "RequireDigit"));
                }

                if (!st.Tags.Contains(VariableTag.RequireLowercaseIsSet) && requiredProperties.Contains("RequireLowercase"))
                {
                    state.AnalysisContext.ReportDiagnostic(Diagnostic.Create(RuleRequiredPasswordValidators,
                                                                             variableState.Value.Node.GetLocation(), "RequireLowercase"));
                }

                if (!st.Tags.Contains(VariableTag.RequireNonLetterOrDigitIsSet) && requiredProperties.Contains("RequireNonLetterOrDigit"))
                {
                    state.AnalysisContext.ReportDiagnostic(Diagnostic.Create(RuleRequiredPasswordValidators,
                                                                             variableState.Value.Node.GetLocation(), "RequireNonLetterOrDigit"));
                }

                if (!st.Tags.Contains(VariableTag.RequireUppercaseIsSet) && requiredProperties.Contains("RequireUppercase"))
                {
                    state.AnalysisContext.ReportDiagnostic(Diagnostic.Create(RuleRequiredPasswordValidators,
                                                                             variableState.Value.Node.GetLocation(), "RequireUppercase"));
                }
            }
        }

        public  void TagVariables(ISymbol symbol, VariableState variableRightState)
        {
            // Only PasswordValidator properties will cause a new tag to be added
            if (AnalyzerUtil.SymbolMatch(symbol, type: "PasswordValidator", name: "RequiredLength"))
            {
                variableRightState.AddTag(VariableTag.RequiredLengthIsSet);
            }
            else if (AnalyzerUtil.SymbolMatch(symbol, type: "PasswordValidator", name: "RequireDigit"))
            {
                variableRightState.AddTag(VariableTag.RequireDigitIsSet);
            }
            else if (AnalyzerUtil.SymbolMatch(symbol, type: "PasswordValidator", name: "RequireLowercase"))
            {
                variableRightState.AddTag(VariableTag.RequireLowercaseIsSet);
            }
            else if (AnalyzerUtil.SymbolMatch(symbol, type: "PasswordValidator", name: "RequireNonLetterOrDigit"))
            {
                variableRightState.AddTag(VariableTag.RequireNonLetterOrDigitIsSet);
            }
            else if (AnalyzerUtil.SymbolMatch(symbol, type: "PasswordValidator", name: "RequireUppercase"))
            {
                variableRightState.AddTag(VariableTag.RequireUppercaseIsSet);
            }
        }
    }
}
