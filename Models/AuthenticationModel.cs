using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CoreBot.Models
{
    public class AuthenticationModel
    {
        private bool authenticated { get; set; }
        private string username { get; set; }
        private static AuthenticationModel instance = null;

        private AuthenticationModel()
        {

        }

        public static AuthenticationModel Instance()
        {         
           if (instance == null)
           {
                instance = new AuthenticationModel();
           }
           return instance;           
        }

        public void SetAuthenticated(string username)
        {
            this.authenticated = true;
            this.username = username;
        }

        public void Logout()
        {
            this.authenticated = false;
            username = "";
        }

        public bool GetAuthenticationState()
        {
            return authenticated;
        }

        public string GetLoggedInUser()
        {
            return username;
        }
    }
}
