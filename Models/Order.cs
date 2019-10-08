using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CoreBot.Models
{
    public class Order
    {
        private int number;
        private List<Product> products;

        public Order(int number, List<Product> products)
        {
            this.number = number;
            this.products = products;
        }

        public int GetOrderNumber()
        {
            return number;
        }

        public List<Product> GetProducts()
        {
            return products;
        }
    }
}
