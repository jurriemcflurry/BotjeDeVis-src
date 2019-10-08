using CoreBot.CognitiveModels;
using CoreBot.Database;
using CoreBot.Models;
using Gremlin.Net.Driver;
using Gremlin.Net.Structure.IO.GraphSON;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.BotBuilderSamples.Dialogs;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CoreBot.Dialogs
{
    public class OrdersDialog : CancelAndHelpDialog
    {
        private GremlinHelper gremlinHelper;
        private LuisHelper luisResult;
        private string[] productsString;
        private List<Product> productList;
        
        public OrdersDialog(IConfiguration configuration) : base(nameof(OrdersDialog))
        {
            //maak connectie met database
            gremlinHelper = new GremlinHelper(configuration);

            //voeg de dialogs toe
            AddDialog(new TextPrompt(nameof(TextPrompt)));
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                CheckForProducts,
                CreateOrder,
                StoreOrder,
                FinalStep,
            }));

            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> CheckForProducts(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            luisResult = (LuisHelper)stepContext.Options;
            productsString = luisResult.Entities.products;
            productList = new List<Product>();

            if (productsString != null)
            {
                foreach (string p in productsString)
                {
                    Product product = new Product(p);
                    
                    //kijk of het product een bestaand product is in de database                   
                    bool productExists = await gremlinHelper.ProductExists(product); //hier gaat ergens iets fout --> checken
                    await stepContext.Context.SendActivityAsync(productExists.ToString());

                    if (productExists)
                    {                        
                        //zo ja, voeg toe aan de lijst
                        productList.Add(product);
                    }
                    else
                    {
                        //zo nee, helaas niet in assortiment
                        await stepContext.Context.SendActivityAsync("Product " + p + " is helaas niet in ons assortiment.");
                    }
                }
            }
            else
            {
                var messageText = "Wat wil je bestellen?";
                var promptMessage = MessageFactory.Text(messageText, messageText, InputHints.ExpectingInput);
                return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = promptMessage }, cancellationToken);
            }
            
            return await stepContext.NextAsync();
        }

        private async Task<DialogTurnResult> CreateOrder(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            //als er resultaat meekomt, betekent dit dat er in de vorige stap een product is gevraagd. Deze wordt hier aan de lijst toegevoegd.
            if(stepContext.Result != null)
            {
                Product product = new Product(stepContext.Result.ToString()); //hele string wordt gepakt als product, kijken om te verbeteren (nog een Luis-call om entiteit te filteren)
                productList.Add(product);
            }

            //als alles producten in de lijst staan, moet dit een order worden
            Random rnd = new Random();
            int ordernumber = rnd.Next(1, 99999);
            Order order = new Order(ordernumber, productList);

            await stepContext.Context.SendActivityAsync("De volgende producten worden voor je besteld met ordernummer " + order.GetOrderNumber().ToString() + ":");

            foreach (Product product in productList)
            {
                await stepContext.Context.SendActivityAsync(product.GetProductName());
            }
            
            return await stepContext.NextAsync(order, cancellationToken);
        }

        private async Task<DialogTurnResult> StoreOrder(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
        //    Order order = (Order)stepContext.Result;
        //    gremlinHelper.StoreOrder(order); //implementeren 
        
            return await stepContext.NextAsync(null, cancellationToken);
        }

        private async Task<DialogTurnResult> FinalStep(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            return await stepContext.NextAsync(null, cancellationToken);
        }
    }
}