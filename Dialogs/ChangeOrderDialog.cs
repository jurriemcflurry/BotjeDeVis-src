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
        private List<Product> productList;
        private List<Product> orderProducts;
        private Order order;
        private List<Product> products;

        public ChangeOrderDialog(IConfiguration configuration) : base(nameof(ChangeOrderDialog))
        {
            //gremlinHelper om later snel met de database te kunnen werken
            gremlinHelper = new GremlinHelper(configuration);

            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                CheckForProductList,
                AskForOrderNumber,
                GetProducts,
                ChangeOrder,
                ActChangeOrder,
                AddOrDeleteProductFromOrder,
                FinishDialog,
            }));

            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> CheckForProductList(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            try
            {
                productList = (List<Product>)stepContext.Options;
            }
            catch
            {
                return await stepContext.NextAsync();
            }

            return await stepContext.NextAsync();
        }

        private async Task<DialogTurnResult> AskForOrderNumber(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if(productList != null)
            {
                return await stepContext.NextAsync();
            }

            var messageText = "Wat is je ordernummer?";
            var promptMessage = MessageFactory.Text(messageText, messageText, InputHints.ExpectingInput);
            return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = promptMessage }, cancellationToken);
        }
        
        private async Task<DialogTurnResult> GetProducts(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if (productList != null)
            {
                return await stepContext.NextAsync();
            }

            string stepResult = (string)stepContext.Result;
            int orderNumber = Int32.Parse(stepResult);
            bool orderExists = await gremlinHelper.OrderExistsByNumber(orderNumber);

            if (orderExists)
            {
                orderProducts = await gremlinHelper.GetOrderProducts(orderNumber);

                foreach(Product p in orderProducts)
                {
                    await stepContext.Context.SendActivityAsync(p.GetProductName());
                }
                               
            }
            else
            {
                await stepContext.Context.SendActivityAsync("Sorry, de bestelling met nummer " + orderNumber + " kan niet worden gevonden.");
                return await stepContext.EndDialogAsync();
            }

            order = new Order(orderNumber, orderProducts);

            return await stepContext.NextAsync();
        }

        //hier komt een stap die het volgende doet:
        // als productList gevuld is (komend van de OrderDialog), geef opties met de producten die gewijzigd kunnen worden. Handel dit netjes af. Return de productlijst naar OrderDialog
        // als orderProducts gevuld is (omdat opgehaald hier), geef opties met de producten die gewijzigd kunnen worden. Handel dit af, en sla op naar database

        private async Task<DialogTurnResult> ChangeOrder(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {                      
            return await stepContext.PromptAsync(nameof(ChoicePrompt), new PromptOptions { 
            Prompt = MessageFactory.Text("Wil je een product toevoegen of verwijderen?"),
            Choices = ChoiceFactory.ToChoices(new List<string> { "Toevoegen", "Verwijderen" })
            }, cancellationToken);
        }

        private async Task<DialogTurnResult> ActChangeOrder(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            FoundChoice choice = (FoundChoice)stepContext.Result;
            if(choice.Index == 0)
            {
                //overwegen of dit een nieuwe OrdersDialog wordt, of dat dit hier afgehandeld wordt
                await stepContext.Context.SendActivityAsync("Product toevoegen wordt later geimplementeerd.");
            }
            else if(choice.Index == 1)
            {
                products = order.GetProducts();
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

        private async Task<DialogTurnResult> AddOrDeleteProductFromOrder(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            string whatToDo = (string)stepContext.Result;
            if(whatToDo != "Toevoegen")
            {
                foreach(Product p in products)
                {
                    if (p.GetProductName().Equals(whatToDo))
                    {
                        products.Remove(p);
                        // call aanroepen om order aan te passen -> edge naar dit product moet worden verwijderd
                        await stepContext.Context.SendActivityAsync("Product " + whatToDo + " is verwijderd uit uw bestelling.");
                        return await stepContext.NextAsync();
                    }
                }
            }
            else
            {
                await stepContext.Context.SendActivityAsync("Product toevoegen wordt later geimplementeerd.");
            }

            return await stepContext.NextAsync();
        }     

        private async Task<DialogTurnResult> FinishDialog(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            return await stepContext.EndDialogAsync();
        }
    }
}
