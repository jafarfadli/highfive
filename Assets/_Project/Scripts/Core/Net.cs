using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Networking;

namespace SeoulFirstWeek.Core
{
    public static class Net
    {
        /// <summary>Sends the request and resumes on the main thread when it finishes.</summary>
        public static Task SendAsync(this UnityWebRequest request, CancellationToken ct)
        {
            var tcs = new TaskCompletionSource<bool>();
            var op = request.SendWebRequest();
            op.completed += _ => tcs.TrySetResult(true);
            if (ct.CanBeCanceled)
            {
                ct.Register(() =>
                {
                    if (tcs.TrySetCanceled())
                    {
                        try { request.Abort(); } catch { /* already disposed */ }
                    }
                });
            }
            return tcs.Task;
        }

        public static bool Succeeded(this UnityWebRequest request) =>
            request.result == UnityWebRequest.Result.Success;
    }
}
