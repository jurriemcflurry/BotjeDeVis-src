using CoreBot.Models;
using Gremlin.Net.Driver;
using Gremlin.Net.Structure.IO.GraphSON;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
        private GremlinClient g;

        public GremlinHelper(IConfiguration configuration)
        {
            this.graphendpoint = configuration["cosgraphendpoint"];
            this.graphkey = configuration["cosgraphkey"];
        }

        public GremlinClient ConnectToDatabase()
        {
            string hostname = graphendpoint;
            int port = 443;
            string authKey = graphkey;
            string database = "botjedevis";
            string collection = "webshop";
            var gremlinServer = new GremlinServer(hostname, port, enableSsl: true,
                                                            username: "/dbs/" + database + "/colls/" + collection,
                                                            password: authKey);
            var gremlinClient = new GremlinClient(gremlinServer, new GraphSON2Reader(), new GraphSON2Writer(), GremlinClient.GraphSON2MimeType);

            return gremlinClient;
        }

        public void StoreOrder(Order order)
        {
            g = ConnectToDatabase();
            
            //vind de producten adhv Label = product & name = productName
            //indien niet gevonden, maak product
            //creeer order met ordernummer
            //creeer edges tussen net gemaakt order, en de producten in de database adhv label = product & name = productName
            // maak databasecall om order op te slaan
        }

        public async Task<bool> ProductExists(Product product)
        {
            string query = "g.V().hasLabel('product').has('name','" + product.GetProductName() + "')";
            var result = await g.SubmitAsync<dynamic>(query);
            string output = JsonConvert.SerializeObject(result);

            if(output != null)
            {
                return true;
            }

            return false;
        }
    }
}

//onderstaand is een test om de Gremlinconnectie uit te voeren; werkende code           
/* string query = "g.V().hasLabel('person')";
 var results = await g.SubmitAsync<dynamic>(query);
 foreach (var result in results)
 {
     string output = JsonConvert.SerializeObject(result);
     var jsonObj = JObject.Parse(output);
     string userId = (string)jsonObj["id"];



     var properties = jsonObj["properties"];
     var name = properties["name"];
     var personNameArray = name[0];
     var personName = personNameArray["value"];

     await stepContext.Context.SendActivityAsync(personName.ToString());
 }*/
