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

        public async Task<bool> StoreOrderAsync(Order order)
        {
            g = ConnectToDatabase();
            string orderNumber = order.GetOrderNumber().ToString();
            List<Product> productsToStore = order.GetProducts();
            bool success = false;

            //creeer order vertice met ordernummer
            string query = "g.addV('order').property('data','order').property('number','" + orderNumber + "').property('status','received')";
            await g.SubmitAsync<dynamic>(query);

            //creeer edges tussen net gemaakt order, en de producten in de database adhv label = product & name = productName
            foreach (Product p in productsToStore)
            {
                string query2 = "g.V().hasLabel('order').has('number','" + orderNumber + "').as('a').V().hasLabel('product').has('name','" + p.GetProductName() + "').as('b').addE('contains_product').from('a').to('b')";
                var result = await g.SubmitAsync<dynamic>(query2);
                var output = JsonConvert.SerializeObject(result);
                if (output == "[]")
                {
                    success = false;
                }
                else
                {
                    success = true;
                }
            }

            return success;
        }

        public async Task<bool> ProductExistsAsync(Product product)
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

        public async Task<bool> OrderExistsByNumberAsync(int orderNumber)
        {
            g = ConnectToDatabase();
            string orderExistsQuery = "g.V().hasLabel('order').has('number','" + orderNumber + "')";
            var result = await g.SubmitAsync<dynamic>(orderExistsQuery);
            var output = JsonConvert.SerializeObject(result);

            if(output == "[]")
            {
                return false;
            }


            return true;
        }

        public async Task<List<Product>> GetOrderProductsAsync(int orderNumber)
        {
            List<Product> productList = new List<Product>();
            g = ConnectToDatabase();
            string getOrderProductsQuery = "g.V().hasLabel('order').has('number','" + orderNumber + "').outE().hasLabel('contains_product').inV().dedup()";
            var result = await g.SubmitAsync<dynamic>(getOrderProductsQuery);
            var output = JsonConvert.SerializeObject(result);
            var jsonArray = JArray.Parse(output);

            for(int i = 0; i < jsonArray.Count; i++)
            {
                var jsonObj = (JObject)jsonArray[i];
                var properties = (JObject)jsonObj["properties"];
                var nameArray = (JArray)properties["name"];
                var nameArray2 = (JObject)nameArray[0];
                var name = nameArray2["value"].ToString();
                Product p = new Product(name);
                productList.Add(p);
            }                             

            return productList;
        }

        public async Task RemoveProductFromOrderAsync(Order order, Product product)
        {
            g = ConnectToDatabase();
            string removeProductFromOrder = "g.V().hasLabel('order').has('number','" + order.GetOrderNumber() + "').outE().hasLabel('contains_product').where(inV().hasLabel('product').has('name','" + product.GetProductName() + "')).drop()";
            await g.SubmitAsync<dynamic>(removeProductFromOrder);

            string checkIfOrderContainsProducts = "g.V().hasLabel('order').has('number','" + order.GetOrderNumber() + "').outE().hasLabel('contains_product')";
            var result2 = await g.SubmitAsync<dynamic>(checkIfOrderContainsProducts);
            var output = JsonConvert.SerializeObject(result2);

            if(output == "[]")
            {
                RemoveOrder(order);
            }
        }

        public async void AddProductToOrder(Order order, Product product)
        {
            g = ConnectToDatabase();
            string addProductToOrder = "g.V().hasLabel('order').has('number','" + order.GetOrderNumber() + "').as('a').V().hasLabel('product').has('name','" + product.GetProductName() + "').as('b').addE('contains_product').from('a').to('b')";
            await g.SubmitAsync<dynamic>(addProductToOrder);
        }

        public async void RemoveOrder(Order order)
        {
            g = ConnectToDatabase();
            string removeOrder = "g.V().hasLabel('order').has('number','" + order.GetOrderNumber() + "').drop()";
            await g.SubmitAsync<dynamic>(removeOrder);
        }

        public async Task<string> GetOrderStatusAsync(int orderNumber)
        {
            g = ConnectToDatabase();
            string getOrderStatus = "g.V().hasLabel('order').has('number','" + orderNumber + "')";
            var result = await g.SubmitAsync<dynamic>(getOrderStatus);
            var output = JsonConvert.SerializeObject(result);
            var orderArray = JArray.Parse(output);
            var orderObject = (JObject)orderArray[0];
            var properties = (JObject)orderObject["properties"];
            var statusArray = (JArray)properties["status"];
            var statusArray2 = (JObject)statusArray[0];
            string status = statusArray2["value"].ToString();

            return status;
        }

        public async Task<string> GetProductInformationAsync(Product p)
        {
            g = ConnectToDatabase();
            string getProductInformationQuery = "g.V().hasLabel('product').has('name','" + p.GetProductName() + "').properties('productinfo').value()";
            var result = await g.SubmitAsync<dynamic>(getProductInformationQuery);
            var output = JsonConvert.SerializeObject(result);
            return output;
        }
    
    }
}
