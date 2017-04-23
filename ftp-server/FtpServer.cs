using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ftp_server
{
    class FtpServer : IDisposable
    {
        private Thread thread = null;
        private TcpListener _listener = null;
        private int _port;
        private bool _disposed;
        private bool _listening;
        private List<ClientConnection> _activeConnections;

        public FtpServer(int port)
        {
            _port = port;
        }


        public void Start()
        {
            _listener = new TcpListener(IPAddress.Any, _port);


            _listening = true;
            _listener.Start();

            _activeConnections = new List<ClientConnection>();

            _listener.BeginAcceptTcpClient(HandleAcceptTcpClient, _listener);
        }

        public void Stop()
        {

            _listening = false;
            _listener.Stop();

            _listener = null;
        }

        private void HandleAcceptTcpClient(IAsyncResult result)
        {
            if (_listening)
            {
                _listener.BeginAcceptTcpClient(HandleAcceptTcpClient, _listener);

                TcpClient client = _listener.EndAcceptTcpClient(result);

                ClientConnection clientConnection = new ClientConnection(client);

                _activeConnections.Add(clientConnection);

                clientConnection.HandleClient();
            }
        }
        

        public void Dispose()
        {
            if (!_disposed)
            {
                Stop();

                foreach (ClientConnection conn in _activeConnections)
                {
                    conn.Dispose();
                }
            }

            _disposed = true;
        }
    }
}
