using Avalonia.Threading;

namespace PWManager.UITests;

internal static class UiTestPump
{
    public static void WaitFor(Task task)
    {
        while (!task.IsCompleted)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(1);
        }

        task.GetAwaiter().GetResult();
    }
}
