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
    class FtpServer
    {
        private Thread thread = null;
        private TcpListener socketListen = null;
        private int port;

        public FtpServer()
        {

        }

        public void Start(int _port)
        {
            port = _port;
            //   thread = new Thread(ThreadRun);
            // thread.Start();
            ThreadRun();
        }

        public void Stop()
        {
            socketListen.Stop();
            thread.Join();
        }

        private void ThreadRun()
        {
            socketListen = new TcpListener(IPAddress.Any, port);
            
            if(socketListen != null)
            {
                socketListen.Start();

                bool run = true;

                while(run)
                {
                    TcpClient socket = null;
                    try
                    {
                        socket = socketListen.AcceptTcpClient();
                    }
                    catch(SocketException)
                    {
                        run = false;
                    }
                    finally
                    {
                        if(socket == null)
                        {
                            run = false;
                        }
                        else
                        {

                            ClientConnection clientConnection = new ClientConnection(socket);

                            //clientConnection.HandleClient();

                            clientConnection.Start();
                          
                        }
                    }
                }
            }
        }
    }
}
