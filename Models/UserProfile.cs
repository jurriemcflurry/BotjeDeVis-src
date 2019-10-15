using Microsoft.Azure.Amqp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CoreBot.Models
{
    public class UserProfile
    {
        private static UserProfile instance = null;
        private string name;
        
        private UserProfile(string name)
        {
            this.name = name;
        }       

        public static UserProfile Instance(string name)
        {
            
                if (instance == null)
                {
                    instance = new UserProfile(name);
                }

                return instance;
            
        }

        public string GetUserName()
        {
            return name;
        }
    }
}
