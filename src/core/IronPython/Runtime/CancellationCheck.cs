// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace IronPython.Runtime {

    /// <summary>
    /// Lightweight cancellation probe emitted by the compiler at loop back-edges
    /// and line boundaries when cancellation injection is enabled
    /// (see <see cref="Compiler.PythonCompilerOptions.InjectCancellationChecks"/>).
    /// On the non-cancelled path this collapses to a single volatile boolean read;
    /// the method is marked <see cref="MethodImplOptions.AggressiveInlining"/> so it
    /// inlines into the call site with no call overhead.
    /// </summary>
    internal static class CancellationCheck {

        /// <summary>
        /// Probes the cancellation token stored on the supplied
        /// <see cref="CodeContext"/>'s <see cref="ModuleContext"/> and throws
        /// <see cref="OperationCanceledException"/> if cancellation has been
        /// requested.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is the form emitted by the compiler at generated back-edge and
        /// line-boundary check points. Because every nested <see cref="CodeContext"/>
        /// (top-level, function, class, module) shares the same
        /// <see cref="ModuleContext"/> instance, reading the token off the context
        /// means the token set once at run start is visible everywhere with no
        /// additional plumbing.
        /// </para>
        /// <para>
        /// The token is copied to a local before the check so that the
        /// <see cref="CancellationToken.IsCancellationRequested"/> field is read
        /// exactly once.
        /// </para>
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Check(CodeContext context) {
            context.ModuleContext.CancellationToken.ThrowIfCancellationRequested();
        }
    }
}
