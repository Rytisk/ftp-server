using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ftp_server
{
    class User
    {
        private string _username;
        private string _password;
        private string _root;

        private bool _loggedIn;

        public string Username
        {
            get
            {
                return _username;
            }
            set
            {
                _username = value;
            }
        }

        public string Password
        {
            get
            {
                return _password;
            }
            set
            {
                _password = value;
            }
        }

        public string Root
        {
            get
            {
                return _root;
            }

            set
            {
                _root = value;
            }
        }

        public bool LoggedIn
        {
            get
            {
                return _loggedIn;
            }
            set
            {
                _loggedIn = value;
            }
        }

        public User()
        {
            _loggedIn = false;
        }

        public User(string username, string password, string root) : this()
        {
            _username = username;
            _password = password;
            _root = root;
        }
    }
}
