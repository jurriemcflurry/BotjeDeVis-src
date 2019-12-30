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
    public class OrderStatusDialog : CancelAndHelpDialog
    {
        private GremlinHelper gremlinHelper;
        private AuthenticationModel auth;
        private int orderNumber;
        private string status;

        public OrderStatusDialog(IConfiguration configuration) : base(nameof(OrderStatusDialog))
        {
            gremlinHelper = new GremlinHelper(configuration);
            auth = AuthenticationModel.Instance();

            AddDialog(new ConfirmPrompt(nameof(ConfirmPrompt)));
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]{
                GetOrderNumberAsync,
                CheckOrderStatusAsync,
                ActOnOrderStatusAsync,
                FinalStepAsync,
            }));

            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> GetOrderNumberAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if (!auth.GetAuthenticationState())
            {
                await stepContext.Context.SendActivityAsync("Log in om de status van je bestelling te kunnen bekijken.");
                return await stepContext.EndDialogAsync("inloggen");
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

                if(orderNumber != 0)
                {
                    return await stepContext.NextAsync();
                }

                var messageText = "Wat is het ordernummer? Dan ga ik voor je op zoek.";
                var promptMessage = MessageFactory.Text(messageText, messageText, InputHints.ExpectingInput);
                return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = promptMessage }, cancellationToken);
            }        
        }

        private async Task<DialogTurnResult> CheckOrderStatusAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if(orderNumber.Equals(null))
            {
                orderNumber = Int32.Parse((string)stepContext.Result);
            }

            bool allowed = await gremlinHelper.OrderBelongsToUserAsync(orderNumber.ToString());

            if (!allowed)
            {
                await stepContext.Context.SendActivityAsync("De ingelogde gebruiker " + auth.GetLoggedInUser() + " heeft geen order met nummer " + orderNumber.ToString());
                return await stepContext.EndDialogAsync();
            }

            status = await gremlinHelper.GetOrderStatusAsync(orderNumber);
            
            return await stepContext.NextAsync();
        }

        private async Task<DialogTurnResult> ActOnOrderStatusAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if (status.Contains("delivery"))
            {
                await stepContext.Context.SendActivityAsync("De volgende bezorgdatum staat voor je ingepland: " + status);
                return await stepContext.NextAsync();
            }

            switch (status)
            {
                case "awaiting payment":
                    await stepContext.Context.SendActivityAsync("We zijn in afwachting van je betaling voor deze order met nummer " + orderNumber + " .");
                    return await stepContext.PromptAsync(nameof(ChoicePrompt), new PromptOptions
                    {
                        Prompt = MessageFactory.Text("Wil je de bestelling nu betalen?"),
                        RetryPrompt = MessageFactory.Text("Probeer het nog een keer"),
                        Choices = ChoiceFactory.ToChoices(new List<string> { "Ja", "Nee" })
                    }, cancellationToken);
                case "payment received":
                    await stepContext.Context.SendActivityAsync("Je betaling is ontvangen. Je bestelling met nummer " + orderNumber + " wordt gereedgemaakt voor verzending!");
                    return await stepContext.PromptAsync(nameof(ChoicePrompt), new PromptOptions
                    {
                        Prompt = MessageFactory.Text("Wil je nu een bezorgmoment inplannen?"),
                        RetryPrompt = MessageFactory.Text("Probeer het nog een keer"),
                        Choices = ChoiceFactory.ToChoices(new List<string> { "Ja", "Nee" })
                    }, cancellationToken);
                case "order received":
                    await stepContext.Context.SendActivityAsync("Je bestelling met nummer " + orderNumber + " wordt klaargemaakt voor verzending!");
                    return await stepContext.NextAsync();
                case "being delivered":
                    await stepContext.Context.SendActivityAsync("De bestelling met nummer " + orderNumber + " is onderweg!");
                    return await stepContext.NextAsync();
                case "delivered":
                    await stepContext.Context.SendActivityAsync("Je bestelling met nummer " + orderNumber + " is afgeleverd!");
                    return await stepContext.NextAsync();
                default:
                    await stepContext.Context.SendActivityAsync("Er lijkt iets fout te zijn gegaan, ik kan geen informatie over deze order vinden.");
                    return await stepContext.NextAsync();
            }

        }

        private async Task<DialogTurnResult> FinalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if(stepContext.Result != null)
            {
                FoundChoice choice = (FoundChoice)stepContext.Result;

                if (choice.Index == 0 && status.Equals("awaiting payment"))
                {
                    bool orderPaid = await gremlinHelper.PayOrderAsync(orderNumber);

                    if (orderPaid)
                    {
                        await stepContext.Context.SendActivityAsync("Betaling geslaagd! Je bestelling wordt zo snel mogelijk geleverd.");
                    }
                    else
                    {
                        await stepContext.Context.SendActivityAsync("Betaling kon niet worden uitgevoerd. Probeer het later opnieuw.");
                    }
                }
                else if(choice.Index == 0 && status.Equals("payment received"))
                {
                    return await stepContext.EndDialogAsync(orderNumber + " bezorgen");
                }
            }

            return await stepContext.EndDialogAsync();
        }
    }
}
