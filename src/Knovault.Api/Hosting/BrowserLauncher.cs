using System.Diagnostics;

namespace Knovault.Api.Hosting;

public static class BrowserLauncher
{
    /// <summary>盡力而為地開啟預設瀏覽器；失敗不拋例外。</summary>
    public static void TryOpen(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch
        {
            // 無瀏覽器/無桌面環境時忽略
        }
    }
}
