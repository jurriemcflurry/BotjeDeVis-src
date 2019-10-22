using CoreBot.Database;
using CoreBot.Models;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Bot.Schema;
using Microsoft.BotBuilderSamples.Dialogs;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CoreBot.Dialogs
{
    public class ChangeOrderDialog : CancelAndHelpDialog
    {
        private GremlinHelper gremlinHelper;
        private List<Product> orderProducts;
        private Order order;
        private List<Product> products;
        private string whatToDo;
        private string productListString = "Producten: ";
        private Product product;
        private string productFound;

        public ChangeOrderDialog(IConfiguration configuration) : base(nameof(ChangeOrderDialog))
        {
            //gremlinHelper om later snel met de database te kunnen werken
            gremlinHelper = new GremlinHelper(configuration);

            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                AskForOrderNumberAsync,
                GetProductsAsync,
                ChangeOrderAsync,
                ActChangeOrderAsync,
                AddOrDeleteProductFromOrderAsync,
                FinishDialogAsync,
            }));

            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> AskForOrderNumberAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var messageText = "Wat is je ordernummer?";
            var promptMessage = MessageFactory.Text(messageText, messageText, InputHints.ExpectingInput);
            return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = promptMessage }, cancellationToken);
        }
        
        private async Task<DialogTurnResult> GetProductsAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            string stepResult = (string)stepContext.Result;
            int orderNumber = Int32.Parse(stepResult);
            bool orderExists = await gremlinHelper.OrderExistsByNumberAsync(orderNumber);

            if (orderExists)
            {
                orderProducts = await gremlinHelper.GetOrderProductsAsync(orderNumber);                              
            }
            else
            {
                await stepContext.Context.SendActivityAsync("Sorry, de bestelling met nummer " + orderNumber + " kan niet worden gevonden.");
                return await stepContext.EndDialogAsync();
            }

            order = new Order(orderNumber, orderProducts);

            return await stepContext.NextAsync();
        }

        private async Task<DialogTurnResult> ChangeOrderAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {                      
            return await stepContext.PromptAsync(nameof(ChoicePrompt), new PromptOptions { 
            Prompt = MessageFactory.Text("Wil je een product toevoegen of verwijderen?"),
            Choices = ChoiceFactory.ToChoices(new List<string> { "Toevoegen", "Verwijderen" })
            }, cancellationToken);
        }

        private async Task<DialogTurnResult> ActChangeOrderAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            FoundChoice choice = (FoundChoice)stepContext.Result;
            whatToDo = choice.Value.ToString();

            products = order.GetProducts();

            if (choice.Index == 0)
            {
                var messageText = "Wat wil je bestellen?";
                var promptMessage = MessageFactory.Text(messageText, messageText, InputHints.ExpectingInput);
                return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = promptMessage }, cancellationToken);
            }
            else if(choice.Index == 1)
            {             
                List<string> productsString = new List<string>();

                foreach (Product p in products)
                {
                    productsString.Add(p.GetProductName());
                }

                return await stepContext.PromptAsync(nameof(ChoicePrompt), new PromptOptions
                {
                    Prompt = MessageFactory.Text("Welk product wil je verwijderen?"),
                    Choices = ChoiceFactory.ToChoices(productsString)
                }, cancellationToken);
            }

            return await stepContext.NextAsync();
        }

        private async Task<DialogTurnResult> AddOrDeleteProductFromOrderAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {           
            if (whatToDo != "Toevoegen")
            {
                FoundChoice choice = (FoundChoice)stepContext.Result;
                productFound = choice.Value.ToString();

                foreach (Product p in products)
                {
                    if (p.GetProductName().Equals(productFound))
                    {
                        products.Remove(p);
                        await gremlinHelper.RemoveProductFromOrderAsync(order, p);
                        await stepContext.Context.SendActivityAsync("Product " + productFound + " is verwijderd uit uw bestelling.");
                        return await stepContext.NextAsync();
                    }
                }
            }
            else
            {
                productFound = (string)stepContext.Result;
                product = new Product(productFound);
                bool productExists = await gremlinHelper.ProductExistsAsync(product);

                if (productExists)
                {
                    products.Add(product);
                    await stepContext.Context.SendActivityAsync("De volgende producten staan nu in je bestelling:");
                    foreach (Product p in products)
                    {
                        productListString += p.GetProductName() + ", ";
                    }
                    productListString = productListString.Remove(productListString.Length - 2);
                    await stepContext.Context.SendActivityAsync(productListString);

                    return await stepContext.PromptAsync(nameof(ChoicePrompt), new PromptOptions
                    {
                        Prompt = MessageFactory.Text("Bevestig de wijziging in de bestelling:"),
                        RetryPrompt = MessageFactory.Text("Probeer het nog een keer"),
                        Choices = ChoiceFactory.ToChoices(new List<string> { "Bevestigen", "Annuleren" })
                    }, cancellationToken);
                    
                }
                else
                {
                    //zo nee, helaas niet in assortiment
                    await stepContext.Context.SendActivityAsync("Product " + product.GetProductName() + " is helaas niet in ons assortiment.");
                }
            }

            return await stepContext.NextAsync();
        }     

        private async Task<DialogTurnResult> FinishDialogAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            FoundChoice choice = (FoundChoice)stepContext.Result;
            if(choice.Index == 0)
            {
                try
                {
                    await gremlinHelper.AddProductToOrderAsync(order, product);
                    await stepContext.Context.SendActivityAsync("Bestelling geslaagd! Nogmaals bedankt voor het shoppen bij ons!");
                }
                catch
                {
                    await stepContext.Context.SendActivityAsync("Het lukte helaas niet om je bestelling uit te voeren. Probeer het later opnieuw.");
                }               
            }

            return await stepContext.EndDialogAsync();
        }
    }
}
