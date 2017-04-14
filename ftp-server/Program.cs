using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ftp_server
{
    class Program
    {
        static void Main(string[] args)
        {
            FtpServer ftpServer = new FtpServer();

            ftpServer.Start(21);

        }
    }
}
