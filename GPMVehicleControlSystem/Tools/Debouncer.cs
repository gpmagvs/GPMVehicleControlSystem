namespace GPMVehicleControlSystem.Tools
{
    public class Debouncer
    {
        private CancellationTokenSource cts;
        public event EventHandler<string> OnActionCanceled;
        /// <summary>
        /// 防抖動
        /// </summary>
        /// <param name="action">動作</param>
        /// <param name="delay">延遲時間(毫秒)</param>
        public void Debounce(Action action, int delay = 300, string actionName = "")
        {
            if (cts != null)
            {
                cts.Cancel();
                cts.Dispose();
                OnActionCanceled?.Invoke(this, actionName);
            }
            cts = new CancellationTokenSource();
            Task.Delay(delay, cts.Token).ContinueWith((t) =>
            {
                if (!t.IsCanceled)
                {
                    action();
                }
            });
        }
    }
}
