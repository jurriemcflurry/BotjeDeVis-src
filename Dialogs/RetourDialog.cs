using CoreBot.CognitiveModels;
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
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CoreBot.Dialogs
{
    public class RetourDialog : CancelAndHelpDialog
    {
        private GremlinHelper gremlinHelper;
        private AuthenticationModel auth;
        private int orderNumber = 0;
        private string status;
        private Order order;
        private List<Product> products = new List<Product>();
        private List<Product> returnProducts = new List<Product>();
        private List<string> productsString = new List<string>();
        private bool entireOrder;

        public RetourDialog(IConfiguration configuration) : base(nameof(RetourDialog))
        {
            gremlinHelper = new GremlinHelper(configuration);
            auth = AuthenticationModel.Instance();

            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                CheckForLoginAsync,
                CheckForOrderNumberAsync,
                CheckOrderStatusAsync,
                GetOrderProductsAsync,
                ProductsOrOrderRetourAsync,
                SelectReturnProductsAsync,
                ConfirmReturnAsync,
                HandleReturnAsync,
                FinalStepAsync,
            })) ;

            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> CheckForLoginAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if (!auth.GetAuthenticationState())
            {
                await stepContext.Context.SendActivityAsync("Log alsjeblieft in om je zo goed mogelijk te kunnen helpen met de retour.");
                return await stepContext.EndDialogAsync("inloggen");
            }

            return await stepContext.NextAsync();
        }

        private async Task<DialogTurnResult> CheckForOrderNumberAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if(orderNumber != 0)
            {
                return await stepContext.NextAsync();
            }

            LuisHelper luisResult = (LuisHelper)stepContext.Options;
            string request = luisResult.Text;
            bool containsNumber = request.Any(Char.IsDigit);

            if (containsNumber)
            {
                string result = Regex.Match(request, @"\d+").Value;
                orderNumber = Int32.Parse(result);
                return await stepContext.NextAsync();
            }
            else
            {
                orderNumber = await gremlinHelper.GetOrderNumberByPersonAsync();

                if (orderNumber != 0)
                {
                    return await stepContext.NextAsync();
                }

                var messageText = "Wat is het ordernummer van de bestelling?";
                var promptMessage = MessageFactory.Text(messageText, messageText, InputHints.ExpectingInput);
                return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = promptMessage }, cancellationToken);
            }
        }

        private async Task<DialogTurnResult> CheckOrderStatusAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if (orderNumber.Equals(0))
            {
                orderNumber = Int32.Parse((string)stepContext.Result);
            }

            bool allowed = await gremlinHelper.OrderBelongsToUserAsync(orderNumber.ToString());

            if (!allowed)
            {
                await stepContext.Context.SendActivityAsync("De ingelogde gebruiker " + auth.GetLoggedInUser() + " heeft geen order met nummer " + orderNumber.ToString());
                orderNumber = 0;
                return await stepContext.EndDialogAsync();
            }

            status = await gremlinHelper.GetOrderStatusAsync(orderNumber);

            if (status.Equals("delivered"))
            {
                return await stepContext.NextAsync();
            }
            else
            {
                await stepContext.Context.SendActivityAsync("Deze bestelling is nog niet bezorgd, en kan daarom niet worden teruggestuurd.");
                orderNumber = 0;
                return await stepContext.EndDialogAsync();
            }            
        }

        private async Task<DialogTurnResult> GetOrderProductsAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            
                List<Product> orderProducts = await gremlinHelper.GetOrderProductsAsync(orderNumber);
                order = new Order(orderNumber, orderProducts);
                products = order.GetProducts();
            

            if (products.Count().Equals(0))
            {
                await stepContext.Context.SendActivityAsync("Er zijn geen producten in deze bestelling die retour gezonden kunnen worden");

                if(returnProducts.Count() > 0)
                {
                    entireOrder = true;
                    return await stepContext.NextAsync();
                }
                else
                {
                    return await stepContext.EndDialogAsync();
                }             
            }
            else if (products.Count().Equals(1))
            {
                entireOrder = true;
                foreach (Product p in products)
                {
                    returnProducts.Add(p);                   
                }

                products.Clear();
            }
            else
            {
                return await stepContext.PromptAsync(nameof(ChoicePrompt), new PromptOptions
                {
                    Prompt = MessageFactory.Text("Wil je enkele producten retour zenden, of de volledige bestelling?"),
                    Choices = ChoiceFactory.ToChoices(new List<string> { "Producten", "Gehele bestelling" })
                }, cancellationToken);
            }

            return await stepContext.NextAsync();
        }

        private async Task<DialogTurnResult> ProductsOrOrderRetourAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if (entireOrder)
            {
                return await stepContext.NextAsync();
            }

            FoundChoice choice = (FoundChoice)stepContext.Result;

            if(choice.Index == 0)
            {
                return await stepContext.PromptAsync(nameof(ChoicePrompt), new PromptOptions
                {
                    Prompt = MessageFactory.Text("Welk product wil je terugsturen?"),
                    Choices = ChoiceFactory.ToChoices(productsString)
                }, cancellationToken);
            }
            else if(choice.Index == 1)
            {
                entireOrder = true;

                foreach(Product p in products)
                {
                    returnProducts.Add(p);
                    products.Remove(p);
                }
            }

            return await stepContext.NextAsync();
        }

        private async Task<DialogTurnResult> SelectReturnProductsAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if (entireOrder)
            {
                return await stepContext.NextAsync();
            }

            FoundChoice choice = (FoundChoice)stepContext.Result;
            string productFound = choice.Value.ToString();

            foreach (Product p in products)
            {
                if (p.GetProductName().Equals(productFound))
                {
                    returnProducts.Add(p);
                    products.Remove(p);                
                }
            }

            return await stepContext.PromptAsync(nameof(ChoicePrompt), new PromptOptions
            {
                Prompt = MessageFactory.Text("Wat wil je nog meer doen?"),
                Choices = ChoiceFactory.ToChoices(new List<string> { "Retourproduct toevoegen", "Retour inplannen" })
            }, cancellationToken);
        }

        private async Task<DialogTurnResult> ConfirmReturnAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if (entireOrder)
            {
                return await stepContext.PromptAsync(nameof(ChoicePrompt), new PromptOptions
                {
                    Prompt = MessageFactory.Text("Bevestig dat je de bestelling met nummer " + orderNumber + " retour wil zenden."),
                    Choices = ChoiceFactory.ToChoices(new List<string> { "Retour bevestigen", "Annuleren" })
                }, cancellationToken);
            }

            // else selected to add another product, or to confirm
            FoundChoice choice = (FoundChoice)stepContext.Result;

            if(choice.Index == 0)
            {
                return await stepContext.ReplaceDialogAsync(nameof(RetourDialog));
            }
            else
            {
                //confirm return of selected products
                return await stepContext.PromptAsync(nameof(ChoicePrompt), new PromptOptions
                {
                    Prompt = MessageFactory.Text("Bevestig dat je deze producten retour wil zenden: " + returnProducts.ToString()), //nagaan of deze weergave prettig is
                    Choices = ChoiceFactory.ToChoices(new List<string> { "Retour bevestigen", "Annuleren" })
                }, cancellationToken);
            }
        }

        private async Task<DialogTurnResult> HandleReturnAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            FoundChoice choice = (FoundChoice)stepContext.Result;

            if(choice.Index == 0)
            {
                Random rnd = new Random();
                int returnOrderNumber = rnd.Next(1, 99999);

                while (await gremlinHelper.ReturnOrderExistsByNumberAsync(returnOrderNumber))
                {
                    returnOrderNumber = rnd.Next(1, 99999);
                }

                Order returnOrder = new Order(returnOrderNumber, returnProducts);

                bool returnOrderCreated = await gremlinHelper.CreateReturnOrderAsync(returnOrder, order);

                if (returnOrderCreated)
                {
                    await stepContext.Context.SendActivityAsync("De retourzending is aangemeld. De retourlabel is via de mail verzonden!");
                    return await stepContext.NextAsync();
                }
                else
                {
                    await stepContext.Context.SendActivityAsync("De retour kon niet worden geplaatst. Probeer het later opnieuw.");
                    return await stepContext.EndDialogAsync();
                }
            }
            else
            {
                return await stepContext.EndDialogAsync();
            }

            

            
        }

        private async Task<DialogTurnResult> FinalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            orderNumber = 0;
            products.Clear();
            returnProducts.Clear();
            return await stepContext.EndDialogAsync();
        }
    }
}
