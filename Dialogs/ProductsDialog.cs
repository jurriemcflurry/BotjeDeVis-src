using CoreBot.CognitiveModels;
using CoreBot.Database;
using CoreBot.Models;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.BotBuilderSamples.Dialogs;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
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
        private List<string> productTypeList = new List<string>();
        private string productInfoOutput = "";
        private string productInfoForCard = "";
        private GremlinHelper gremlinHelper;
        private List<string> productInfo = new List<string>();

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
            string[] productTypes = luisResult.Entities.products;           

            for(int i = 0; i < productTypes.Length; i++)
            {
                string productType = productTypes[i];
                string newProductType = Regex.Replace(productType, " ", string.Empty);               
                productTypeList.Add(newProductType);
            }
            
            return await stepContext.NextAsync(null, cancellationToken);
        }

        private async Task<DialogTurnResult> GiveInformationAboutProductsAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var attachments = new List<Attachment>();
            var reply = MessageFactory.Attachment(attachments);
            reply.AttachmentLayout = AttachmentLayoutTypes.Carousel;

            await CreateCardsAsync(reply, productTypeList);           

            await stepContext.Context.SendActivityAsync(reply, cancellationToken);
            return await stepContext.NextAsync();
        }

        private async Task<DialogTurnResult> FinalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            productInfo.Clear();
            productTypeList.Clear();
            return await stepContext.EndDialogAsync();
        }

        private async Task CreateCardsAsync(IMessageActivity reply, List<string> productTypeList)
        {
            foreach (string type in productTypeList)
            {
                productInfoOutput = await gremlinHelper.GetProductInformationPerTypeAsync(type);

                List<string> productInfoList = new List<string>();
                var productArray = JArray.Parse(productInfoOutput);

                for (int i = 0; i < productArray.Count; i++)
                {
                    productInfoList.Clear();
                    productInfoForCard = "";
                    var product = (JObject)productArray[i];
                    var properties = (JObject)product["properties"];
                    var productInfo = (JArray)properties["productinfo"];
                    var productName = (JArray)properties["name"];
                    var productName2 = (JObject)productName[0];
                    var productNameValue = productName2["value"];

                    for (int j = 0; j < productInfo.Count; j++)
                    {
                        var productInfo2 = (JObject)productInfo[j];
                        var productInfoValue = productInfo2["value"];
                        productInfoList.Add(productInfoValue.ToString());
                    }

                    foreach (string info in productInfoList)
                    {
                        productInfoForCard += info + Environment.NewLine;
                    }

                    Product p = new Product(productNameValue.ToString(), productInfoList);
                    reply.Attachments.Add(Cards.GetThumbnailCard(p.GetProductName(), productInfoForCard).ToAttachment());
                }
            }

            if (productInfoOutput.Equals("[]")) //niets gevonden met type, check op meervoud met 's'
            {
                foreach (string type in productTypeList)
                {
                    string type2 = type.Remove(type.Length - 1);
                    productInfoOutput = await gremlinHelper.GetProductInformationPerTypeAsync(type2);

                    List<string> productInfoList = new List<string>();
                    var productArray = JArray.Parse(productInfoOutput);

                    for (int i = 0; i < productArray.Count; i++)
                    {
                        productInfoList.Clear();
                        productInfoForCard = "";
                        var product = (JObject)productArray[i];
                        var properties = (JObject)product["properties"];
                        var productInfo = (JArray)properties["productinfo"];
                        var productName = (JArray)properties["name"];
                        var productName2 = (JObject)productName[0];
                        var productNameValue = productName2["value"];

                        for (int j = 0; j < productInfo.Count; j++)
                        {
                            var productInfo2 = (JObject)productInfo[j];
                            var productInfoValue = productInfo2["value"];
                            productInfoList.Add(productInfoValue.ToString());
                        }

                        foreach (string info in productInfoList)
                        {
                            productInfoForCard += info + Environment.NewLine;
                        }

                        Product p = new Product(productNameValue.ToString(), productInfoList);
                        reply.Attachments.Add(Cards.GetThumbnailCard(p.GetProductName(), productInfoForCard).ToAttachment());
                    }
                }
            }

            if (productInfoOutput.Equals("[]")) //niets gevonden met type, check op meervoud met 'en'
            {
                foreach (string type in productTypeList)
                {
                    string type3 = type.Remove(type.Length - 2);
                    productInfoOutput = await gremlinHelper.GetProductInformationPerTypeAsync(type3);

                    List<string> productInfoList = new List<string>();
                    var productArray = JArray.Parse(productInfoOutput);

                    for (int i = 0; i < productArray.Count; i++)
                    {
                        productInfoList.Clear();
                        productInfoForCard = "";
                        var product = (JObject)productArray[i];
                        var properties = (JObject)product["properties"];
                        var productInfo = (JArray)properties["productinfo"];
                        var productName = (JArray)properties["name"];
                        var productName2 = (JObject)productName[0];
                        var productNameValue = productName2["value"];

                        for (int j = 0; j < productInfo.Count; j++)
                        {
                            var productInfo2 = (JObject)productInfo[j];
                            var productInfoValue = productInfo2["value"];
                            productInfoList.Add(productInfoValue.ToString());
                        }

                        foreach (string info in productInfoList)
                        {
                            productInfoForCard += info + Environment.NewLine;
                        }

                        Product p = new Product(productNameValue.ToString(), productInfoList);
                        reply.Attachments.Add(Cards.GetThumbnailCard(p.GetProductName(), productInfoForCard).ToAttachment());
                    }
                }
            }
        }
    }
}
