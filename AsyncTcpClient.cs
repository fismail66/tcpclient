using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

public class AsyncTcpClient : IDisposable
{
    private bool disposed = false;
    private TcpClient tcpClient;
    private Stream stream;

    private int minBufferSize = 8192;
    private int maxBufferSize = 15 * 1024 * 1024;
    private int bufferSize = 8192;

    private int BufferSize
    {
        get
        {
            return this.bufferSize;
        }
        set
        {
            this.bufferSize = value;
            if (this.tcpClient != null)
                this.tcpClient.ReceiveBufferSize = value;
        }
    }

    public int MinBufferSize
    {
        get
        {
            return this.minBufferSize;
        }
        set
        {
            this.minBufferSize = value;
        }
    }

    public int MaxBufferSize
    {
        get
        {
            return this.maxBufferSize;
        }
        set
        {
            this.maxBufferSize = value;
        }
    }

    public int SendBufferSize
    {
        get
        {
            if (this.tcpClient != null)
                return this.tcpClient.SendBufferSize;
            else
                return 0;
        }
        set
        {
            if (this.tcpClient != null)
                this.tcpClient.SendBufferSize = value;
        }
    }

   // public event EventHandler<byte[]> OnDataReceived;
   // public event EventHandler OnDisconnected;
   public Action<byte[]> OnDataReceived;
   public Action OnDisconnected;
    public Action<Exception> OnException;
    public bool IsConnected
    {
        get
        {
            return this.tcpClient != null && this.tcpClient.Connected;
        }
    }

    public bool IsRecieving {get; set;} = false;

    public AsyncTcpClient()
    {

    }

    private async Task Close()
    {
        await Task.Yield();
        if (this.tcpClient != null)
        {

            this.tcpClient.Close();
            this.tcpClient.Dispose();
            this.tcpClient = null;
        }
        if (this.stream != null)
        {
            this.stream.Dispose();
            this.stream = null;
        }
    }
    private async Task CloseIfCanceled(CancellationToken token, Action onClosed = null)
    {
        if (token.IsCancellationRequested)
        {
            await this.Close();
            if (onClosed != null)
                onClosed();
            token.ThrowIfCancellationRequested();
        }
    }
    public async Task ConnectAsync(RemoteServerInfo remoteServerInfo, CancellationToken cancellationToken = default(CancellationToken))
    {
        try
        {
            //Connect async method
            await this.Close();
            cancellationToken.ThrowIfCancellationRequested();
            this.tcpClient = new TcpClient();
            cancellationToken.ThrowIfCancellationRequested();
            await this.tcpClient.ConnectAsync(remoteServerInfo.Host, remoteServerInfo.Port);
            await this.CloseIfCanceled(cancellationToken);
            this.stream = this.tcpClient.GetStream();
            await this.CloseIfCanceled(cancellationToken);
        }
        catch (Exception ex)
        {
            this.CloseIfCanceled(cancellationToken).Wait();
            OnException?.Invoke(ex);
            //throw;
        }
    }

    public async Task SendAsync(byte[] data, CancellationToken token = default(CancellationToken))
    {
        try
        {
            await this.stream.WriteAsync(data, 0, data.Length, token);
            await this.stream.FlushAsync(token);
        }
        catch (IOException ex)
        {
            var onDisconected = this.OnDisconnected;
            if (ex.InnerException != null && ex.InnerException is ObjectDisposedException) {;}// for SSL streams
            //else if (onDisconected != null)
            //    onDisconected(this, EventArgs.Empty);
            else
               onDisconected?.Invoke(); 
        }
    }
    public async Task Recieve(CancellationToken token = default(CancellationToken))
    {
        try
        {
            if (!this.IsConnected || this.IsRecieving)
                throw new InvalidOperationException();
            this.IsRecieving = true;
            byte[] buffer = new byte[bufferSize];
            while (this.IsConnected)
            {
                token.ThrowIfCancellationRequested();
                int bytesRead = await this.stream.ReadAsync(buffer, 0, buffer.Length, token);
                if (bytesRead > 0)
                {
                    if (bytesRead == buffer.Length)
                        this.BufferSize = Math.Min(this.BufferSize * 10, this.maxBufferSize);
                    else
                    {
                        do
                        {
                            int reducedBufferSize = Math.Max(this.BufferSize / 10, this.minBufferSize);
                            if (bytesRead < reducedBufferSize)
                                this.BufferSize = reducedBufferSize;

                        }
                        while (bytesRead > this.minBufferSize);
                    }
                    var onDataRecieved = this.OnDataReceived;
                    byte[] data = new byte[bytesRead];
                    Array.Copy(buffer, data, bytesRead);
                    onDataRecieved.Invoke(data);
                    // var onDataRecieved = this.OnDataReceived;
                    // if (onDataRecieved != null)
                    // {
                    //     byte[] data = new byte[bytesRead];
                    //     Array.Copy(buffer, data, bytesRead);
                    //     onDataRecieved(this, data);
                    // }
                }
                buffer = new byte[bufferSize];
            }
        }
        catch (ObjectDisposedException) { }
        catch (IOException ex)
        {
            var discon = this.OnDisconnected;
            if (ex.InnerException is ObjectDisposedException) {;}
            discon?.Invoke();
            // var evt = this.OnDisconnected;
            // if (ex.InnerException is ObjectDisposedException) {;}
            // if (evt != null)
            //     evt(this, EventArgs.Empty);
        }
        finally
        {
            this.IsRecieving = false;
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (disposing)
            {
                // Dispose managed resources.

                if (this.tcpClient != null)
                {
                    this.tcpClient.Close();
                    this.tcpClient = null;
                }
            }

            // There are no unmanaged resources to release, but
            // if we add them, they need to be released here.
        }

        disposed = true;

        // If it is available, make the call to the
        // base class's Dispose(Boolean) method
        // base.Dispose(disposing);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}

public class RemoteServerInfo{
    public IPAddress Host {get; set;}
    public int Port {get; set; }
}
