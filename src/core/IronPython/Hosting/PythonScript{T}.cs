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
    /// A Python script that has been — or can be — eagerly compiled so that it can
    /// be executed many times against different globals with low per-run overhead.
    /// </summary>
    /// <typeparam name="T">
    /// The type of the value produced by the script. Use <see cref="PythonScript"/>
    /// (non-generic) semantics by creating a script that returns <see cref="object"/>
    /// for statement-only scripts.
    /// </typeparam>
    public sealed class PythonScript<T> {

        private readonly ScriptEngine _engine;
        private readonly PythonScriptOptions _options;
        private readonly string _code;
        private CompiledCode? _compiled;

        internal PythonScript(ScriptEngine engine, string code, PythonScriptOptions options) {
            _engine = engine;
            _code = code;
            _options = options;
        }

        /// <summary>
        /// Eagerly compiles the script so that syntax errors surface before the first
        /// run. Subsequent <see cref="RunAsync"/> calls reuse the compiled code.
        /// </summary>
        public void Compile() {
            if (_compiled != null) {
                return;
            }
            _compiled = PythonScriptRunner.Compile(_engine, _code, SourceCodeKind.Expression);
        }

        /// <summary>
        /// Runs the script against a fresh scope seeded with the public members of
        /// <paramref name="globals"/>. Cancellation is observed via the probes
        /// injected at compile time.
        /// </summary>
        public async Task<ScriptState<T>> RunAsync(object? globals = null, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();

            Compile();
            var scope = _engine.CreateScope();
            PythonScriptRunner.SeedGlobals(scope, globals, globals?.GetType());
            PythonScriptRunner.ApplyPreamble(_engine, scope, _options);

            object? result = await PythonScriptRunner.ExecuteAsync(_compiled!, scope, cancellationToken).ConfigureAwait(false);
            T typed = result == null ? default! : (T)result;
            return new ScriptState<T>(_engine, scope, typed);
        }
    }
}
