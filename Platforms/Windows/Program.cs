using Microsoft.UI.Dispatching;
using Velopack;
using WinRT;

namespace ChurchTimeTracker.WinUI;

public static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        VelopackApp.Build().Run();

        ComWrappersSupport.InitializeComWrappers();
        Microsoft.UI.Xaml.Application.Start(initialization =>
        {
            DispatcherQueueSynchronizationContext context = new(
                DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            new App();
        });
    }
}
