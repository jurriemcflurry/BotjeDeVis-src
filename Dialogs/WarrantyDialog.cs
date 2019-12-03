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
    public class WarrantyDialog : CancelAndHelpDialog
    {
        private GremlinHelper gremlinHelper;
        private AuthenticationModel auth;
        private LuisHelper luisResult;
        private int orderNumber = 0;
        private List<string> productsString = new List<string>();
        private Product warrantyProduct;
        private List<Product> productList;
        private List<Product> returnProducts;
        private DateTime twoYearsAgo = DateTime.Today.AddYears(-2);

        public WarrantyDialog(IConfiguration configuration) : base(nameof(WarrantyDialog))
        {
            gremlinHelper = new GremlinHelper(configuration);
            auth = AuthenticationModel.Instance();

            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                CheckForLoginAsync,
                CheckForOrderNumberAsync,
                AskForOrderNumberAsync,
                CheckIfOrderFromLoggedInPersonAsync,
                AskForWarrantyProductAsync,
                CheckIfProductHasWarrantyAsync,
                GiveWarrantyOptionsAsync,
                ConfirmWarrantyOptionAsync,
                FinalStepAsync,
            }));

            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> CheckForLoginAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if (!auth.GetAuthenticationState())
            {
                await stepContext.Context.SendActivityAsync("Log alstublieft in om een reparatie in te plannen.");
                return await stepContext.EndDialogAsync("inloggen");
            }

            luisResult = (LuisHelper)stepContext.Options;
            return await stepContext.NextAsync();
        }

        private async Task<DialogTurnResult> CheckForOrderNumberAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            string request = luisResult.Text;
            bool containsNumber = request.Any(Char.IsDigit);

            if (containsNumber)
            {
                string result = Regex.Match(request, @"\d+").Value;
                orderNumber = Int32.Parse(result);
            }

            return await stepContext.NextAsync();
        }

        private async Task<DialogTurnResult> AskForOrderNumberAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if (orderNumber != 0)
            {
                return await stepContext.NextAsync();
            }
            else
            {
                var messageText = "Wat is het ordernummer? Dan ga ik voor je op zoek.";
                var promptMessage = MessageFactory.Text(messageText, messageText, InputHints.ExpectingInput);
                return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = promptMessage }, cancellationToken);
            }
        }

        private async Task<DialogTurnResult> CheckIfOrderFromLoggedInPersonAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if (orderNumber == 0)
            {
                string result = (string)stepContext.Result;

                if (result.Any(Char.IsDigit))
                {
                    string resultNumbers = Regex.Match(result, @"\d+").Value;
                    orderNumber = Int32.Parse(result);
                }
                else
                {
                    await stepContext.Context.SendActivityAsync("Je antwoord bevat geen ordernummer. Probeer het opnieuw.");
                    return await stepContext.ReplaceDialogAsync(nameof(RepairDialog), luisResult, cancellationToken);
                }

            }

            bool orderExists = await gremlinHelper.OrderExistsByNumberAsync(orderNumber);

            if (!orderExists)
            {
                await stepContext.Context.SendActivityAsync("Ik heb geen bestelling gevonden met nummer " + orderNumber + ". Probeer opnieuw als je gebruik wil maken van de garantieregeling.");
                orderNumber = 0;
                return await stepContext.EndDialogAsync();
            }

            bool orderIsFromUser = await gremlinHelper.OrderBelongsToUserAsync(orderNumber.ToString());

            if (!orderIsFromUser)
            {
                await stepContext.Context.SendActivityAsync("Deze bestelling is niet van " + auth.GetLoggedInUser() + ". Log in met het account waarmee de bestelling is gedaan om gebruik te maken van de garantieregeling.");
                orderNumber = 0;
                return await stepContext.EndDialogAsync();
            }

            string orderStatus = await gremlinHelper.GetOrderStatusAsync(orderNumber);

            if (!orderStatus.Equals("delivered"))
            {
                await stepContext.Context.SendActivityAsync("Deze bestelling is nog niet afgeleverd, de producten komen daarom nog niet in aanmerking voor garantie. Als u de producten wel hebt ontvangen, probeer het dan later opnieuw.");
                return await stepContext.EndDialogAsync();
            }

            return await stepContext.NextAsync();
        }

        private async Task<DialogTurnResult> AskForWarrantyProductAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            productList = await gremlinHelper.GetOrderProductsAsync(orderNumber);
            

            foreach (Product p in productList)
            {
                productsString.Add(p.GetProductName());
            }

            if (productsString.Count() < 1)
            {
                await stepContext.Context.SendActivityAsync("Deze bestelling bevat geen producten die in aanmerking komen voor garantie.");
                return await stepContext.EndDialogAsync();
            }

            await stepContext.Context.SendActivityAsync("Ik heb nog geen producten herkend in je verzoek.");

            return await stepContext.PromptAsync(nameof(ChoicePrompt), new PromptOptions
            {
                Prompt = MessageFactory.Text("Voor welk product wil je gebruik maken van de garantieregeling?"),
                Choices = ChoiceFactory.ToChoices(productsString)
            }, cancellationToken);
        }

        private async Task<DialogTurnResult> CheckIfProductHasWarrantyAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            FoundChoice choice = (FoundChoice)stepContext.Result;
            string productFound = choice.Value.ToString();
            warrantyProduct = new Product(productFound);

            //haal de orderdatum op (format is dd/mm/yyyy)
            string orderDatum = await gremlinHelper.GetProductWarrantyAsync(orderNumber, warrantyProduct);
            DateTime orderDate = Convert.ToDateTime(orderDatum);

            if(orderDate > twoYearsAgo)
            {
                await stepContext.Context.SendActivityAsync("Dit product valt binnen de garantietermijn van 2 jaar.");
            }
            else
            {
                await stepContext.Context.SendActivityAsync("Dit product valt helaas buiten de garantietermijn van 2 jaar.");
                orderNumber = 0;
                return await stepContext.EndDialogAsync();
            }

            return await stepContext.NextAsync();
        }

        private async Task<DialogTurnResult> GiveWarrantyOptionsAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            return await stepContext.PromptAsync(nameof(ChoicePrompt), new PromptOptions
            {
                Prompt = MessageFactory.Text("Bevestig dat u gebruik wil maken van de garantieregeling voor " + warrantyProduct.GetProductName()),
                RetryPrompt = MessageFactory.Text("Probeer het nog een keer"),
                Choices = ChoiceFactory.ToChoices(new List<string> { "Gebruik maken van garantie", "Annuleren" })
            }, cancellationToken);
        }

        private async Task<DialogTurnResult> ConfirmWarrantyOptionAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            FoundChoice choice = (FoundChoice)stepContext.Result;

            if(choice.Index == 0)
            {
                Order warrantyOrder = new Order(orderNumber, productList);
                await gremlinHelper.RemoveProductFromOrderAsync(warrantyOrder, warrantyProduct);
                await stepContext.Context.SendActivityAsync("De kosten voor dit product worden binnen enkele werkdagen op uw rekening teruggestort, zodra uw retour bij ons is ontvangen.");

                Random rnd = new Random();
                int returnOrderNumber = rnd.Next(1, 99999);

                while (await gremlinHelper.ReturnOrderExistsByNumberAsync(returnOrderNumber))
                {
                    returnOrderNumber = rnd.Next(1, 99999);
                }

                returnProducts.Add(warrantyProduct);

                Order returnOrder = new Order(returnOrderNumber, returnProducts);

                bool returnOrderCreated = await gremlinHelper.CreateReturnOrderAsync(returnOrder, warrantyOrder);

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
                await stepContext.Context.SendActivityAsync("Garantiemelding geannuleerd.");
                orderNumber = 0;
                return await stepContext.EndDialogAsync();
            }
        }

        private async Task<DialogTurnResult> FinalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            orderNumber = 0;
            productList.Clear();
            returnProducts.Clear();
            return await stepContext.EndDialogAsync();
        }
    }
}
