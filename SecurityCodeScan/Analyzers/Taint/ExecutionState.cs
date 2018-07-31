﻿using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using SecurityCodeScan.Analyzers.Utils;

namespace SecurityCodeScan.Analyzers.Taint
{
    /// <summary>
    /// Execution state at a given statement. It is altered after each statement are evaluated.
    /// 
    /// Objectives of this class:
    ///  * Keep the state of each variable
    ///  * Keep reference to utilities that are require along the taint analysis. (Such as resolving symbol)
    /// </summary>
    public class ExecutionState
    {
        public  SyntaxNodeAnalysisContext                  AnalysisContext      { get; }
        public  IReadOnlyDictionary<string, VariableState> VariableStates       => Variables;
        private Dictionary<string, VariableState>          Variables            { get; }

        /// <summary>
        /// Initialize the state with no variable recorded yet.
        /// </summary>
        /// <param name="ctx">Context used to resolve symbol.</param>
        public ExecutionState(SyntaxNodeAnalysisContext ctx)
        {
            AnalysisContext = ctx;
            Variables       = new Dictionary<string, VariableState>();
        }

        public void AddNewValue(string identifier, VariableState value)
        {
            if (VariableStates.ContainsKey(identifier)) //New variable in a different scope
            {
                Logger.Log("Removing existing state for " + identifier);
                Variables.Remove(identifier);
            }

            Logger.Log($"Adding state for {identifier} ({value})");
            Variables.Add(identifier, value);
        }

        public void AddOrUpdateValue(string identifier, VariableState value)
        {
            if (VariableStates.ContainsKey(identifier)) //Override existing value
            {
                VariableStates[identifier].Replace(value);
                Logger.Log($"Updating state for {identifier} ({value})");
            }
            else
            {
                //Unexpected state
                Logger.Log($"Adding state for {identifier} ({value})");

                Variables.Add(identifier, value);
            }
        }

        /// <summary>
        /// Resolve semantic class name of a given variable reference.
        /// </summary>
        /// <param name="node">Expression to evaluate</param>
        /// <returns>The resolved symbol with the complete class name and method name.</returns>
        public ISymbol GetSymbol(SyntaxNode node)
        {
            try
            {
                return node != null ? AnalysisContext.SemanticModel.GetSymbolInfo(node).Symbol : null;
            }
            catch (ArgumentException) // todo: find better way to skip or load symbols outside the syntax tree
            {
                return null; // Syntax node is not within syntax tree
            }
        }
    }
}
