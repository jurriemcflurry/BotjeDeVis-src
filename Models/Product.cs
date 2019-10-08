using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CoreBot.Models
{
    public class Product
    {
        private string productName;
        private int productNumber;

        public Product(string productName,int productNumber)
        {
            this.productName = productName;
            this.productNumber = productNumber;
        }

        public Product(string productName)
        {
            this.productName = productName;
        }

        public string GetProductName()
        {
            return productName;
        }

        public int GetProductNumber()
        {
            return productNumber;
        }
    }
}
