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
    public class RepairDialog : CancelAndHelpDialog
    {
        private GremlinHelper gremlinHelper;
        private AuthenticationModel auth;
        private LuisHelper luisResult;
        private int orderNumber = 0;
        private List<string> productsString = new List<string>();
        private Product productToRepair;
        private int daysToRepair;

        public RepairDialog(IConfiguration configuration) : base(nameof(RepairDialog))
        {
            gremlinHelper = new GremlinHelper(configuration);
            auth = AuthenticationModel.Instance();

            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                CheckIfLoggedInAsync,
                CheckForOrderNumberOrProductAsync,
                AskForOrderNumberAsync,
                CheckIfOrderFromLoggedInPersonAsync,
                AskForProductToRepairAsync,
                ScheduleRepairAsync,
                ConfirmRepairAsync,
                StoreRepairAsync,
                FinalStepAsync,
            }));

            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> CheckIfLoggedInAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if (!auth.GetAuthenticationState())
            {
                await stepContext.Context.SendActivityAsync("Log alstublieft in om een reparatie in te plannen.");
                return await stepContext.EndDialogAsync("inloggen");
            }

            luisResult = (LuisHelper)stepContext.Options;
            return await stepContext.NextAsync();
        }

        private async Task<DialogTurnResult> CheckForOrderNumberOrProductAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
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
            if(orderNumber != 0)
            {
                return await stepContext.NextAsync();
            }
            else
            {
                orderNumber = await gremlinHelper.GetOrderNumberByPersonAsync();

                if (orderNumber != 0)
                {
                    return await stepContext.NextAsync();
                }

                var messageText = "Wat is het ordernummer? Dan ga ik voor je op zoek.";
                var promptMessage = MessageFactory.Text(messageText, messageText, InputHints.ExpectingInput);
                return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = promptMessage }, cancellationToken);
            }
        }

        private async Task<DialogTurnResult> CheckIfOrderFromLoggedInPersonAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if(orderNumber == 0)
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
                await stepContext.Context.SendActivityAsync("Ik heb geen bestelling gevonden met nummer " + orderNumber + ". Probeer opnieuw als je een product wil laten repareren.");
                orderNumber = 0;
                return await stepContext.EndDialogAsync();
            }

            bool orderIsFromUser = await gremlinHelper.OrderBelongsToUserAsync(orderNumber.ToString());

            if (!orderIsFromUser)
            {
                await stepContext.Context.SendActivityAsync("Deze bestelling is niet van " + auth.GetLoggedInUser() + ". Log in met het account waarmee de bestelling is gedaan om de reparatie in te plannen.");
                orderNumber = 0;
                return await stepContext.EndDialogAsync();
            }

            string orderStatus = await gremlinHelper.GetOrderStatusAsync(orderNumber);

            if (!orderStatus.Equals("delivered"))
            {
                await stepContext.Context.SendActivityAsync("Deze bestelling is nog niet afgeleverd, de producten komen daarom nog niet in aanmerking voor reparatie. Als u de producten wel hebt ontvangen, probeer het dan later opnieuw.");
                return await stepContext.EndDialogAsync();
            }

            return await stepContext.NextAsync();
        }

        private async Task<DialogTurnResult> AskForProductToRepairAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {                  
            List<Product> productList = await gremlinHelper.GetOrderProductsAsync(orderNumber);

            foreach (Product p in productList)
            {
                bool repairable = await gremlinHelper.CheckIfProductIsRepairableAsync(p);

                if (repairable)
                {
                    productsString.Add(p.GetProductName());
                }               
            }

            if(productsString.Count() < 1)
            {
                await stepContext.Context.SendActivityAsync("Deze bestelling bevat geen producten die in aanmerking komen voor reparatie. Je kan wel controleren of het product nog in aanmerking komt voor de garantieregeling.");
                return await stepContext.EndDialogAsync();
            }

            await stepContext.Context.SendActivityAsync("Ik heb nog geen producten herkend in je verzoek.");

            return await stepContext.PromptAsync(nameof(ChoicePrompt), new PromptOptions
            {
                Prompt = MessageFactory.Text("Welk product wil je laten repareren?"),
                Choices = ChoiceFactory.ToChoices(productsString)
            }, cancellationToken);
        }

        private async Task<DialogTurnResult> ScheduleRepairAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            FoundChoice choice = (FoundChoice)stepContext.Result;
            string productFound = choice.Value.ToString();
            productToRepair = new Product(productFound);

            return await stepContext.PromptAsync(nameof(ChoicePrompt), new PromptOptions
            {
                Prompt = MessageFactory.Text("De reparatie van " + productToRepair.GetProductName() + " kan op de volgende momenten worden ingepland (kies een optie):"),
                RetryPrompt = MessageFactory.Text("Probeer het nog een keer"),
                Choices = ChoiceFactory.ToChoices(new List<string> { "Morgen", "Overmorgen", "Over 2 dagen", "Annuleren" })
            }, cancellationToken);
        }

        private async Task<DialogTurnResult> ConfirmRepairAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            FoundChoice choice = (FoundChoice)stepContext.Result;

            if (choice.Index != 3)
            {
                if (choice.Value.Equals("Morgen"))
                {
                    daysToRepair = 1;
                }
                else if (choice.Value.Equals("Overmorgen"))
                {
                    daysToRepair = 2;
                }
                else
                {
                    daysToRepair = 3;
                }
            }
            else
            {
                await stepContext.Context.SendActivityAsync("Geen optie geselecteerd. De reparatie wordt geannuleerd.");
                return await stepContext.EndDialogAsync();
            }

            return await stepContext.PromptAsync(nameof(ChoicePrompt), new PromptOptions
            {
                Prompt = MessageFactory.Text("De reparatie zal plaatsvinden over " + daysToRepair + ". Onze reparatiedienst zal contact opnemen om een tijdstip af te spreken. Bevestig hieronder de reparatie."),
                RetryPrompt = MessageFactory.Text("Probeer het nog een keer"),
                Choices = ChoiceFactory.ToChoices(new List<string> { "Bevestigen", "Annuleren" })
            }, cancellationToken);
        }

        private async Task<DialogTurnResult> StoreRepairAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            FoundChoice choice = (FoundChoice)stepContext.Result;

            if(choice.Index == 0)
            {
                bool repairStored = await gremlinHelper.StoreRepairAsync(productToRepair, daysToRepair);

                if (repairStored)
                {
                    await stepContext.Context.SendActivityAsync("Reparatie succesvol ingepland!");
                }
                else
                {
                    await stepContext.Context.SendActivityAsync("Het lukte helaas niet om de reparatie in te plannen. Probeer het later opnieuw");
                }
            }
            else
            {
                await stepContext.Context.SendActivityAsync("De reparatie is niet bevestigd, en zal niet worden ingepland.");
                return await stepContext.EndDialogAsync();
            }

            return await stepContext.NextAsync();
        }

        private async Task<DialogTurnResult> FinalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            orderNumber = 0;
            productsString.Clear();
            return await stepContext.EndDialogAsync();
        }
    }
}
