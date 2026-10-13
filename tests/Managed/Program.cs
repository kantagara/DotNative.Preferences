using DotNative.Preferences;
var id = "DotNative.Preferences.Tests." + Guid.NewGuid().ToString("N");
var first = new FilePreferences(id);
try
{
    await first.SetAsync("locale", "sr");
    await first.SetAsync("counter", 42);
    var reopened = new FilePreferences(id);
    if (await reopened.GetAsync("locale", "en") != "sr" || await reopened.GetAsync("counter", 0) != 42)
        throw new Exception("Typed persistence failed");
    using var cancel = new CancellationTokenSource(); cancel.Cancel();
    try { await reopened.SetAsync("locale", "ru", cancel.Token); throw new Exception("Cancelled write committed"); }
    catch (OperationCanceledException) { }
    if (await first.GetAsync("locale", "en") != "sr") throw new Exception("Cancelled write changed data");
    await reopened.RemoveAsync("counter");
    if (await first.ContainsKeyAsync("counter")) throw new Exception("Remove failed");
    Console.WriteLine("PASS typed reopen, cancelled commit, remove");
}
finally { await first.ClearAsync(); }
