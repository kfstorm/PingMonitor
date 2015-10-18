using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace Kfstorm.PingMonitor
{
    public class PingClient : IDisposable
    {
        public List<PingResult> PingResults = new List<PingResult>();

        public PingOptions Options;

        public TimeSpan Interval { get; set; }

        public TimeSpan Timeout { get; set; }

        public string Host { get; set; }

        private CancellationTokenSource _cancellationTokenSource;

        public ClientState State { get; private set; }

        private Task _backgroundTask;

        private readonly Mutex _mutex = new Mutex();

        public event EventHandler<PingTickEventArgs> PingTick;

        public event EventHandler<PingExceptionEventArgs> PingException;

        protected virtual void OnPingException(PingExceptionEventArgs e)
        {
            EventHandler<PingExceptionEventArgs> handler = PingException;
            if (handler != null) handler(this, e);
        }

        protected virtual void OnPintTick(PingTickEventArgs e)
        {
            EventHandler<PingTickEventArgs> handler = PingTick;
            if (handler != null) handler(this, e);
        }

        public enum ClientState
        {
            Stoped,
            Running,
            Closed,
        }

        public PingClient(string host, TimeSpan interval, TimeSpan timeout)
        {
            Host = host;
            Interval = interval;
            Timeout = timeout;
        }

        public void Start()
        {
            _mutex.WaitOne();
            try
            {
                switch (State)
                {
                    case ClientState.Stoped:
                        _cancellationTokenSource = new CancellationTokenSource();
                        _backgroundTask = Task.Factory.StartNew(() => TaskCallback(_cancellationTokenSource.Token));
                        State = ClientState.Running;
                        break;
                    case ClientState.Running:
                        throw new InvalidOperationException("Pinging.");
                    case ClientState.Closed:
                        throw new ObjectDisposedException("Closed.", (Exception)null);
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            finally
            {
                _mutex.ReleaseMutex();
            }
        }

        private void TaskCallback(CancellationToken token)
        {
            using (var ping = new Ping())
            {
                while (!token.IsCancellationRequested)
                {
                    var dateTime = DateTime.Now;
                    PingReply reply = null;
                    try
                    {
                        reply = ping.Send(Host, (int) Timeout.TotalMilliseconds);
                    }
                    catch (Exception ex)
                    {
                        if (!token.IsCancellationRequested)
                        {
                            var eventArgs = new PingExceptionEventArgs {Exception = ex};
                            OnPingException(eventArgs);
                            if (eventArgs.Cancel)
                            {
                                return;
                            }
                        }
                        else
                        {
                            return;
                        }
                    }
                    if (reply != null)
                    {
                        var result = new PingResult
                        {
                            DateTime = dateTime,
                            Status = reply.Status,
                            TimeCost =
                                reply.Status == IPStatus.TimedOut
                                    ? Timeout
                                    : TimeSpan.FromMilliseconds(reply.RoundtripTime)
                        };
                        PingResults.Add(result);
                        OnPintTick(new PingTickEventArgs {Result = result});
                    }
                    while (!token.IsCancellationRequested)
                    {
                        var sleep = Interval - (DateTime.Now - dateTime);
                        var max = TimeSpan.FromMilliseconds(10);
                        if (sleep > max)
                        {
                            sleep = max;
                        }
                        if (sleep > TimeSpan.Zero)
                        {
                            Thread.Sleep(sleep);
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }
        }

        public void Stop()
        {
            _mutex.WaitOne();
            try
            {
                switch (State)
                {
                    case ClientState.Stoped:
                        break;
                    case ClientState.Running:
                        _cancellationTokenSource.Cancel();
                        try
                        {
                            _backgroundTask.Wait();
                        }
                        finally
                        {
                            State = ClientState.Stoped;
                            _cancellationTokenSource.Dispose();
                            _backgroundTask.Dispose();
                        }
                        break;
                    case ClientState.Closed:
                        throw new ObjectDisposedException("Closed.", (Exception) null);
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            finally
            {
                _mutex.ReleaseMutex();
            }
        }

        public void Dispose()
        {
            Stop();
            State = ClientState.Closed;
        }
    }

    public class PingTickEventArgs : EventArgs
    {
        public PingResult Result { get; set; }
    }

    public class PingExceptionEventArgs : EventArgs
    {
        public Exception Exception { get; set; }
        public bool Cancel { get; set; }
    }
}
