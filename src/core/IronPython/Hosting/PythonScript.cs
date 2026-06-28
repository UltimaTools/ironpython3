// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Scripting;
using Microsoft.Scripting.Hosting;

namespace IronPython.Hosting {

    /// <summary>
    /// A Roslyn-mirroring entry point for executing Python scripts with
    /// first-class <see cref="CancellationToken"/> support. When code is executed
    /// through this class the compiler injects lightweight cancellation probes at
    /// every loop back-edge and source line boundary, so runaway scripts can be
    /// cancelled promptly without cooperative polling from the script itself.
    /// </summary>
    public static class PythonScript {

        /// <summary>
        /// Runs <paramref name="code"/> as a statement sequence and returns the
        /// resulting script state.
        /// </summary>
        public static async Task<ScriptState<object>> RunAsync(
            string code,
            PythonScriptOptions? options = null,
            object? globals = null,
            Type? globalsType = null,
            CancellationToken cancellationToken = default) {

            if (code == null) {
                throw new ArgumentNullException(nameof(code));
            }

            cancellationToken.ThrowIfCancellationRequested();

            var engine = PythonScriptRunner.CreateEngine(options);
            var compiled = PythonScriptRunner.Compile(engine, code, SourceCodeKind.Statements);
            var scope = engine.CreateScope();

            PythonScriptRunner.SeedGlobals(scope, globals, globalsType);
            PythonScriptRunner.ApplyPreamble(engine, scope, options);

            await PythonScriptRunner.ExecuteAsync(compiled, scope, cancellationToken).ConfigureAwait(false);
            return new ScriptState<object>(engine, scope, null!);
        }

        /// <summary>
        /// Evaluates <paramref name="code"/> as an expression and returns its value
        /// converted to <typeparamref name="T"/>.
        /// </summary>
        public static async Task<T> EvaluateAsync<T>(
            string code,
            PythonScriptOptions? options = null,
            object? globals = null,
            Type? globalsType = null,
            CancellationToken cancellationToken = default) {

            if (code == null) {
                throw new ArgumentNullException(nameof(code));
            }

            cancellationToken.ThrowIfCancellationRequested();

            var engine = PythonScriptRunner.CreateEngine(options);
            var compiled = PythonScriptRunner.Compile(engine, code, SourceCodeKind.Expression);
            var scope = engine.CreateScope();

            PythonScriptRunner.SeedGlobals(scope, globals, globalsType);
            PythonScriptRunner.ApplyPreamble(engine, scope, options);

            object? result = await PythonScriptRunner.ExecuteAsync(compiled, scope, cancellationToken).ConfigureAwait(false);
            return result == null ? default! : (T)result;
        }

        /// <summary>
        /// Pre-compiles <paramref name="code"/> into a <see cref="PythonScript{T}"/>
        /// that can be executed many times with different globals. The cancellation
        /// probes are emitted at compile time, so every subsequent run is cancellable.
        /// </summary>
        public static PythonScript<T> Create<T>(
            string code,
            PythonScriptOptions? options = null,
            Type? globalsType = null) {

            if (code == null) {
                throw new ArgumentNullException(nameof(code));
            }

            var engine = PythonScriptRunner.CreateEngine(options);
            return new PythonScript<T>(engine, code, options ?? PythonScriptOptions.Default);
        }
    }
}
