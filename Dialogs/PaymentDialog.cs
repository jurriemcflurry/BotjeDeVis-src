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
    public class PaymentDialog : CancelAndHelpDialog
    {
        private GremlinHelper gremlinHelper;
        private AuthenticationModel auth;
        private int orderNumber = 0;
        private string status;
        private string request;
        private bool paymentSuccessful;

        public PaymentDialog(IConfiguration configuration) : base(nameof(PaymentDialog))
        {
            gremlinHelper = new GremlinHelper(configuration);
            auth = AuthenticationModel.Instance();

            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                GetOrderNumberAsync,
                CheckIfOrderExistsAsync,
                CheckIfOrderNeedsPaymentAsync,
                ConfirmPaymentAsync,
                ActOnPaymentAsync,
                FinalStepAsync,
            }));
        }

        private async Task<DialogTurnResult> GetOrderNumberAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            string type = stepContext.Options.GetType().ToString();

            if (type.Equals("CoreBot.CognitiveModels.LuisHelper"))
            {
                LuisHelper luisResult = (LuisHelper)stepContext.Options;
                request = luisResult.Text;               
            }
            else if (type.Equals("System.String"))
            {
                request = (string)stepContext.Options;
            }

            bool containsNumber = request.Any(Char.IsDigit);

            if (containsNumber)
            {
                string result = Regex.Match(request, @"\d+").Value;
                orderNumber = Int32.Parse(result);
                return await stepContext.NextAsync();
            }
            else
            {
                var messageText = "Wat is het ordernummer? Dan ga ik voor je op zoek.";
                var promptMessage = MessageFactory.Text(messageText, messageText, InputHints.ExpectingInput);
                return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = promptMessage }, cancellationToken);
            }
        }

        private async Task<DialogTurnResult> CheckIfOrderExistsAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if (orderNumber.Equals(0))
            {
                orderNumber = Int32.Parse((string)stepContext.Result);
            }

            bool allowed = await gremlinHelper.OrderBelongsToUserAsync(orderNumber.ToString());

            if (!allowed)
            {
                await stepContext.Context.SendActivityAsync("De ingelogde gebruiker " + auth.GetLoggedInUser() + " heeft geen order met nummer " + orderNumber.ToString());
                return await stepContext.EndDialogAsync();
            }

            bool orderExists = await gremlinHelper.OrderExistsByNumberAsync(orderNumber);

            if (orderExists)
            {
                return await stepContext.NextAsync();
            }
            else
            {
                await stepContext.Context.SendActivityAsync("Er is geen order gevonden met ordernummer " + orderNumber + ". Probeer opnieuw.");
                return await stepContext.ReplaceDialogAsync(nameof(PaymentDialog));
            }
        }

        private async Task<DialogTurnResult> CheckIfOrderNeedsPaymentAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            status = await gremlinHelper.GetOrderStatusAsync(orderNumber);

            if(status.Equals("awaiting payment") || status.Equals("partially paid"))
            {
                await stepContext.Context.SendActivityAsync("Deze bestelling is in afwachting van je betaling.");
                return await stepContext.PromptAsync(nameof(ChoicePrompt), new PromptOptions
                {
                    Prompt = MessageFactory.Text("Wil je de bestelling nu betalen?"),
                    RetryPrompt = MessageFactory.Text("Probeer het nog een keer"),
                    Choices = ChoiceFactory.ToChoices(new List<string> { "Betalen", "Annuleren" })
                }, cancellationToken);
            }
            else
            {
                await stepContext.Context.SendActivityAsync("Deze bestelling is niet in afwachting van een betaling.");
                await stepContext.Context.SendActivityAsync("De status van deze bestelling is: " + status);
                return await stepContext.EndDialogAsync();
            }
        }

        private async Task<DialogTurnResult> ConfirmPaymentAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            return await stepContext.PromptAsync(nameof(ChoicePrompt), new PromptOptions
            {
                Prompt = MessageFactory.Text("Bevestig de betaling van order " + orderNumber),
                RetryPrompt = MessageFactory.Text("Probeer het nog een keer"),
                Choices = ChoiceFactory.ToChoices(new List<string> { "Bevestigen", "Annuleren" })
            }, cancellationToken);
        }

        private async Task<DialogTurnResult> ActOnPaymentAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            FoundChoice choice = (FoundChoice)stepContext.Result;

            if (choice.Index == 0)
            {
                paymentSuccessful = await gremlinHelper.PayOrderAsync(orderNumber);

                if (paymentSuccessful)
                {
                    await stepContext.Context.SendActivityAsync("Betaling ontvangen! Je bestelling zal zo snel mogelijk worden geleverd.");
                }
                else
                {
                    await stepContext.Context.SendActivityAsync("Het lukte helaas niet om de betaling uit te voeren. Probeer het later opnieuw. (Ordernummer is " + orderNumber + ")");
                }

                return await stepContext.NextAsync();
            }
            else
            {
                await stepContext.Context.SendActivityAsync("Betaling geannuleerd. Ordernummer is " + orderNumber);
            }

            return await stepContext.NextAsync();
        }

        private async Task<DialogTurnResult> FinalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {

            if (paymentSuccessful)
            {
                return await stepContext.EndDialogAsync(orderNumber + " laten bezorgen");
            }

            orderNumber = 0;
            return await stepContext.EndDialogAsync();
        }
    }
}
