using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ftp_server
{
    static class Login
    {
        private static string _filePath = "D:\\Projects\\ftp-server\\ftp-server\\users\\users.xml";

        public static bool IsValidLogin(User user)
        {
            XDocument xdoc = XDocument.Load(_filePath);

            return xdoc.Descendants("user")
              .Where(id => id.Attribute("username").Value == user.Username
                     && id.Attribute("password").Value == user.Password).Any();
        }

        public static bool UsernameExists(string username)
        {
            XDocument xdoc = XDocument.Load(_filePath);

            return xdoc.Descendants("user")
              .Where(id => id.Attribute("username").Value == username).Any();
        }
    }
}
