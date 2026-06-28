// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;

namespace IronPython.Hosting {

    /// <summary>
    /// Describes a script-local variable captured after execution of a script.
    /// Instances are returned from <see cref="ScriptState{T}.Variables"/>.
    /// </summary>
    public sealed class ScriptVariable {

        internal ScriptVariable(string name, Type type, object? value) {
            Name = name;
            Type = type;
            Value = value;
        }

        /// <summary>
        /// Gets the name of the variable as it appears in the script's scope.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the static type of the variable's value as observed by the host.
        /// This is <see cref="object"/> for most Python values since they are boxed.
        /// </summary>
        public Type Type { get; }

        /// <summary>
        /// Gets the current value of the variable, or <c>null</c> if the variable
        /// is not currently bound to a value.
        /// </summary>
        public object? Value { get; }
    }
}
