using CoreBot.CognitiveModels;
using CoreBot.Database;
using CoreBot.Models;
using Gremlin.Net.Driver;
using Gremlin.Net.Structure.IO.GraphSON;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Bot.Schema;
using Microsoft.BotBuilderSamples;
using Microsoft.BotBuilderSamples.Dialogs;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CoreBot.Dialogs
{
    public class OrdersDialog : CancelAndHelpDialog
    {
        private GremlinHelper gremlinHelper;
        private LuisHelper luisResult = null;
        private string[] productsString;
        private List<Product> productList = new List<Product>();
        private string productListString = "Producten: ";
        private Order order;
        private string whatToDo = "";
        private List<string> productTypeList = new List<string>();
        private string productInfoOutput = "";
        private string productInfoForCard = "";
        private readonly WebshopRecognizer _luisRecognizer;

        public OrdersDialog(IConfiguration configuration, WebshopRecognizer luisRecognizer) : base(nameof(OrdersDialog))
        {
            // gremlinHelper to handle databaseinteraction
            gremlinHelper = new GremlinHelper(configuration);

            //luisRecognizer for making LUIS calls
            _luisRecognizer = luisRecognizer;

            AddDialog(new TextPrompt(nameof(TextPrompt)));
            AddDialog(new ChoicePrompt(nameof(ChoicePrompt)));
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

        //check for already mentioned products; either coming in with the luisResult from the MainDialog, or the productlist from a previous iteration
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
                        string newProduct = Regex.Replace(p, " ", string.Empty);                        
                 
                        bool productExists = await gremlinHelper.ProductExistsAsync(newProduct);

                        if (productExists)
                        {
                            Product product = new Product(newProduct);
                            productList.Add(product);
                        }
                        else
                        {
                            
                            bool productTypeExists = await gremlinHelper.ProductTypeExistsAsync(newProduct);

                            if (productTypeExists)
                            {
                                //info over product (proberen deze dialog te eindigen en productdialog te starten?)
                                await stepContext.Context.SendActivityAsync("Hier wordt de info over dit type product getoond (dit wordt nog geimplementeerd!)");
                            }
                            else
                            {
                                await stepContext.Context.SendActivityAsync("Product " + newProduct + " is helaas niet in ons assortiment.");
                            }
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

        //ask the user what they want to do with the current order that is in process
        private async Task<DialogTurnResult> PromptQuestionAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            return await stepContext.PromptAsync(nameof(ChoicePrompt), new PromptOptions
            {
                Prompt = MessageFactory.Text("Wil je een product toevoegen of verwijderen?"),
                Choices = ChoiceFactory.ToChoices(new List<string> { "Toevoegen", "Verwijderen", "Klaar met bestellen" })
            }, cancellationToken);
        }

        //handle the user input, and act accordingly
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

        //update the producylist by either adding an extra product, or removing selected product.
        //skip to confirmOrder in case the user selected they are done with their order
        private async Task<DialogTurnResult> UpdateProductListAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if (whatToDo.Equals("Toevoegen"))
            {
                // call luis to filter the entities from the input
                luisResult = await _luisRecognizer.RecognizeAsync<LuisHelper>(stepContext.Context, cancellationToken);
                string[] productsAsked = luisResult.Entities.products;

                for(int i = 0; i < productsAsked.Length; i++)
                {
                    string newProductName = Regex.Replace(productsAsked[i], " ", string.Empty);                   
                    bool productExists = await gremlinHelper.ProductExistsAsync(newProductName);

                    if (productExists)
                    {
                        Product product = new Product(newProductName);
                        productList.Add(product);
                    }
                    else
                    {
                        await stepContext.Context.SendActivityAsync("Product " + newProductName + " is helaas niet in ons assortiment.");
                    }
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

        //ask the user to confirm their order
        private async Task<DialogTurnResult> ConfirmOrderAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if(productList.Count == 0)
            {
                await stepContext.Context.SendActivityAsync("Er staan geen producten in je lijst");
                return await stepContext.NextAsync();
            }

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

        //if confirmed, store the order in the database
        private async Task<DialogTurnResult> StoreOrderAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if (productList.Count == 0)
            {
                return await stepContext.NextAsync();
            }

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

        //clear the productlist in case this dialog is needed again, and return to the point the MainDailog left off
        private async Task<DialogTurnResult> FinalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            productList.Clear();
            luisResult = null;
            return await stepContext.EndDialogAsync();
        }
    }
}