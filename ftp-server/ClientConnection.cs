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
    class ClientConnection : IDisposable
    {
        #region Fields

        private TcpClient _client;
        private TcpClient _dataClient;
        private TcpListener _passiveListener;

        private NetworkStream _networkStream;
        private StreamReader _reader;
        private StreamWriter _writer;     
        private StreamWriter _dataWriter;

        private IPEndPoint _dataEndpoint;

        private User _user;
        private string _transferType;
        private string _currentDirectory;
        private bool _disposed;
        private bool _passiveConn;

        #endregion

        public ClientConnection(TcpClient client)
        {
            _user = new User();

            _client = client;

            _networkStream = _client.GetStream();
            _reader = new StreamReader(_networkStream);
            _writer = new StreamWriter(_networkStream);
        }

        private string Response(string cmd, string argument)
        {
            string response = "";
            switch (cmd)
            {
                case "USER":
                    response = "503 Bad sequence of commands";
                    break;
                case "PASS":
                    response = "503 Bad sequence of commands";
                    break;
                case "CWD":
                    response = ChangeWorkingDirectory(argument);
                    break;
                case "CDUP":
                    response = ChangeWorkingDirectory("..");
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
                case "DELE":
                    response = Delete(argument);
                    break;
                case "MKD":
                    response = MakeDirectory(argument);
                    break;
                case "RMD":
                    response = RemoveDirectoyy(argument);
                    break;
                case "STRU":
                    response = Structure(argument);
                    break;
                case "MODE":
                    response = Mode(argument);
                    break;
                default:
                    response = "502 Command not implemented";
                    break;
            }
            return response;
        }

        private string LoginResponse(string cmd, string argument)
        {
            string response = "";

            switch (cmd)
            {
                case "USER":
                    response = CheckUsername(argument);
                    break;
                case "PASS":
                    response = CheckPassword(argument);
                    break;
                default:
                    response = "530 Not logged in";
                    break;
            }
            return response;
        }

        public void HandleClient()
        {
            try
            {
                _writer.WriteLine("220 Ready");
                _writer.Flush();
            }
            catch(IOException ex)
            {
                Console.WriteLine(ex.Message);
                return;
            }

            string line = null;

            _dataClient = new TcpClient();
            
            while (!string.IsNullOrEmpty(line = _reader.ReadLine()))
            {
                Console.WriteLine(line);
                string response = null;

                string[] command = line.Split(' ');

                string cmd = command[0].ToUpper();

                string argument = command.Length > 1 ? line.Substring(command[0].Length + 1) : null;

                if (string.IsNullOrWhiteSpace(argument))
                    argument = null;

                if(_user.LoggedIn)
                {
                    response = Response(cmd, argument);
                }
                else
                {
                    response = LoginResponse(cmd, argument);
                }

                if (_client == null || !_client.Connected)
                {
                    break;
                }
                else
                {
                    try
                    {
                        _writer.WriteLine(response);
                        _writer.Flush();
                    }
                    catch(IOException)
                    {
                        Console.WriteLine("Connection lost.");
                        break;
                    }
                    
                    if (response.StartsWith("221"))
                    {
                        break;
                    }
                }
            }
            Dispose();
        }

        #region AccessControlCommands

        private string CheckUsername(string username)
        {
            if (username == null)
            {
                return "530 Not logged in. Missing <username>";
            }

            if (Login.UsernameExists(username))
            {
                _user.Username = username;
                return "331 Username ok, need password";
            }

            return "530 Not logged in";            
        }

        private string CheckPassword(string password)
        {
            _user.Password = password;

            if (password == null)
            {
                return "530 Not logged in. Missing <password>";
            }

            if (Login.IsValidLogin(_user))
            {
                _user.Root = "E:\\";
                _currentDirectory = _user.Root;
                _user.LoggedIn = true;
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
                _currentDirectory = _user.Root;
            }
            else
            {
                string newDir;
                try
                {
                    if (pathname.StartsWith("/"))
                    {
                        pathname = pathname.Substring(1).Replace('/', '\\');
                        newDir = Path.Combine(_user.Root, pathname);
                    }
                    else
                    {
                        pathname = pathname.Replace('/', '\\');
                        newDir = Path.Combine(_currentDirectory, pathname);
                    }
                }
                catch(ArgumentException)
                {
                    return "550 Directory not found";
                }
                

                if (Directory.Exists(newDir))
                {
                    _currentDirectory = new DirectoryInfo(newDir).FullName;

                    if (!IsPathValid(_currentDirectory))
                    {
                        _currentDirectory = _user.Root;
                        return "550 Can't access directory";
                    }
                }
                else
                {
                    _currentDirectory = _user.Root;
                    return "550 Directory not found";
                }
            }

            return "250 Changed to new directory";
        }

        #endregion

        #region TransferParameterCommands

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
                    _transferType = typeCode;
                    response = "200 OK";
                    break;
                case "E":
                case "L":
                default:
                    response = "504 Command not implemented for that parameter.";
                    break;
            }

            if (formatControl != null)
            {
                switch (formatControl)
                {
                    case "N":
                        response = "200 OK";
                        break;
                    case "T":
                    case "C":
                    default:
                        response = "504 Command not implemented for that parameter.";
                        break;
                }
            }

            return response;
        }

        private string Port(string hostPort)
        {
            _passiveConn = false;

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

        private string Passive()
        {
            _passiveConn = true;

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

        private string Structure(string structure)
        {
            switch (structure)
            {
                case "F":
                    break;
                case "R":
                case "P":
                    return string.Format("504 STRU not implemented for \"{0}\"", structure);
                default:
                    return string.Format("501 Parameter {0} not recognized", structure);
            }

            return "200 Command OK";
        }

        private string Mode(string mode)
        {
            if (mode.ToUpperInvariant() == "S")
            {
                return "200 OK";
            }
            else
            {
                return "504 Command not implemented for that parameter";
            }
        }

        #endregion

        #region FTPServiceCommands

        private string List(string pathname)
        {
            pathname = NormalizeFilename(pathname);

            if(pathname != null)
            {   
                try
                { 
                    _writer.WriteLine("150 Opening Passive mode data transfer for LIST");
                    _writer.Flush();

                    return HandleList(pathname);
                }
                catch (IOException ex)
                {
                    Console.WriteLine(ex.Message);
                }

            }

            return "450 Requested file action not taken";
        }

        private string Delete(string pathname)
        {
            pathname = NormalizeFilename(pathname);

            if(pathname != null)
            {
                if(File.Exists(pathname))
                {
                    File.Delete(pathname);
                }
                else
                {
                    return "550 File not found";
                }

                return "250 Requested file action okay, completed";
            }

            return "550 File not found";
        }

        private string RemoveDirectoyy(string pathname)
        {
            pathname = NormalizeFilename(pathname);

            if(pathname != null)
            {
                if(Directory.Exists(pathname))
                {
                    Directory.Delete(pathname);
                }
                else
                {
                    return "550 Directory not found";
                }

                return "250 Requested directory action okay, comlpeted";
            }

            return "550 Directory not found";
        }

        private string MakeDirectory(string pathname)
        {
            pathname = NormalizeFilename(pathname);
            
            if(pathname != null)
            {
                if(!Directory.Exists(pathname))
                {
                    Directory.CreateDirectory(pathname);
                }
                else
                {
                    return "550 Directory already exists";
                }

                return "250 Requested directory action okay, completed";
            }

            return "550 Directory not created";
        }

        private string HandleList(string pathname)
        {
            if (_passiveConn)
            {
                _dataClient = _passiveListener.AcceptTcpClient();
            }
            else
            {
                _dataClient = new TcpClient(_dataEndpoint.AddressFamily);
                _dataClient.Connect(_dataEndpoint.Address, _dataEndpoint.Port);
            }

            using (NetworkStream stream = _dataClient.GetStream())
            {
                _dataWriter = new StreamWriter(stream, Encoding.ASCII);

                IEnumerable<string> directories = Directory.EnumerateDirectories(pathname);

                foreach (string dir in directories)
                {
                    DirectoryInfo d = new DirectoryInfo(dir);
                    
                    string line = string.Format("drwxr-xr-x 2 2003 2003 {0,8} {1}", "4096",  d.Name);

                    try
                    {
                        _dataWriter.WriteLine(line);
                        _dataWriter.Flush();
                    }
                    catch(IOException ex)
                    {
                        Console.WriteLine(ex.Message);
                        return "550 Requested action not taken";
                    }

                }


                IEnumerable<string> files = Directory.EnumerateFiles(pathname);

                foreach (string file in files)
                {
                    FileInfo f = new FileInfo(file);
                    
                    string line = string.Format("-rw-r--r--    2 2003     2003     {0,8} {1}", f.Length, f.Name);

                    try
                    {
                        _dataWriter.WriteLine(line);
                        _dataWriter.Flush();
                    }
                    catch (IOException ex)
                    {
                        Console.WriteLine(ex.Message);
                        return "550 Requested action not taken";
                    }
                }
                
            }
            
            _dataClient.Close();
            _dataClient = null;

            return "226 Transfer complete";
        }

        private void HandleRetrieve(IAsyncResult res)
        {
            string pathname = res.AsyncState as string;

            if (_passiveConn)
            {
                _dataClient = _passiveListener.EndAcceptTcpClient(res);
            }
            else
            {
                _dataClient.EndConnect(res);
            }

            using (NetworkStream dataStream = _dataClient.GetStream())
            {
                using (FileStream fs = new FileStream(pathname, FileMode.Open, FileAccess.Read))
                {
                    if(CopyStream(fs, dataStream) > 0)
                    {
                        try
                        {
                            _writer.WriteLine("226 Closing data connection, file transfer succesful");
                            _writer.Flush();
                        }
                        catch(IOException ex)
                        {
                            Console.WriteLine(ex.Message);
                        }
                    }
                }
            }
            _dataClient.Close();
            _dataClient = null;
        }

        private string Retrieve(string pathname)
        {
            pathname = NormalizeFilename(pathname);

            if(IsPathValid(pathname))
            {
                if(File.Exists(pathname))
                {
                    if (_passiveConn)
                    {
                        _passiveListener.BeginAcceptTcpClient(HandleRetrieve, pathname);
                    }
                    else
                    {
                        _dataClient = new TcpClient(_dataEndpoint.AddressFamily);
                        _dataClient.BeginConnect(_dataEndpoint.Address, _dataEndpoint.Port, HandleRetrieve, pathname);
                    }
                    return "150 Opening Passive mode data transfer for RETR";
                }
            }
            return "550 File Not Found";
        }

        private string Store(string pathname)
        {
            pathname = NormalizeFilename(pathname);

            if (pathname != null)
            {
                
                if (_passiveConn)
                {
                    _passiveListener.BeginAcceptTcpClient(HandleStore, pathname);
                }
                else
                {
                    _dataClient = new TcpClient(_dataEndpoint.AddressFamily);
                    _dataClient.BeginConnect(_dataEndpoint.Address, _dataEndpoint.Port, HandleStore, pathname);
                }

                return "150 Opening Passive mode data transfer for STOR";
            }

            return "450 Requested file action not taken";
        }

        private void HandleStore(IAsyncResult res)
        {
            string pathname = res.AsyncState as string;
            
            if (_passiveConn)
            {
                _dataClient = _passiveListener.EndAcceptTcpClient(res);
            }
            else
            {
                _dataClient.EndConnect(res);
            }

            using (NetworkStream dataStream = _dataClient.GetStream())
            {
                using (FileStream fs = new FileStream(pathname, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None, 4096, FileOptions.SequentialScan))
                {
                    if(CopyStream(dataStream, fs) > 0)
                    {
                        try
                        {
                            _writer.WriteLine("226 Closing data connection, file transfer succesful");
                            _writer.Flush();
                        }
                        catch(IOException ex)
                        {
                            Console.WriteLine(ex.Message);
                        }
                    }
                }
            }
            _dataClient.Close();
            _dataClient = null;
        }

        private string PrintWorkingDirectory()
        {
            string current = _currentDirectory.Replace(_user.Root, string.Empty).Replace('\\', '/');

            if (current.Length == 0)
            {
                current = "/";
            }

            return string.Format("257 \"{0}\" is current directory.", current); ;
        }

        #endregion

        #region SupportFunctions

        private long CopyStream(Stream input, Stream output)
        {
            if (_transferType == "I")
            {
                return CopyStreamImage(input, output, 4096);
            }
            else
            {
                return CopyStreamAscii(input, output, 4096);
            }
        }

        private long CopyStreamImage(Stream input, Stream output, int bufferSize)
        {
            byte[] buffer = new byte[bufferSize];
            int count = 0;
            long total = 0;

            while ((count = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                try
                {
                    output.Write(buffer, 0, count);
                    total += count;
                }
                catch(IOException ioe)
                {
                    Console.WriteLine(ioe.Message);
                    return -1; 
                }                
            }

            return total;
        }

        private long CopyStreamAscii(Stream input, Stream output, int bufferSize)
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
                        try
                        { 
                            wtr.Write(buffer, 0, count);
                            total += count;
                        }
                        catch (IOException ioe)
                        {
                            Console.WriteLine(ioe.Message);
                            return -1;
                        }
                    }
                }
            }

            return total;
        }

        private string NormalizeFilename(string path)
        {
            if (path == null)
            {
                path = string.Empty;
            }

            if (path == "/")
            {
                return _user.Root;
            }
            else if (path.StartsWith("/"))
            {
                path = new FileInfo(Path.Combine(_user.Root, path.Substring(1))).FullName;
            }
            else
            {
                try
                {
                    path = new FileInfo(Path.Combine(_currentDirectory, path)).FullName;
                }
                catch (ArgumentException)
                {
                    return null;
                }
            }

            return IsPathValid(path) ? path : null;
        }

        private bool IsPathValid(string path)
        {
            return path.StartsWith(_user.Root);
        }
        
        #endregion

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_client != null)
                {
                    _client.Close();
                }

                if (_dataClient != null)
                {
                    _dataClient.Close();
                }

                if (_networkStream != null)
                {
                    _networkStream.Close();
                }

                if (_reader != null)
                {
                    _reader.Close();
                }

                if (_writer != null)
                {
                    _writer.Close();
                }
            }

            _disposed = true;
        }
    }
}
