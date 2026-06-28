// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;

namespace IronPython.Hosting {

    /// <summary>
    /// Holds options that influence how a script is compiled and executed by the
    /// <see cref="PythonScript"/> entry points. Instances are immutable; use the
    /// <c>Add</c>/<c>With</c> builder methods to derive new options.
    /// </summary>
    public sealed class PythonScriptOptions {

        /// <summary>
        /// Gets the default options: no extra references, no imports, no search paths.
        /// </summary>
        public static PythonScriptOptions Default { get; } = new PythonScriptOptions();

        private PythonScriptOptions() {
            References = Array.Empty<Assembly>();
            Imports = Array.Empty<string>();
            SearchPaths = Array.Empty<string>();
        }

        private PythonScriptOptions(IReadOnlyList<Assembly> references, IReadOnlyList<string> imports, IReadOnlyList<string> searchPaths) {
            References = references;
            Imports = imports;
            SearchPaths = searchPaths;
        }

        /// <summary>
        /// The assemblies made available to the script via <c>clr.AddReference</c>
        /// before it runs.
        /// </summary>
        public IReadOnlyList<Assembly> References { get; }

        /// <summary>
        /// The namespaces imported into the script scope (as <c>from X import *</c>)
        /// before it runs.
        /// </summary>
        public IReadOnlyList<string> Imports { get; }

        /// <summary>
        /// Additional directories searched for Python modules.
        /// </summary>
        public IReadOnlyList<string> SearchPaths { get; }

        /// <summary>
        /// Returns a new <see cref="PythonScriptOptions"/> that additionally makes the
        /// supplied assemblies available to the script (the equivalent of
        /// <c>clr.AddReference</c>).
        /// </summary>
        public PythonScriptOptions AddReferences(params Assembly[] assemblies) {
            if (assemblies == null || assemblies.Length == 0) {
                return this;
            }

            var combined = new List<Assembly>(References);
            combined.AddRange(assemblies);
            return new PythonScriptOptions(combined, Imports, SearchPaths);
        }

        /// <summary>
        /// Returns a new <see cref="PythonScriptOptions"/> that additionally performs
        /// <c>from &lt;namespace&gt; import *</c> for each supplied namespace before the
        /// script runs, making the namespace's public members available unqualified.
        /// </summary>
        public PythonScriptOptions AddImports(params string[] namespaces) {
            if (namespaces == null || namespaces.Length == 0) {
                return this;
            }

            var combined = new List<string>(Imports);
            combined.AddRange(namespaces);
            return new PythonScriptOptions(References, combined, SearchPaths);
        }

        /// <summary>
        /// Returns a new <see cref="PythonScriptOptions"/> that uses the supplied
        /// directories as the module search paths.
        /// </summary>
        public PythonScriptOptions WithSearchPaths(params string[] paths) {
            if (paths == null || paths.Length == 0) {
                return this;
            }

            var combined = new List<string>(SearchPaths);
            combined.AddRange(paths);
            return new PythonScriptOptions(References, Imports, combined);
        }
    }
}
