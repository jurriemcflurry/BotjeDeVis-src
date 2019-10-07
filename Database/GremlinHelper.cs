using Gremlin.Net.Driver;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CoreBot.Database
{
        public class GremlinHelper
    {
        private string graphkey;
        private string graphendpoint;

        public GremlinHelper(string graphkey, string graphendpoint)
        {
            this.graphendpoint = graphendpoint;
            this.graphkey = graphkey;
        }

       /* public async Task<> ConnectToDatabase()
        {
            string hostname = graphendpoint;
            int port = 443;
            string authKey = graphkey;
            string database = "botjedevis";
            string collection = "webshop";
            var gremlinServer = new GremlinServer(hostname, port, enableSsl: true,
                                                            username: "/dbs/" + database + "/colls/" + collection,
                                                            password: authKey);

            return gremlinServer;
        }*/
    }
}