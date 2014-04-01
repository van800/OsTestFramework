using System;
using System.Threading;

namespace JetBrains.OsTestFramework.Common
{
  public static class RetryUtility
  {
    public static void RetryAction(Action action, int numRetries, int retryWaitMiliseconds)
    {
      if (action == null)
        throw new ArgumentNullException("action"); // slightly safer...

      do
      {
        try { action(); return; }
        catch
        {
          if (numRetries <= 0) throw;  // improved to avoid silent failure
          else Thread.Sleep(retryWaitMiliseconds);
        }
      } while (numRetries-- > 0);
    }
  }
}
