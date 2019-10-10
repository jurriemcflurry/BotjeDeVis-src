﻿using CoreBot.Models;
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

        private GremlinClient ConnectToDatabase()
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

        public async Task<bool> StoreOrder(Order order)
        {
            g = ConnectToDatabase();

            //creeer order vertice met ordernummer
            string query = "g.addV('order').property('data','order').property('number','" + order.GetOrderNumber().ToString() + "')";
            var result = await g.SubmitAsync<dynamic>(query);

            if (g.SubmitAsync<dynamic>(query).IsFaulted)
            {
                return false;
            }

            //creeer edges tussen net gemaakt order, en de producten in de database adhv label = product & name = productName
            foreach (Product p in order.GetProducts())
            {
                string query2 = "g.V().hasLabel('order').has('number','" + order.GetOrderNumber().ToString() + "').as('a').V().hasLabel('product').has('name','" + p.GetProductName() + "').as('b').addE('contains_product').from('a').to('b')";
                var result2 = await g.SubmitAsync<dynamic>(query2);

                if (g.SubmitAsync<dynamic>(query2).IsFaulted)
                {
                    return false;
                }
            }

            return true;
        }

        public async Task<bool> ProductExists(Product product)
        {
            g = ConnectToDatabase();
            string query = "g.V().hasLabel('product').has('name','" + product.GetProductName() + "')";
            var result = await g.SubmitAsync<dynamic>(query);
            string output = JsonConvert.SerializeObject(result);

            //als product niet bestaat, bestaat de vertice niet, en wordt er een lege array gereturned. Als de array dus niet leeg is, is het product gevonden.
            if(output != "[]")
            {
                return true;
            }

            return false;
        }

        public async Task<bool> OrderExistsByNumber(int orderNumber)
        {
            g = ConnectToDatabase();
            string orderExistsQuery = "g.V('order').has('number','" + orderNumber + "')";
            var result = await g.SubmitAsync<dynamic>(orderExistsQuery);
            var output = JsonConvert.SerializeObject(result);

            if(output == "[]")
            {
                return false;
            }


            return true;
        }

        public async Task<string> AnswerQuestion(string onderwerp)
        {
            g = ConnectToDatabase();
            string query = "g.V().hasLabel('vraag').has('onderwerp','" + onderwerp + "').outE().inV()";
            var result = await g.SubmitAsync<dynamic>(query);
            var output = JsonConvert.SerializeObject(result);
            var jsonArray = JArray.Parse(output);
            var jsonObj = (JObject)jsonArray[0];
            var properties = (JObject)jsonObj["properties"];
            var antwoordArray = (JArray)properties["antwoord"];
            var antwoordArray2 = (JObject)antwoordArray[0];
            string antwoord = antwoordArray2["value"].ToString();

            return antwoord;
        }
    }
}
