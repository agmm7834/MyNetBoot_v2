using MyNetBoot.Server.Services;

Console.Title = "MyNetBoot Server";

var server = new ServerManager();

// Ctrl+C bilan to'xtatish
Console.CancelKeyPress += (s, e) =>
{
    e.Cancel = true;
    Console.WriteLine("\nTo'xtatilmoqda...");
    server.Stop();
    Environment.Exit(0);
};

try
{
    await server.StartAsync();

    // Server ishlab turadi
    while (true)
    {
        await Task.Delay(1000);
    }
}
catch (Exception ex)
{
    Console.WriteLine($"[FATAL ERROR] {ex.Message}");
    Console.WriteLine(ex.StackTrace);
    Environment.Exit(1);
}
