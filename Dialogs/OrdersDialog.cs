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
        private List<Product> productList;
        private string productListString = "Producten: ";

        public OrdersDialog(IConfiguration configuration) : base(nameof(OrdersDialog))
        {
            //gremlinHelper om later snel met de database te kunnen werken
            gremlinHelper = new GremlinHelper(configuration);

            //voeg de dialogs toe
            AddDialog(new TextPrompt(nameof(TextPrompt)));
            AddDialog(new ChoicePrompt(nameof(ChoicePrompt)));
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
            luisResult = (LuisHelper)stepContext.Options;
            productsString = luisResult.Entities.products;
            productList = new List<Product>();

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
                productList.Add(product);
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
                Choices = ChoiceFactory.ToChoices(new List<string> { "Bevestigen", "Annuleren"})
            }, cancellationToken);

        }

        private async Task<DialogTurnResult> StoreOrder(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            FoundChoice choice = (FoundChoice)stepContext.Result;
            if(choice.Index == 0)
            {
                //gekozen voor bevestigen, dus nu wordt de order aangemaakt
                Random rnd = new Random();
                int ordernumber = rnd.Next(1, 99999);
                Order order = new Order(ordernumber, productList);

                if (productList.Count > 0)
                {
                    await stepContext.Context.SendActivityAsync("De volgende producten worden voor je besteld met ordernummer " + order.GetOrderNumber().ToString() + ":");
                    await stepContext.Context.SendActivityAsync(productListString);
                }

                bool orderStored = await gremlinHelper.StoreOrder(order);

                if (orderStored)
                {
                    await stepContext.Context.SendActivityAsync("Bestelling geslaagd! Bedankt voor het shoppen bij ons!");
                }
                else
                {
                    await stepContext.Context.SendActivityAsync("Het lukte helaas niet om je bestelling uit te voeren. Probeer het later opnieuw.");
                }
            }
            else if(choice.Index == 1)
            {
                return await stepContext.EndDialogAsync();
            }

            return await stepContext.NextAsync(null, cancellationToken);
        }

        private async Task<DialogTurnResult> FinalStep(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            productList.Clear();
            return await stepContext.EndDialogAsync();
        }
    }
}