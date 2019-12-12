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
    public class ComplaintDialog : CancelAndHelpDialog
    {
        private LuisHelper luisResult;
        private GremlinHelper gremlinHelper;
        private AuthenticationModel auth;
        private int orderNumber;
        private string klacht;
        private bool orderExists;

        public ComplaintDialog(IConfiguration configuration) : base(nameof(ComplaintDialog))
        {
            gremlinHelper = new GremlinHelper(configuration);
            auth = AuthenticationModel.Instance();

            AddDialog(new ChoicePrompt(nameof(ChoicePrompt)));
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                GetOrderNumberAsync,
                RequestComplaintSubjectAsync,
                HandleComplaintAsync,
                StoreComplaintAsync,
                FinalStepAsync,
            }));

            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> GetOrderNumberAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if (!auth.GetAuthenticationState())
            {
                await stepContext.Context.SendActivityAsync("Log alstublieft in zodat we uw klacht zo goed mogelijk kunnen oplossen.");
                return await stepContext.EndDialogAsync("inloggen");
            }

            luisResult = (LuisHelper)stepContext.Options;
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

                var messageText = "Wat is het ordernummer van de bestelling waar uw klacht over gaat?";
                var promptMessage = MessageFactory.Text(messageText, messageText, InputHints.ExpectingInput);
                return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = promptMessage }, cancellationToken);
            }
        }

        private async Task<DialogTurnResult> RequestComplaintSubjectAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if (orderNumber.Equals(null))
            {
                orderNumber = Int32.Parse((string)stepContext.Result);
            }

            orderExists = await gremlinHelper.OrderExistsByNumberAsync(orderNumber);
            if (orderExists)
            {
                bool allowed = await gremlinHelper.OrderBelongsToUserAsync(orderNumber.ToString());

                if (!allowed)
                {
                    await stepContext.Context.SendActivityAsync("De ingelogde gebruiker " + auth.GetLoggedInUser() + " heeft geen order met nummer " + orderNumber.ToString());
                    orderNumber = 0;
                    return await stepContext.EndDialogAsync();
                }
            }
            else
            {
                await stepContext.Context.SendActivityAsync("Ik kan geen order vinden met nummer " + orderNumber.ToString() + ". Probeer het aub opnieuw.");
                orderNumber = 0;
                return await stepContext.ReplaceDialogAsync(nameof(ComplaintDialog), cancellationToken);
            }

            var messageText = "Wat is uw klacht?";
            var promptMessage = MessageFactory.Text(messageText, messageText, InputHints.ExpectingInput);
            return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = promptMessage }, cancellationToken);
        }

        private async Task<DialogTurnResult> HandleComplaintAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            klacht = (string)stepContext.Result;

            await stepContext.Context.SendActivityAsync("Voor de bestelling met nummer " + orderNumber + " heb ik de volgende klacht begrepen:");
            await stepContext.Context.SendActivityAsync(klacht);
            return await stepContext.NextAsync();
        }

        private async Task<DialogTurnResult> StoreComplaintAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            bool complaintStored = await gremlinHelper.StoreComplaintAsync(orderNumber, klacht);

            if (complaintStored)
            {
                await stepContext.Context.SendActivityAsync("Uw klacht is succesvol opgeslagen. Er zal zo snel mogelijk een menselijke collega naar kijken en contact met u opnemen.");
                return await stepContext.NextAsync();
            }
            else
            {
                await stepContext.Context.SendActivityAsync("Het versturen van uw klacht is helaas niet gelukt. Dit is doorgegeven aan een van mijn menselijk collega's, die zo spoedig mogelijk contact met u zullen opnemen.");
                return await stepContext.EndDialogAsync();
            }
            
        }

        private async Task<DialogTurnResult> FinalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            klacht = "";            
            return await stepContext.EndDialogAsync();
        }
    }
}
