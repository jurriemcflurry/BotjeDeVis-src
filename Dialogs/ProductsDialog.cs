using CoreBot.CognitiveModels;
using CoreBot.Database;
using CoreBot.Models;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.BotBuilderSamples.Dialogs;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CoreBot.Dialogs
{
    public class ProductsDialog : CancelAndHelpDialog
    {
        private LuisHelper luisResult = null;
        private List<Product> productList = new List<Product>();
        private string productListString = "";
        private GremlinHelper gremlinHelper;
        private string productInfo = "";

        public ProductsDialog(IConfiguration configuration) : base(nameof(ProductsDialog))
        {
            gremlinHelper = new GremlinHelper(configuration);

            AddDialog(new TextPrompt(nameof(TextPrompt)));
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
        {
                ConfirmProductsIntentAsync,
                GiveInformationAboutProductsAsync,
                FinalStepAsync,
        }));

            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> ConfirmProductsIntentAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            luisResult = (LuisHelper)stepContext.Options;
            string[] products = luisResult.Entities.products;           

            for(int i = 0; i < products.Length; i++)
            {
                string product = products[i];
                string newProducts = Regex.Replace(product, " ", string.Empty);               
                Product p = new Product(newProducts);
                productList.Add(p);
            }

            foreach(Product p in productList)
            {
                productListString += p.GetProductName() + " en ";
            }
            productListString = productListString.Remove(productListString.Length - 4);

            string confirmProductQuestion = "Ik begrijp dat je een vraag hebt over " + productListString + ".";
            await stepContext.Context.SendActivityAsync(confirmProductQuestion);
            return await stepContext.NextAsync(null, cancellationToken);
        }

        private async Task<DialogTurnResult> GiveInformationAboutProductsAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            foreach(Product p in productList)
            {
                productInfo = await gremlinHelper.GetProductInformationAsync(p);
                productInfo = productInfo.Replace("[", string.Empty);
                productInfo = productInfo.Replace("]", string.Empty);
                productInfo = productInfo.Replace('"', ' ');
                productInfo = productInfo.Replace(",", Environment.NewLine);
                await stepContext.Context.SendActivityAsync("Daarover heb ik de volgende informatie:");
                string productName = p.GetProductName().First().ToString().ToUpper() + p.GetProductName().Substring(1);
                await stepContext.Context.SendActivityAsync(productName + ":" + Environment.NewLine + productInfo);
            }

            return await stepContext.NextAsync();
        }

        private async Task<DialogTurnResult> FinalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            productListString = "";
            productInfo = "";
            productList.Clear();
            return await stepContext.EndDialogAsync();
        }
    }
}
