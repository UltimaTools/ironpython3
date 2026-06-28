// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Scripting;
using Microsoft.Scripting.Hosting;

namespace IronPython.Hosting {

    /// <summary>
    /// Captures the result of executing a script: the evaluated return value and a
    /// snapshot of the script-local variables bound in the scope. The state can be
    /// continued with additional code via <see cref="ContinueWithAsync"/>.
    /// </summary>
    /// <typeparam name="T">The static type of the script's return value.</typeparam>
    public sealed class ScriptState<T> {

        private readonly ScriptEngine _engine;
        private readonly ScriptScope _scope;

        internal ScriptState(ScriptEngine engine, ScriptScope scope, T returnValue) {
            _engine = engine;
            _scope = scope;
            ReturnValue = returnValue;
        }

        /// <summary>
        /// Gets the value returned by the script (the value of an expression for
        /// <c>Evaluate</c>, or <c>default</c> for statement-only execution).
        /// </summary>
        public T ReturnValue { get; }

        /// <summary>
        /// Gets the variables currently bound in the script's scope, keyed by name.
        /// </summary>
        public IReadOnlyDictionary<string, ScriptVariable> Variables {
            get {
                var result = new Dictionary<string, ScriptVariable>(StringComparer.Ordinal);
                foreach (var name in _scope.GetVariableNames()) {
                    object? value = _scope.GetVariable(name);
                    result[name] = new ScriptVariable(name, value?.GetType() ?? typeof(object), value);
                }
                return result;
            }
        }

        /// <summary>
        /// Continues execution in the same scope, compiling and running
        /// <paramref name="code"/> with cancellation injection enabled. Variables
        /// defined by earlier runs remain visible.
        /// </summary>
        public async Task<ScriptState<T>> ContinueWithAsync(string code, CancellationToken cancellationToken = default) {
            if (code == null) {
                throw new ArgumentNullException(nameof(code));
            }

            cancellationToken.ThrowIfCancellationRequested();

            var compiled = PythonScriptRunner.Compile(_engine, code, SourceCodeKind.Statements);
            await PythonScriptRunner.ExecuteAsync(compiled, _scope, cancellationToken).ConfigureAwait(false);

            // Statements don't yield a value; the carried return value is unchanged.
            return new ScriptState<T>(_engine, _scope, ReturnValue);
        }

        /// <summary>
        /// Continues execution in the same scope, compiling and running
        /// <paramref name="code"/> as an expression and returning a new state whose
        /// <see cref="ScriptState{TResult}.ReturnValue"/> is the expression result.
        /// </summary>
        public async Task<ScriptState<TResult>> ContinueWithExpressionAsync<TResult>(string code, CancellationToken cancellationToken = default) {
            if (code == null) {
                throw new ArgumentNullException(nameof(code));
            }

            cancellationToken.ThrowIfCancellationRequested();

            var compiled = PythonScriptRunner.Compile(_engine, code, SourceCodeKind.Expression);
            object? result = await PythonScriptRunner.ExecuteAsync(compiled, _scope, cancellationToken).ConfigureAwait(false);
            TResult typed = result == null ? default! : (TResult)result;
            return new ScriptState<TResult>(_engine, _scope, typed);
        }
    }
}
