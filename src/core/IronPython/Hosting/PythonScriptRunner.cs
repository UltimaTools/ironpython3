// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Scripting;
using Microsoft.Scripting.Hosting;
using Microsoft.Scripting.Hosting.Providers;
using Microsoft.Scripting.Runtime;

using IronPython.Compiler;
using IronPython.Runtime;

namespace IronPython.Hosting {

    /// <summary>
    /// Internal bridge between the public <see cref="PythonScript"/> API and the
    /// DLR hosting layer. Responsible for creating engines/scopes, wiring up
    /// cancellation, seeding globals, and executing compiled code off the calling
    /// thread so that injected cancellation probes can actually interrupt runaway
    /// scripts.
    /// </summary>
    internal static class PythonScriptRunner {

        /// <summary>
        /// Creates a fresh IronPython engine configured with the search paths from
        /// <paramref name="options"/>.
        /// </summary>
        public static ScriptEngine CreateEngine(PythonScriptOptions? options) {
            var engine = Python.CreateEngine();
            if (options != null && options.SearchPaths.Count > 0) {
                var paths = new List<string>(engine.GetSearchPaths());
                paths.AddRange(options.SearchPaths);
                engine.SetSearchPaths(paths);
            }
            return engine;
        }

        /// <summary>
        /// Returns a <see cref="PythonCompilerOptions"/> that enables cancellation
        /// injection, suitable for passing to <see cref="ScriptSource.Compile(CompilerOptions)"/>.
        /// </summary>
        public static PythonCompilerOptions CreateCompilerOptions() {
            return new PythonCompilerOptions {
                InjectCancellationChecks = true
            };
        }

        /// <summary>
        /// Compiles <paramref name="code"/> as the given source kind with cancellation
        /// injection enabled. Returns a non-null <see cref="CompiledCode"/> or throws
        /// describing the first syntax error.
        /// </summary>
        public static CompiledCode Compile(ScriptEngine engine, string code, SourceCodeKind kind) {
            var source = engine.CreateScriptSourceFromString(code, kind);
            CompiledCode? compiled = source.Compile(CreateCompilerOptions());
            if (compiled == null) {
                throw new SyntaxErrorException("The supplied script could not be compiled.");
            }
            return compiled;
        }

        /// <summary>
        /// Seeds <paramref name="scope"/> with the public properties and fields of
        /// <paramref name="globals"/> so they are visible as top-level names to the
        /// script. <paramref name="globalsType"/> is accepted for Roslyn API parity
        /// but otherwise ignored — names are always discovered by reflection over
        /// <paramref name="globals"/>.
        /// </summary>
        public static void SeedGlobals(ScriptScope scope, object? globals, Type? globalsType) {
            if (globals == null) {
                return;
            }

            Type type = globalsType ?? globals.GetType();
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)) {
                if (property.GetIndexParameters().Length > 0) {
                    continue; // skip indexers
                }

                object? value;
                try {
                    value = property.GetValue(globals, null);
                } catch (TargetInvocationException ex) when (ex.InnerException != null) {
                    throw ex.InnerException;
                }
                scope.SetVariable(property.Name, value);
            }

            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance)) {
                scope.SetVariable(field.Name, field.GetValue(globals));
            }
        }

        /// <summary>
        /// Runs the references/imports preamble described by <paramref name="options"/>
        /// inside <paramref name="scope"/>. Failures here surface as Python exceptions.
        /// </summary>
        public static void ApplyPreamble(ScriptEngine engine, ScriptScope scope, PythonScriptOptions? options) {
            if (options == null || (options.References.Count == 0 && options.Imports.Count == 0)) {
                return;
            }

            if (options.References.Count > 0) {
                engine.Execute("import clr", scope);
                foreach (var assembly in options.References) {
                    scope.SetVariable("__ref", assembly);
                    engine.Execute("clr.AddReference(__ref)", scope);
                }
                scope.Engine.Execute("del __ref", scope);
            }

            foreach (var ns in options.Imports) {
                engine.Execute($"from {ns} import *", scope);
            }
        }

        /// <summary>
        /// Attaches <paramref name="cancellationToken"/> to the module context backing
        /// <paramref name="scope"/> so that cancellation probes injected into the
        /// compiled code can observe it.
        /// </summary>
        public static void AttachCancellation(ScriptEngine engine, ScriptScope scope, CancellationToken cancellationToken) {
            var pyContext = (PythonContext)HostingHelpers.GetLanguageContext(engine);
            var ext = pyContext.EnsureScopeExtension(HostingHelpers.GetScope(scope)) as PythonScopeExtension;
            ext?.ModuleContext.CancellationToken = cancellationToken;
        }

        /// <summary>
        /// Executes <paramref name="compiled"/> against <paramref name="scope"/>,
        /// offloading the CPU-bound run onto the thread pool so the supplied
        /// cancellation token can actually interrupt runaway scripts via the injected
        /// probes. Cancellation surfaces as <see cref="OperationCanceledException"/>;
        /// Python exceptions propagate in their host-visible .NET form.
        /// </summary>
        public static async Task<object?> ExecuteAsync(CompiledCode compiled, ScriptScope scope, CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();

            AttachCancellation(scope.Engine, scope, cancellationToken);

            try {
                return await Task.Run(() => compiled.Execute(scope), cancellationToken).ConfigureAwait(false);
            } catch (OperationCanceledException) {
                throw; // clean cancellation, propagate as-is
            } catch (AggregateException ag) when (ag.InnerException is OperationCanceledException) {
                throw ag.InnerException;
            }
        }
    }
}
