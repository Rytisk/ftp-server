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
    class ClientConnection
    {
        private TcpClient _client;
        private TcpClient _dataClient;
        private TcpListener _passiveListener;

        private NetworkStream _networkStream;
        private StreamReader _reader;
        private StreamWriter _writer;

        private StreamReader _dataReader;
        private StreamWriter _dataWriter;
       

        private IPEndPoint _dataEndpoint;

        private string _username;
        private string _password;
        private string _transferType;
        private string _currentDirectory;
        private string _root;

        private Thread th;

        public ClientConnection(TcpClient client)
        {
            _client = client;

            _networkStream = _client.GetStream();
            _reader = new StreamReader(_networkStream);
            _writer = new StreamWriter(_networkStream);
        }

        public void Start()
        {
            th = new Thread(HandleClient);

            th.Start();
        }

        public void HandleClient()
        {
            _writer.WriteLine("220 Ready!");
            _writer.Flush();

            string line = null;

            Console.WriteLine("starting the server");

            while (!string.IsNullOrEmpty(line = _reader.ReadLine()))
            {
                Console.WriteLine(line);
                string response = null;

                string[] command = line.Split(' ');

                string cmd = command[0].ToUpper();

                string argument = command.Length > 1 ? line.Substring(command[0].Length + 1) : null;

                if (string.IsNullOrWhiteSpace(argument))
                    argument = null;

                switch(cmd)
                {
                    case "USER":
                        response = CheckUsername(argument);
                        break;
                    case "PASS":
                        response = CheckPassword(argument);
                        break;
                    case "ACCT":
                        break;
                    case "CWD":
                        response = ChangeWorkingDirectory(argument);
                        break;
                    case "CDUP":
                        response = ChangeWorkingDirectory("..");
                        break;
                    case "SMNT":
                        response = "502 Command not implemented";
                        break;
                    case "REIN":
                        response = "502 Command not implemented";
                        break;
                    case "QUIT":
                        response = "221 Service closing control connection";
                        break;
                    case "PWD":
                        response = PrintWorkingDirectory();
                        break;
                    case "TYPE":
                        string[] args = argument.Split(' ');
                        response = Type(args[0], args.Length > 1 ? args[1] : null);
                        break;
                    case "PORT":
                        response = Port(argument);
                        break;
                    case "PASV":
                        response = Passive();
                        break;
                    case "LIST":
                        response = List(argument);
                        break;
                    case "RETR":
                        response = Retrieve(argument);
                        break;
                    case "STOR":
                        response = Store(argument);
                        break;
                    default:
                        response = "502 Command not implemented";
                        break;
                }

                if(_client == null || !_client.Connected)
                {
                    break;
                }
                else
                {
                    _writer.WriteLine(response);
                    _writer.Flush();

                    if (response.StartsWith("221"))
                    {
                        break;
                    }
                }
            }
        }

        private string NormalizeFilename(string path)
        {
            if (path == null)
            {
                path = string.Empty;
            }

            if (path == "/")
            {
                return _root;
            }
            else if (path.StartsWith("/"))
            {
                path = new FileInfo(Path.Combine(_root, path.Substring(1))).FullName;
            }
            else
            {
                path = new FileInfo(Path.Combine(_currentDirectory, path)).FullName;
            }

            return IsPathValid(path) ? path : null;
        }

        private bool IsPathValid(string path)
        {
            return path.StartsWith(_root);
        }

        private string List(string pathname)
        {
            pathname = NormalizeFilename(pathname);

            if(pathname != null)
            {
                _dataClient = _passiveListener.AcceptTcpClient();
                HandleList(pathname);

                return string.Format("150 Opening mode data transfer for LIST");
            }

            return "450 Requested file action not taken";
        }

        private void HandleList(string pathname)
        {


            using (NetworkStream stream = _dataClient.GetStream())
            {
                _dataReader = new StreamReader(stream, Encoding.ASCII);
                _dataWriter = new StreamWriter(stream, Encoding.ASCII);

                IEnumerable<string> directories = Directory.EnumerateDirectories(pathname);

                foreach (string dir in directories)
                {
                    DirectoryInfo d = new DirectoryInfo(dir);

                    string date = d.LastWriteTime < DateTime.Now - TimeSpan.FromDays(180) ?
                    d.LastWriteTime.ToString("MMM dd yyyy") :
                    d.LastWriteTime.ToString("MMM dd HH:mm");

                    //string line = string.Format("drwxr-xr-x 2 2003 2003 {0,8} {1} {2}", "4096", date, d.Name);
                    string line = string.Format("drwxr-xr-x 2 2003 2003 {0,8} {1}", "4096",  d.Name);
                    _dataWriter.WriteLine(line);
                    _dataWriter.Flush();
                }


                IEnumerable<string> files = Directory.EnumerateFiles(pathname);

                foreach (string file in files)
                {
                    FileInfo f = new FileInfo(file);

                    string date = f.LastWriteTime < DateTime.Now - TimeSpan.FromDays(180) ?
                        f.LastWriteTime.ToString("MMM dd  yyyy") :
                        f.LastWriteTime.ToString("MMM dd HH:mm");

                    //string line = string.Format("-rw-r--r--    2 2003     2003     {0,8} {1} {2}", f.Length, date, f.Name);
                    string line = string.Format("-rw-r--r--    2 2003     2003     {0,8} {1}", f.Length, f.Name);

                    _dataWriter.WriteLine(line);
                    _dataWriter.Flush();
                }
            }

            _dataClient.Close();
            _dataClient = null;

            _writer.WriteLine("226 Transfer complete");
            _writer.Flush();
        }

        private string Retrieve(string pathname)
        {
            pathname = NormalizeFilename(pathname);

            if(IsPathValid(pathname))
            {
                if(File.Exists(pathname))
                {
                    _dataClient = _passiveListener.AcceptTcpClient();

                    using (NetworkStream dataStream = _dataClient.GetStream())
                    {
                        using (FileStream fs = new FileStream(pathname, FileMode.Open, FileAccess.Read))
                        {
                            CopyStream(fs, dataStream);
                        }
                    }
                    _dataClient.Close();
                    _dataClient = null;

                    _writer.WriteLine("226 Closing data connection, file transfer succesful");
                    _writer.Flush();
                    
                }
            }

            return "550 File Not Found";
        }

        private string Store(string pathname)
        {
            pathname = NormalizeFilename(pathname);

            if (pathname != null)
            {
                _dataClient = _passiveListener.AcceptTcpClient();

                using (NetworkStream dataStream = _dataClient.GetStream())
                {
                    using (FileStream fs = new FileStream(pathname, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None, 4096, FileOptions.SequentialScan))
                    {
                        CopyStream(dataStream, fs);
                    }
                }
                _dataClient.Close();
                _dataClient = null;

                _writer.WriteLine("226 Closing data connection, file transfer succesful");
                _writer.Flush();
            }

            return "450 Requested file action not taken";
        }

        private long CopyStream(Stream input, Stream output)
        {
            if (_transferType == "I")
            {
                return CopyStream(input, output, 4096);
            }
            else
            {
                return CopyStreamAscii(input, output, 4096);
            }
        }

        private static long CopyStream(Stream input, Stream output, int bufferSize)
        {
            byte[] buffer = new byte[bufferSize];
            int count = 0;
            long total = 0;

            while ((count = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                output.Write(buffer, 0, count);
                total += count;
            }

            return total;
        }

        private static long CopyStreamAscii(Stream input, Stream output, int bufferSize)
        {
            char[] buffer = new char[bufferSize];
            int count = 0;
            long total = 0;

            using (StreamReader rdr = new StreamReader(input))
            {
                using (StreamWriter wtr = new StreamWriter(output, Encoding.ASCII))
                {
                    while ((count = rdr.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        wtr.Write(buffer, 0, count);
                        total += count;
                    }
                }
            }

            return total;
        }

        private string Type(string typeCode, string formatControl)
        {
            string response = "500 error";

            switch (typeCode)
            {
                case "I":
                    _transferType = typeCode;
                    response = "200 OK";
                    break;
                case "A":
                    response = "200 OK";
                    break;
                case "E":
                    break;
                case "L":
                    break;
                default:
                    response = "504 Command not implemented for that parameter.";
                    break;
            }

            if (formatControl != null)
            {
                switch (formatControl)
                {
                    case "N":
                        response = "200 Ok";
                        break;
                    case "T":
                        break;
                    case "C":
                        break;
                    default:
                        response = "504 Command not implemented for that parameter.";
                        break;
                }
            }

            return response;
        }
        
        private string Port(string hostPort)
        {
            string[] ipAndPort = hostPort.Split(',');

            byte[] ipAddress = new byte[4];
            byte[] port = new byte[2];

            for (int i = 0; i < 4; i++)
            {
                ipAddress[i] = Convert.ToByte(ipAndPort[i]);
            }

            for (int i = 4; i < 6; i++)
            {
                port[i - 4] = Convert.ToByte(ipAndPort[i]);
            }

            if (BitConverter.IsLittleEndian)
                Array.Reverse(port);

            BitConverter.ToInt16(port, 0);

            _dataEndpoint = new IPEndPoint(new IPAddress(ipAddress), BitConverter.ToInt16(port, 0));

            return "200 Data Connection Established";
        }

        private string PrintWorkingDirectory()
        {
            string current = _currentDirectory.Replace(_root, string.Empty).Replace('\\', '/');

            if (current.Length == 0)
            {
                current = "/";
            }

            return string.Format("257 \"{0}\" is current directory.", current); ;
        }

        private string Passive()
        {
            IPAddress localAddress = ((IPEndPoint)_client.Client.LocalEndPoint).Address;

            _passiveListener = new TcpListener(localAddress, 0);
            _passiveListener.Start();

            IPEndPoint passiveListenerEndpoint = (IPEndPoint)_passiveListener.LocalEndpoint;

            byte[] address = passiveListenerEndpoint.Address.GetAddressBytes();
            short port = (short)passiveListenerEndpoint.Port;

            byte[] portArray = BitConverter.GetBytes(port);

            if (BitConverter.IsLittleEndian)
                Array.Reverse(portArray);

            return string.Format("227 Entering Passive Mode ({0},{1},{2},{3},{4},{5})", address[0], address[1], address[2], address[3], portArray[0], portArray[1]);
        }

        private string CheckUsername(string username)
        {
            _username = username;

            return "331 Username ok, need password";
        }

        private string CheckPassword(string password)
        {
            _password = password;

            if (true)
            {
                _root = "C:\\";
                _currentDirectory = _root;
                return "230 User logged in";
            }
            else
            {
                return "530 Not logged in";
            }
        }

        private string ChangeWorkingDirectory(string pathname)
        {
            if (pathname == "/")
            {
                _currentDirectory = _root;
            }
            else
            {
                string newDir;

                if (pathname.StartsWith("/"))
                {
                    pathname = pathname.Substring(1).Replace('/', '\\');
                    newDir = Path.Combine(_root, pathname);
                }
                else
                {
                    pathname = pathname.Replace('/', '\\');
                    newDir = Path.Combine(_currentDirectory, pathname);
                }

                if (Directory.Exists(newDir))
                {
                    _currentDirectory = new DirectoryInfo(newDir).FullName;

                    if (!IsPathValid(_currentDirectory))
                    {
                        _currentDirectory = _root;
                    }
                }
                else
                {
                    _currentDirectory = _root;
                }
            }
            
            return "250 Changed to new directory";
        }
    }
}
