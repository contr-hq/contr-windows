using System.Collections.Generic;
using System.Threading.Tasks;

namespace Contr.Modules.Images.UI.Tests;

internal static class TestStreams
{
    /// <summary>A stream that completes synchronously, so a fire-and-forget reader finishes in the call.</summary>
    public static async IAsyncEnumerable<T> Of<T>(params T[] items)
    {
        await Task.CompletedTask;
        foreach (var item in items)
        {
            yield return item;
        }
    }
}
