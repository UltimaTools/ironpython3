// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using IronPython.Hosting;
using IronPython.Runtime;

using Microsoft.Scripting;
using Microsoft.Scripting.Hosting;

using NUnit.Framework;

namespace IronPythonTest {
    public class PythonScriptCancellationTest {

        [Test]
        public async Task EvaluateReturnsExpressionValue() {
            var result = await PythonScript.EvaluateAsync<int>("1 + 2");
            Assert.AreEqual(3, result);
        }

        [Test]
        public async Task EvaluateSeesGlobals() {
            var result = await PythonScript.EvaluateAsync<int>(
                "X + Y",
                globals: new { X = 10, Y = 5 });
            Assert.AreEqual(15, result);
        }

        [Test]
        public async Task RunExposesVariables() {
            var state = await PythonScript.RunAsync("a = 1\nb = 2");
            Assert.AreEqual(1, state.Variables["a"].Value);
            Assert.AreEqual(2, state.Variables["b"].Value);
        }

        [Test]
        public async Task ContinueWithSeesEarlierVariables() {
            var state = await PythonScript.RunAsync("x = 1");
            state = await state.ContinueWithAsync("x += 10");
            Assert.AreEqual(11, state.Variables["x"].Value);
        }

        [Test]
        public void PreCancelledTokenThrowsBeforeExecution() {
            var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.ThrowsAsync<OperationCanceledException>(() =>
                PythonScript.RunAsync("a = 1", cancellationToken: cts.Token));
        }

        [Test]
        public async Task InfiniteWhileLoopIsCancelled() {
            var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
            var start = DateTime.UtcNow;

            Assert.ThrowsAsync<OperationCanceledException>(() =>
                PythonScript.RunAsync("x = 0\nwhile True:\n    x += 1", cancellationToken: cts.Token));

            // Should bail out quickly after cancellation rather than hanging forever.
            Assert.Less(DateTime.UtcNow - start, TimeSpan.FromSeconds(10));
        }

        [Test]
        public async Task InfiniteForLoopIsCancelled() {
            var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

            Assert.ThrowsAsync<OperationCanceledException>(() =>
                PythonScript.RunAsync(
                    "def forever():\n    while True:\n        yield 1\nfor _ in forever():\n    pass",
                    cancellationToken: cts.Token));
        }

        [Test]
        public async Task CancellationWorksInsideCalledFunction() {
            // This is the key propagation test: the loop lives in a *called* function,
            // so the token must flow through CreateLocalContext into the nested scope.
            var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

            Assert.ThrowsAsync<OperationCanceledException>(() =>
                PythonScript.RunAsync(
                    "def loop():\n    while True:\n        pass\nloop()",
                    cancellationToken: cts.Token));
        }

        [Test]
        public async Task LongSequentialScriptIsCancelledAtLineBoundary() {
            // A long sequence of no-op statements with no loops should still be
            // cancellable via the per-line probes.
            var lines = Enumerable.Range(0, 100000).Select(_ => "x = 1");
            var code = string.Join("\n", lines);

            var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

            Assert.ThrowsAsync<OperationCanceledException>(() =>
                PythonScript.RunAsync(code, cancellationToken: cts.Token));
        }

        [Test]
        public async Task PreCompiledScriptCompilesAndRuns() {
            var script = PythonScript.Create<int>("3 * 4");
            script.Compile();

            var state = await script.RunAsync();
            Assert.AreEqual(12, state.ReturnValue);
        }

        [Test]
        public async Task PreCompiledScriptSurfacesSyntaxErrorOnCompile() {
            var script = PythonScript.Create<int>("def f(");
            Assert.Throws<SyntaxErrorException>(script.Compile);
        }

        [Test]
        public async Task PreCompiledScriptRunsManyTimesWithDifferentGlobals() {
            var script = PythonScript.Create<int>("X * 2");
            script.Compile();

            var s1 = await script.RunAsync(globals: new { X = 3 });
            var s2 = await script.RunAsync(globals: new { X = 5 });

            Assert.AreEqual(6, s1.ReturnValue);
            Assert.AreEqual(10, s2.ReturnValue);
        }

        [Test]
        public async Task ComprehensionInfiniteLoopIsCancelled() {
            // Verifies that comprehension `for` clauses (which route through
            // ForStatement.TransformFor) also get the back-edge cancellation probe.
            var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

            Assert.ThrowsAsync<OperationCanceledException>(() =>
                PythonScript.RunAsync(
                    "def forever():\n    while True:\n        yield 1\n[x for x in forever()]",
                    cancellationToken: cts.Token));
        }

        [Test]
        public async Task GeneratorExpressionInfiniteLoopIsCancelled() {
            // Generator expressions also use TransformFor internally.
            var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

            Assert.ThrowsAsync<OperationCanceledException>(() =>
                PythonScript.RunAsync(
                    "def forever():\n    while True:\n        yield 1\nfor _ in (x for x in forever()):\n    pass",
                    cancellationToken: cts.Token));
        }

        [Test]
        public async Task NestedWhileLoopsCancelled() {
            // Doubly-nested infinite loop — both levels have back-edge probes.
            var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

            Assert.ThrowsAsync<OperationCanceledException>(() =>
                PythonScript.RunAsync(
                    "while True:\n    while True:\n        pass",
                    cancellationToken: cts.Token));
        }

        [Test]
        public async Task CancellationInsideClassBody() {
            // The class body's ModuleContext shares the outer one, so cancellation
            // propagates automatically without any special wiring.
            var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

            Assert.ThrowsAsync<OperationCanceledException>(() =>
                PythonScript.RunAsync(
                    "class X:\n    while True:\n        pass",
                    cancellationToken: cts.Token));
        }

        [Test]
        public void PreCompiledScriptExpressionOnly() {
            // PythonScript<T> compiles as an expression; multi-statement scripts
            // should fail with a syntax error. Use PythonScript.RunAsync for
            // statement sequences.
            Assert.Throws<SyntaxErrorException>(() => {
                var script = PythonScript.Create<int>("x = 1\ny = 2");
                script.Compile();
            });
        }
    }
}
