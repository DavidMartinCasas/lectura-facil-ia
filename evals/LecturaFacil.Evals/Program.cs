// SPDX-License-Identifier: Apache-2.0
using LecturaFacil.Evals.Running;

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

var runner = new EvalRunner(Console.In, Console.Out, TimeProvider.System, Environment.GetEnvironmentVariable);
return await runner.RunAsync(args, cancellation.Token);
