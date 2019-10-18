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
    public class AddProductsToOrderDialog : CancelAndHelpDialog
    {
        private GremlinHelper gremlinHelper;
        private List<Product> productList = new List<Product>();
        private bool toevoegen;
        private string productListString = "Producten: ";

        public AddProductsToOrderDialog(IConfiguration configuration) : base(nameof(AddProductsToOrderDialog))
        {
            this.gremlinHelper = new GremlinHelper(configuration);

            AddDialog(new TextPrompt(nameof(TextPrompt)));
            AddDialog(new ChoicePrompt(nameof(ChoicePrompt)));
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                AskForProductsAsync,
                HandleChoiceAsync,
                UpdateProductListAsync,
            }));

            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> AskForProductsAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            productList = (List<Product>)stepContext.Options;           

            //prompt springt opeens terug naar parentdialog, ipv naar volgende waterfallstep?
            //probleem treedt alleen op met choiceprompt, niet met bijv textprompt
            // wellicht VS update
            return await stepContext.PromptAsync(nameof(ChoicePrompt), new PromptOptions
            {
                Prompt = MessageFactory.Text("Wil je nog een product toevoegen of verwijderen?"),
                RetryPrompt = MessageFactory.Text("Probeer het nog een keer"),
                Choices = ChoiceFactory.ToChoices(new List<string> { "Toevoegen", "Verwijderen", "Klaar met bestellen" })
            }, cancellationToken);
        }

        private async Task<DialogTurnResult> HandleChoiceAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            FoundChoice choice = (FoundChoice)stepContext.Result;
            switch (choice.Index)
            {
                case 0:
                    toevoegen = true;
                    var messageText = "Welk product wil je toevoegen?";
                    var promptMessage = MessageFactory.Text(messageText, messageText, InputHints.ExpectingInput);
                    return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = promptMessage }, cancellationToken);
                case 1:
                    toevoegen = false;
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
                    return await stepContext.EndDialogAsync(productList);
                default:
                    break;

            }

            return await stepContext.NextAsync();
        }

        private async Task<DialogTurnResult> UpdateProductListAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if (toevoegen)
            {
                string productName = (string)stepContext.Result;
                Product product = new Product(productName); //hele string wordt gepakt als product, kijken om te verbeteren (nog een Luis-call om entiteit te filteren)

                bool productExists = await gremlinHelper.ProductExistsAsync(product);

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
            }
            else if (!toevoegen)
            {
                FoundChoice choice = (FoundChoice)stepContext.Result;
                string productFound = choice.Value.ToString();

                foreach (Product p in productList)
                {
                    if (p.GetProductName().Equals(productFound))
                    {
                        productList.Remove(p);
                        await stepContext.Context.SendActivityAsync("Product " + productFound + " is verwijderd uit uw bestelling.");
                    }
                }
            }

            await stepContext.Context.SendActivityAsync("De volgende producten staan nu in je bestelling:");
            foreach(Product p in productList)
            {
                productListString += p.GetProductName() + ", ";
            }
            productListString = productListString.Remove(productListString.Length - 2);
            await stepContext.Context.SendActivityAsync(productListString);
            return await stepContext.ReplaceDialogAsync(nameof(AddProductsToOrderDialog), productList, cancellationToken);
        }
    }
}
