using CoreBot.CognitiveModels;
using CoreBot.Database;
using CoreBot.Models;
using Gremlin.Net.Driver;
using Gremlin.Net.Structure.IO.GraphSON;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
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
        private List<Product> productList = new List<Product>();
        private string productListString = "Producten: ";
        private IConfiguration configuration;

        public OrdersDialog(IConfiguration configuration) : base(nameof(OrdersDialog))
        {
            this.configuration = configuration;
            //gremlinHelper om later snel met de database te kunnen werken
            gremlinHelper = new GremlinHelper(configuration);

            //voeg de dialogs toe

            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                CheckForProducts,
                AddProducts,
                ConfirmOrder,
                StoreOrder,
                FinalStep,
            }));

            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> CheckForProducts(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            string entryType = stepContext.Options.GetType().ToString();

            switch (entryType)
            {
                case "CoreBot.CognitiveModels.LuisHelper":
                    luisResult = (LuisHelper)stepContext.Options;
                    productsString = luisResult.Entities.products;
                    break;
                case "System.Collections.Generic.List`1[CoreBot.Models.Product]":
                    var messageText = "Wat wil je bestellen?";
                    var promptMessage = MessageFactory.Text(messageText, messageText, InputHints.ExpectingInput);
                    return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = promptMessage }, cancellationToken);
                default:
                    return await stepContext.NextAsync();
            }
                        
           
            if (productsString != null)
            {
                foreach (string p in productsString)
                {
                    Product product = new Product(p);
                    
                    //kijk of het product een bestaand product is in de database                   
                    bool productExists = await gremlinHelper.ProductExists(product);

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

                    // eindig deze dialoog als er alleen niet-bestaande producten worden gevraagd
                    if(productList == null)
                    {
                        return await stepContext.EndDialogAsync();
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

        private async Task<DialogTurnResult> AddProducts(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            //als er resultaat meekomt, betekent dit dat er in de vorige stap een product is gevraagd. Deze wordt hier aan de lijst toegevoegd.
            if(stepContext.Result != null)
            {
                Product product = new Product(stepContext.Result.ToString()); //hele string wordt gepakt als product, kijken om te verbeteren (nog een Luis-call om entiteit te filteren)

                bool productExists = await gremlinHelper.ProductExists(product);

                if (productExists)
                {
                    //zo ja, voeg toe aan de lijst
                    productList.Add(product);
                }
                else
                {
                    //zo nee, helaas niet in assortiment
                    await stepContext.Context.SendActivityAsync("Product " + product.GetProductName() + " is helaas niet in ons assortiment.");
                }

                // eindig deze dialoog als er alleen niet-bestaande producten worden gevraagd
                if (productList == null)
                {
                    return await stepContext.EndDialogAsync();
                }               
            }       
                     
            return await stepContext.NextAsync(cancellationToken);
        }

        private async Task<DialogTurnResult> ConfirmOrder(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            await stepContext.Context.SendActivityAsync("De volgende producten staan in je bestelling:");
            foreach (Product product in productList)
            {
                productListString += product.GetProductName() + ", ";
            }
            productListString = productListString.Remove(productListString.Length - 2);
            await stepContext.Context.SendActivityAsync(productListString);

            return await stepContext.PromptAsync(nameof(ChoicePrompt), new PromptOptions
            {
                Prompt = MessageFactory.Text("Bevestig de bestelling"),
                RetryPrompt = MessageFactory.Text("Probeer het nog een keer"),
                Choices = ChoiceFactory.ToChoices(new List<string> { "Bevestigen", "Wijzigen", "Annuleren"})
            }, cancellationToken);

        }

        private async Task<DialogTurnResult> StoreOrder(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            FoundChoice choice = (FoundChoice)stepContext.Result;
            if(choice.Index == 0)
            {
                //gekozen voor bevestigen, dus nu wordt de order aangemaakt
                Random rnd = new Random();
                int orderNumber = rnd.Next(1, 99999);

                //check if order exists
                bool orderExists;

                //maak een nieuw ordernummer als het nummer al bestaat
                while (orderExists = await gremlinHelper.OrderExistsByNumber(orderNumber))
                {
                    orderNumber = rnd.Next(1, 99999);
                }

                //nu ordernummer uniek is, maak orderobject aan;
                Order order = new Order(orderNumber, productList);

                //geef feedback aan de user over de order die wordt geplaatst
                if (productList.Count > 0)
                {
                    await stepContext.Context.SendActivityAsync("De volgende producten worden voor je besteld met ordernummer " + order.GetOrderNumber().ToString() + ":");
                    await stepContext.Context.SendActivityAsync(productListString);
                }

                try
                {
                    await gremlinHelper.StoreOrder(order);
                    await stepContext.Context.SendActivityAsync("Bestelling geslaagd! Bedankt voor het shoppen bij ons!");
                }
                catch
                {
                    await stepContext.Context.SendActivityAsync("Het lukte helaas niet om je bestelling uit te voeren. Probeer het later opnieuw.");
                }
                
            }
            else if(choice.Index == 1)
            {                
                //overwegen om dit wellicht hier af te handelen
                productListString = "Producten: ";
                return await stepContext.ReplaceDialogAsync(nameof(ChangeOrderDialog), productList, cancellationToken);
            }
            else if(choice.Index == 2)
            {
                return await stepContext.NextAsync();
            }

            return await stepContext.NextAsync();
        }

        private async Task<DialogTurnResult> FinalStep(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            //productlijst moet geleegd worden na gedane bestelling, zodat dit geen probleem oplevert bij een volgende order
            productList.Clear();
            productListString = "Producten: ";
            return await stepContext.EndDialogAsync();
        }
    }
}