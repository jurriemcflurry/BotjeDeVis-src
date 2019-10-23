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

    //het toevoegen van producten aan de lijst moet een loop worden, zodat je meerdere producten kunt bestellen en niet na 1 product stopt
    //wellicht de stap addProducts in een andere dialog, waar geloopt kan worden tot het wordt bevestigd. Daarna terugkeren naar de juiste stap in deze dialog
    //of optie te beginnen met andere dialoog om producten in een loop toe te voegen, en alleen de volledige lijst mee te geven naar deze dialog
    public class OrdersDialog : CancelAndHelpDialog
    {
        private GremlinHelper gremlinHelper;
        private LuisHelper luisResult = null;
        private string[] productsString;
        private List<Product> productList = new List<Product>();
        private string productListString = "Producten: ";
        private Order order;
        private string whatToDo = "";

        public OrdersDialog(IConfiguration configuration) : base(nameof(OrdersDialog))
        {
            //gremlinHelper om later snel met de database te kunnen werken
            gremlinHelper = new GremlinHelper(configuration);

            //voeg de dialogs toe
            AddDialog(new TextPrompt(nameof(TextPrompt)));
            AddDialog(new ChoicePrompt(nameof(ChoicePrompt)));
            AddDialog(new AddProductsToOrderDialog(configuration));
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                CheckForProductsAsync,
                PromptQuestionAsync,
                HandleChoiceAsync,
                UpdateProductListAsync,
                ConfirmOrderAsync,
                StoreOrderAsync,                
                FinalStepAsync,
            }));

            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> CheckForProductsAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            productListString = "";

            if (luisResult == null && productList.Count == 0)
            {
                luisResult = (LuisHelper)stepContext.Options;
                productsString = luisResult.Entities.products;

                if (productsString != null)
                {
                    foreach (string p in productsString)
                    {
                        Product product = new Product(p);
                 
                        bool productExists = await gremlinHelper.ProductExistsAsync(product);

                        if (productExists)
                        {
                            productList.Add(product);
                        }
                        else
                        {
                            await stepContext.Context.SendActivityAsync("Product " + p + " is helaas niet in ons assortiment.");
                        }
                    }

                    return await stepContext.NextAsync();
                }
                else 
                {
                    return await stepContext.NextAsync();
                }
            }
            else
            {
                productList = (List<Product>)stepContext.Options;
                return await stepContext.NextAsync();
            }
            
        }

        private async Task<DialogTurnResult> PromptQuestionAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            return await stepContext.PromptAsync(nameof(ChoicePrompt), new PromptOptions
            {
                Prompt = MessageFactory.Text("Wil je nog een product toevoegen of verwijderen?"),
                Choices = ChoiceFactory.ToChoices(new List<string> { "Toevoegen", "Verwijderen", "Klaar met bestellen" })
            }, cancellationToken);
        }

        private async Task<DialogTurnResult> HandleChoiceAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            FoundChoice choice = (FoundChoice)stepContext.Result;
            whatToDo = choice.Value;
            switch (choice.Index)
            {
                case 0:                   
                    var messageText = "Welk product wil je toevoegen?";
                    var promptMessage = MessageFactory.Text(messageText, messageText, InputHints.ExpectingInput);
                    return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = promptMessage }, cancellationToken);
                case 1:
                    List<string> productsString = new List<string>();
                    foreach (Product p in productList)
                    {
                        productsString.Add(p.GetProductName());
                    }
                    return await stepContext.PromptAsync(nameof(ChoicePrompt), new PromptOptions
                    {
                        Prompt = MessageFactory.Text("Welk product wil je verwijderen?"),
                        Choices = ChoiceFactory.ToChoices(productsString)
                    }, cancellationToken);
                case 2:
                    return await stepContext.NextAsync();
            }

            return await stepContext.NextAsync();
        }

        private async Task<DialogTurnResult> UpdateProductListAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if (whatToDo.Equals("Toevoegen"))
            {
                string productName = (string)stepContext.Result;
                Product product = new Product(productName); //hele string wordt gepakt als product, kijken om te verbeteren (bijv nog een Luis-call om entiteit te filteren)

                bool productExists = await gremlinHelper.ProductExistsAsync(product);

                if (productExists)
                {
                    productList.Add(product);
                }
                else
                {
                    await stepContext.Context.SendActivityAsync("Product " + product.GetProductName() + " is helaas niet in ons assortiment.");
                }
            }
            else if (whatToDo.Equals("Verwijderen"))
            {
                FoundChoice choice = (FoundChoice)stepContext.Result;
                string productFound = choice.Value.ToString();

                foreach (Product p in productList)
                {
                    if (p.GetProductName().Equals(productFound))
                    {
                        productList.Remove(p);
                        await stepContext.Context.SendActivityAsync("Product " + productFound + " is verwijderd uit uw bestelling.");
                        break;
                    }
                }
            }
            else 
            {
                return await stepContext.NextAsync();
            }

            await stepContext.Context.SendActivityAsync("De volgende producten staan nu in je bestelling:");
            foreach (Product p in productList)
            {
                productListString += p.GetProductName() + ", ";
            }
            productListString = productListString.Remove(productListString.Length - 2);
            await stepContext.Context.SendActivityAsync(productListString);
            return await stepContext.ReplaceDialogAsync(nameof(OrdersDialog), productList, cancellationToken);
        }

        private async Task<DialogTurnResult> ConfirmOrderAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
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

        private async Task<DialogTurnResult> StoreOrderAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            FoundChoice choice = (FoundChoice)stepContext.Result;

            if (choice.Index == 0)
            {
                Random rnd = new Random();
                int orderNumber = rnd.Next(1, 99999);

                while (await gremlinHelper.OrderExistsByNumberAsync(orderNumber))
                {
                    orderNumber = rnd.Next(1, 99999);
                }

                order = new Order(orderNumber, productList);
          
                await stepContext.Context.SendActivityAsync("De volgende producten worden voor je besteld met ordernummer " + order.GetOrderNumber().ToString() + ":");
                await stepContext.Context.SendActivityAsync(productListString);
                
                try
                {
                    bool success = await gremlinHelper.StoreOrderAsync(order);
                    if (success)
                    {
                        await stepContext.Context.SendActivityAsync("Bestelling geslaagd! Bedankt voor het shoppen bij ons!");
                    }
                    else
                    {
                        await stepContext.Context.SendActivityAsync("Het lukte helaas niet om je bestelling uit te voeren. Probeer het later opnieuw.");
                    }                    
                }
                catch
                {
                    await stepContext.Context.SendActivityAsync("Het lukte helaas niet om je bestelling uit te voeren. Probeer het later opnieuw.");
                }

                return await stepContext.NextAsync();
            }
            else
            {               
                return await stepContext.NextAsync();
            }

        }

        private async Task<DialogTurnResult> FinalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            productList.Clear();
            return await stepContext.EndDialogAsync();
        }
    }
}