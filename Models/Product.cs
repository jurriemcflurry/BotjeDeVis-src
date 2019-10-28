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
        private List<string> productInfo;

        public Product(string productName,int productNumber, List<string> productInfo)
        {
            this.productName = productName;
            this.productNumber = productNumber;
            this.productInfo = productInfo;
        }

        public Product(string productName, List<string> productInfo)
        {
            this.productName = productName;
            this.productInfo = productInfo;
        }

        public Product(string productName)
        {
            this.productName = productName;
        }

        public Product() { }

        public string GetProductName()
        {
            return productName;
        }

        public int GetProductNumber()
        {
            return productNumber;
        }

        public List<string> getProductInfo()
        {
            return productInfo;
        }
    }
}
