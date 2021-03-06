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
        private AuthenticationModel auth;

        public GremlinHelper(IConfiguration configuration)
        {
            this.graphendpoint = configuration["cosgraphendpoint"];
            this.graphkey = configuration["cosgraphkey"];
            auth = AuthenticationModel.Instance();
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
            string orderDate = DateTime.Today.ToString("d");

            //creeer order vertice met ordernummer
            string query = "g.addV('order').property('data','order').property('number','" + orderNumber + "').property('status','awaiting payment')";
            await g.SubmitAsync<dynamic>(query);

            //creeer edge tussen net gemaakt order, en de ingelogde gebruiker
            string edgeBetweenUserAndOrderQuery = "g.V().hasLabel('order').has('number','" + orderNumber + "').as('a').V().hasLabel('person').has('name','" + auth.GetLoggedInUser() + "').as('b').addE('has_order').from('b').to('a')";
            await g.SubmitAsync<dynamic>(edgeBetweenUserAndOrderQuery);

            //creeer edges tussen net gemaakt order, en de producten in de database adhv label = product & name = productName
            foreach (Product p in productsToStore)
            {
                string query2 = "g.V().hasLabel('order').has('number','" + orderNumber + "').as('a').V().hasLabel('product').has('name','" + p.GetProductName() + "').as('b').addE('contains_product').property('orderdate','" + orderDate + "').from('a').to('b')";
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

        public async Task<bool> StoreRepairAsync(Product p, int daysToRepair)
        {
            g = ConnectToDatabase();
            string storeRepairQuery = "g.addV('repair').property('data','repair').property('product','" + p.GetProductName() + "').property('daysToRepair','" + daysToRepair + "').as('a').V().hasLabel('person').has('name','" + auth.GetLoggedInUser() + "').as('b').addE('has_repair').from('b').to('a')";
            var result = await g.SubmitAsync<dynamic>(storeRepairQuery);
            var output = JsonConvert.SerializeObject(result);

            if (output == "[]")
            {
                return false;
            }

            return true;
        }

        public async Task<bool> ReturnOrderExistsByNumberAsync(int orderNumber)
        {
            g = ConnectToDatabase();
            string returnOrderExistsQuery = "g.V().hasLabel('return').has('number','" + orderNumber + "')";
            var result = await g.SubmitAsync<dynamic>(returnOrderExistsQuery);
            var output = JsonConvert.SerializeObject(result);

            if (output == "[]")
            {
                return false;
            }

            return true;
        }

        public async Task<bool> CreateReturnOrderAsync(Order returnOrder, Order order)
        {
            g = ConnectToDatabase();
            string orderNumber = returnOrder.GetOrderNumber().ToString();
            List<Product> productsToReturn = returnOrder.GetProducts();
            bool success = false;

            //creeer retour order vertice met ordernummer
            string query = "g.addV('return').property('data','return').property('number','" + orderNumber + "')";
            await g.SubmitAsync<dynamic>(query);

            //creeer edge tussen net gemaakt order, en de ingelogde gebruiker
            string edgeBetweenUserAndOrderQuery = "g.V().hasLabel('return').has('number','" + orderNumber + "').as('a').V().hasLabel('person').has('name','" + auth.GetLoggedInUser() + "').as('b').addE('has_return').from('b').to('a')";
            await g.SubmitAsync<dynamic>(edgeBetweenUserAndOrderQuery);

            //creeer edges tussen net gemaakt order, en de producten in de database adhv label = product & name = productName
            foreach (Product p in productsToReturn)
            {
                string query2 = "g.V().hasLabel('return').has('number','" + orderNumber + "').as('a').V().hasLabel('product').has('name','" + p.GetProductName() + "').as('b').addE('contains_product').from('a').to('b')";
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

            if (success)
            {
                foreach (Product p in productsToReturn)
                {
                    await RemoveProductFromOrderAsync(order, p);
                }
            }

            return success;
        }

        public async Task<bool> PayOrderAsync(int orderNumber)
        {
            g = ConnectToDatabase();
            string updateOrderStatusToPaid = "g.V().hasLabel('order').has('number','" + orderNumber + "').property('status','payment received')";
            var result = await g.SubmitAsync<dynamic>(updateOrderStatusToPaid);
            string output = JsonConvert.SerializeObject(result);
          
            if (output != "[]")
            {
                return true;
            }

            return false;
        }

        public async Task<bool> SetDeliveryStatusAsync(int orderNumber, int daysToDelivery, string deliveryTime)
        {
            g = ConnectToDatabase();
            string setDeliveryStatus = "";
            if(daysToDelivery > 0)
            {
                setDeliveryStatus = "g.V().hasLabel('order').has('number','" + orderNumber + "').property('status','delivery in " + daysToDelivery + " days, time: " + deliveryTime + "')";
            }
            else if(daysToDelivery == 0)
            {
                setDeliveryStatus = "g.V().hasLabel('order').has('number','" + orderNumber + "').property('status','being delivered')";              
            }
            else if(daysToDelivery < 0)
            {
                setDeliveryStatus = "g.V().hasLabel('order').has('number','" + orderNumber + "').property('status','delivered')";
            }

            var result = await g.SubmitAsync<dynamic>(setDeliveryStatus);
            string output = JsonConvert.SerializeObject(result);

            if (output != "[]")
            {
                return true;
            }

            return false;
        }

        public async Task SetOrderPartiallyPaidAsync(int orderNumber)
        {
            g = ConnectToDatabase();
            string orderNumberString = orderNumber.ToString();
            string setOrderPartiallyPaid = "g.V().hasLabel('order').has('number','" + orderNumber + "').property('status','partially paid')";
            await g.SubmitAsync<dynamic>(setOrderPartiallyPaid);
        }

        public async Task<bool> ProductExistsAsync(string productName)
        {
            g = ConnectToDatabase();
            string query = "g.V().hasLabel('product').has('name','" + productName + "')";
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

        public async Task<bool> OrderBelongsToUserAsync(string orderNumber)
        {
            g = ConnectToDatabase();
            string orderBelongsToUserQuery = "g.V().hasLabel('person').has('name','" + auth.GetLoggedInUser() + "').outE().hasLabel('has_order').inV().hasLabel('order').has('number','" + orderNumber + "')";
            var result = await g.SubmitAsync<dynamic>(orderBelongsToUserQuery);
            var output = JsonConvert.SerializeObject(result);

            if(output != "[]")
            {
                return true;
            }

            return false;
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
                await RemoveOrderAsync(order);
            }
        }

        public async Task AddProductToOrderAsync(Order order, Product product)
        {
            string orderDate = DateTime.Today.ToString("o");
            g = ConnectToDatabase();
            string addProductToOrder = "g.V().hasLabel('order').has('number','" + order.GetOrderNumber() + "').as('a').V().hasLabel('product').has('name','" + product.GetProductName() + "').as('b').addE('contains_product').property('orderdate','" + orderDate + "').from('a').to('b')";
            await g.SubmitAsync<dynamic>(addProductToOrder);
            await SetOrderPartiallyPaidAsync(order.GetOrderNumber());
        }

        public async Task RemoveOrderAsync(Order order)
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
            string getProductInformationQuery = "g.V().hasLabel('product').has('type','" + p.GetProductName() + "').properties('productinfo').value()";
            var result = await g.SubmitAsync<dynamic>(getProductInformationQuery);
            var output = JsonConvert.SerializeObject(result);
            return output;
        }

        public async Task<string> GetProductInformationPerTypeAsync(string type)
        {
            g = ConnectToDatabase();          
            string getProductsPerTypeQuery = "g.V().hasLabel('product').has('type','" + type + "')";
            var result = await g.SubmitAsync<dynamic>(getProductsPerTypeQuery);
            var output = JsonConvert.SerializeObject(result);
           
            return output;
        }

        public async Task<bool> ProductTypeExistsAsync(string type)
        {
            g = ConnectToDatabase();
            string getProductsPerTypeQuery = "g.V().hasLabel('product').has('type','" + type + "')";
            var result = await g.SubmitAsync<dynamic>(getProductsPerTypeQuery);
            var output = JsonConvert.SerializeObject(result);

            if (output == "[]")
            {
                return false;
            }

            return true;
        }

        public async Task<bool> StoreComplaintAsync(int orderNumber, string complaint)
        {
            g = ConnectToDatabase();
            string storeComplaintQuery = "g.addV('complaint').property('data','complaint').property('complaint','" + complaint + "').property('orderNumber','" + orderNumber + "')";
            var result = await g.SubmitAsync<dynamic>(storeComplaintQuery);

            var output = JsonConvert.SerializeObject(result);

            if (output == "[]")
            {
                return false;
            }
            else
            {
                string addComplaintToOrderQuery = "g.V().hasLabel('complaint').has('orderNumber','" + orderNumber + "').as('a').V().hasLabel('order').has('number','" + orderNumber + "').as('b').addE('belongs_to').from('a').to('b')";
                var result2 = await g.SubmitAsync<dynamic>(addComplaintToOrderQuery);
                var output2 = JsonConvert.SerializeObject(result2);

                if(output2 == "[]")
                {
                    return false;
                }

                string addComplaintToUserQuery = "g.V().hasLabel('complaint').has('orderNumber','" + orderNumber + "').as('a').V().hasLabel('person').has('name','" + auth.GetLoggedInUser() + "').as('b').addE('has_complaint').from('b').to('a')";
                var result3 = await g.SubmitAsync<dynamic>(addComplaintToUserQuery);
                var output3 = JsonConvert.SerializeObject(result2);

                if (output3 == "[]")
                {
                    return false;
                }
            }

            return true;
        }

        public async Task<bool> CheckCredentialsAsync(string username, string password)
        {
            g = ConnectToDatabase();
            string checkCredentialsQuery = "g.V().hasLabel('person').has('name','" + username + "').has('password','" + password + "')";
            var result = await g.SubmitAsync<dynamic>(checkCredentialsQuery);
            var output = JsonConvert.SerializeObject(result);

            if(output != "[]")
            {
                return true;
            }

            return false;
        }

        public async Task<bool> CreateAccountAsync(string username, string password)
        {
            g = ConnectToDatabase();
            string createAccountQuery = "g.addV('person').property('data','person').property('name','" + username + "').property('password','" + password + "')";
            var result = await g.SubmitAsync<dynamic>(createAccountQuery);
            var output = JsonConvert.SerializeObject(result);

            if (output != "[]")
            {
                return true;
            }

            return false;
        }

        public async Task<bool> CheckIfProductIsRepairableAsync(Product p)
        {
            g = ConnectToDatabase();
            string checkIfProductIsRepairableQuery = "g.V().hasLabel('product').has('name','" + p.GetProductName() + "').has('repairable', true)";
            var result = await g.SubmitAsync<dynamic>(checkIfProductIsRepairableQuery);
            var output = JsonConvert.SerializeObject(result);

            if (output != "[]")
            {
                return true;
            }

            return false;
        }

        public async Task<string> GetProductWarrantyAsync(int orderNumber, Product p)
        {
            g = ConnectToDatabase();
            string getProductWarrantyQuery = "g.V().hasLabel('order').has('number','" + orderNumber + "').outE().hasLabel('contains_product').where(inV().hasLabel('product').has('name','" + p.GetProductName() + "'))";
            var result = await g.SubmitAsync<dynamic>(getProductWarrantyQuery);
            var output = JsonConvert.SerializeObject(result);
            var edge = JArray.Parse(output);
            var edgeObject = (JObject)edge[0];
            var properties = (JObject)edgeObject["properties"];
            var orderDate = properties["orderdate"];

            return orderDate.ToString();
        }

        public async Task<int> GetOrderNumberByPersonAsync()
        {
            g = ConnectToDatabase();
            string getOrderNumberByPersonQuery = "g.V().hasLabel('person').has('name', '" + auth.GetLoggedInUser() + "').outE().hasLabel('has_order').inV()";
            var result = await g.SubmitAsync<dynamic>(getOrderNumberByPersonQuery);
            var output = JsonConvert.SerializeObject(result);
            var orders = JArray.Parse(output);

            if(orders.Count != 1)
            {
                return 0;
            }
            else
            {
                var orderObject = (JObject)orders[0];
                var properties = (JObject)orderObject["properties"];
                var number = (JArray)properties["number"];
                var number2 = (JObject)number[0];
                var orderNumber = number2["value"].ToString();

                return Int32.Parse(orderNumber);
            }
        }
    }
}
