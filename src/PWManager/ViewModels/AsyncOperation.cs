using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace PWManager.ViewModels;

internal static class AsyncOperation
{
    public static void Forget(Func<Task> operation) => _ = ObserveAsync(operation);

    private static async Task ObserveAsync(Func<Task> operation)
    {
        try
        {
            await operation();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            Trace.TraceError($"Background operation failed: {exception}");
        }
    }
}
