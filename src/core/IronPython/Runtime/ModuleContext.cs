// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Threading;
using Microsoft.Scripting.Runtime;
using Microsoft.Scripting.Utils;

namespace IronPython.Runtime {
    /// <summary>
    /// Captures the globals and other state of module code.
    /// </summary>
    public sealed class ModuleContext {
        private readonly PythonContext/*!*/ _pyContext;
        private readonly PythonDictionary/*!*/ _globals;
        private readonly CodeContext/*!*/ _globalContext;
        private readonly PythonModule _module;
        private ExtensionMethodSet _extensionMethods = ExtensionMethodSet.Empty;
        private ModuleOptions _features;
        private CancellationToken _cancellationToken;

        /// <summary>
        /// Creates a new ModuleContext which is backed by the specified dictionary.
        /// </summary>
        public ModuleContext(PythonDictionary/*!*/ globals, PythonContext/*!*/ creatingContext) {
            ContractUtils.RequiresNotNull(globals, nameof(globals));
            ContractUtils.RequiresNotNull(creatingContext, nameof(creatingContext));

            _globals = globals;
            _pyContext = creatingContext;
            _globalContext = new CodeContext(globals, this);
            _module = new PythonModule(globals);
            _module.Scope.SetExtension(_pyContext.ContextId, new PythonScopeExtension(_pyContext, _module, this));
        }

        /// <summary>
        /// Creates a new ModuleContext for the specified module.
        /// </summary>
        public ModuleContext(PythonModule/*!*/ module, PythonContext/*!*/ creatingContext) {
            ContractUtils.RequiresNotNull(module, nameof(module));
            ContractUtils.RequiresNotNull(creatingContext, nameof(creatingContext));

            _globals = module.__dict__;
            _pyContext = creatingContext;
            _globalContext = new CodeContext(_globals, this);
            _module = module;
        }

        /// <summary>
        /// Gets the dictionary used for the global variables in the module
        /// </summary>
        public PythonDictionary/*!*/ Globals {
            get {
                return _globals;
            }
        }

        /// <summary>
        /// Gets the language context which created this module.
        /// </summary>
        public PythonContext/*!*/ Context {
            get {
                return _pyContext;
            }
        }

        /// <summary>
        /// Gets the DLR Scope object which is associated with the modules dictionary.
        /// </summary>
        public Scope/*!*/ GlobalScope {
            get {
                return _module.Scope;
            }
        }

        /// <summary>
        /// Gets the global CodeContext object which is used for execution of top-level code.
        /// </summary>
        public CodeContext/*!*/ GlobalContext {
            get {
                return _globalContext;
            }
        }

        /// <summary>
        /// Gets the module object which this code is executing in.
        /// 
        /// This module may or may not be published in sys.modules.  For user defined
        /// code typically the module gets published at the start of execution.  But if
        /// this ModuleContext is attached to a Scope, or if we've just created a new
        /// module context for executing code it will not be in sys.modules.
        /// </summary>
        public PythonModule Module {
            get {
                return _module;
            }
        }

        /// <summary>
        /// Gets the features that code has been compiled with in the module.
        /// </summary>
        public ModuleOptions Features {
            get {
                return _features;
            }
            set {
                _features = value;
            }
        }

        /// <summary>
        /// Gets or sets whether code running in this context should display
        /// CLR members (for example .ToString on objects).
        /// </summary>
        public bool ShowCls {
            get {
                return (_features & ModuleOptions.ShowClsMethods) != 0;
            }
            set {
                Debug.Assert(this != _pyContext.SharedContext.ModuleContext || !value);
                if (value) {
                    _features |= ModuleOptions.ShowClsMethods;
                } else {
                    _features &= ~ModuleOptions.ShowClsMethods;
                }
            }
        }

        internal ExtensionMethodSet ExtensionMethods {
            get {
                return _extensionMethods;
            }
            set {
                _extensionMethods = value;
            }
        }

        /// <summary>
        /// Gets or sets the <see cref="CancellationToken"/> that flows through all
        /// code executing against this module context. When the compiler has emitted
        /// cancellation checks (see <see cref="Compiler.PythonCompilerOptions.InjectCancellationChecks"/>),
        /// generated code probes this token at loop back-edges and line boundaries and
        /// raises <see cref="OperationCanceledException"/> if cancellation has been
        /// requested.
        /// </summary>
        /// <remarks>
        /// Because every nested <see cref="CodeContext"/> (function, class, module)
        /// shares the same <see cref="ModuleContext"/> instance, setting this once at
        /// the start of a run propagates the token automatically to all nested code
        /// without any further plumbing.
        /// </remarks>
        public CancellationToken CancellationToken {
            get {
                return _cancellationToken;
            }
            set {
                _cancellationToken = value;
            }
        }

        /// <summary>
        /// Initializes __builtins__ for the module scope.
        /// </summary>
        internal void InitializeBuiltins(bool moduleBuiltins) {
            // adds __builtin__ variable if necessary.  Python adds the module directly to
            // __main__ and __builtin__'s dictionary for all other modules.  Our callers
            // pass the appropriate flags to control this behavior.
            if (!Globals.ContainsKey("__builtins__")) {
                if (moduleBuiltins) {
                    Globals["__builtins__"] = Context.BuiltinModuleInstance;
                } else {
                    Globals["__builtins__"] = Context.BuiltinModuleDict;
                }
            }
        }
    }
}
