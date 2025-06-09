using NLog;

namespace GPMVehicleControlSystem.Tools
{
    public class StateDebouncer<T>
    {
        private readonly TimeSpan _debounceDuration;
        private readonly TimeSpan _timeoutDuration;
        private readonly TimeSpan _pollingInterval;

        private DateTime? _expectedValueStartTime = null;
        private T? _lastObservedValue;

        public StateDebouncer(TimeSpan debounceDuration, TimeSpan timeoutDuration, TimeSpan? pollingInterval = null)
        {
            _debounceDuration = debounceDuration;
            _timeoutDuration = timeoutDuration;
            _pollingInterval = pollingInterval ?? TimeSpan.FromMilliseconds(200); // 預設每 200ms 輪詢一次
        }

        /// <summary>
        /// 自動開始檢查狀態直到 debounce 成功或 timeout。
        /// </summary>
        public async Task<(bool IsSuccess, bool IsTimeout)> StartAsync(Func<T> readCurrentValue, T expectedValue, CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.Now;
            _lastObservedValue = default!; // 強制讓它不等於 expectedValue

            while (!cancellationToken.IsCancellationRequested)
            {
                var current = readCurrentValue();

                Console.WriteLine($"Current State=>{current}");

                if (EqualityComparer<T>.Default.Equals(current, expectedValue))
                {
                    if (!EqualityComparer<T>.Default.Equals(_lastObservedValue, expectedValue))
                    {
                        _expectedValueStartTime = DateTime.Now;
                        _lastObservedValue = expectedValue;
                    }

                    if (_expectedValueStartTime.HasValue && DateTime.Now - _expectedValueStartTime >= _debounceDuration)
                    {
                        Reset();
                        return (true, false); // 成功
                    }
                }
                else
                {
                    _expectedValueStartTime = null;
                    _lastObservedValue = current;
                }

                if (DateTime.Now - startTime >= _timeoutDuration)
                {
                    Reset();
                    return (false, true); // 超時
                }

                await Task.Delay(_pollingInterval, cancellationToken);
            }

            Reset();
            return (false, true); // 被取消視同 timeout
        }

        private void Reset()
        {
            _expectedValueStartTime = null;
            _lastObservedValue = default!;
        }
    }
}
