// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CoreBot.CognitiveModels;
using CoreBot.Dialogs;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Recognizers.Text.DataTypes.TimexExpression;

namespace Microsoft.BotBuilderSamples.Dialogs
{
    public class MainDialog : ComponentDialog
    {
        private readonly WebshopRecognizer _luisRecognizer;
        protected readonly ILogger Logger;

        // Dependency injection uses this constructor to instantiate MainDialog
        public MainDialog(WebshopRecognizer luisRecognizer, ILogger<MainDialog> logger, IConfiguration configuration)
            : base(nameof(MainDialog))
        {
            _luisRecognizer = luisRecognizer;
            Logger = logger;

            AddDialog(new TextPrompt(nameof(TextPrompt)));
            AddDialog(new OrdersDialog(configuration));
            AddDialog(new ProductsDialog());
            AddDialog(new GreetingDialog());
            AddDialog(new NoneDialog());
            AddDialog(new CancelAndHelpDialog(nameof(CancelAndHelpDialog)));
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                IntroStepAsync,
                ActStepAsync,
                FinalStepAsync,
            }));

            // The initial child Dialog to run.
            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> IntroStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if (!_luisRecognizer.IsConfigured)
            {
                await stepContext.Context.SendActivityAsync(
                    MessageFactory.Text("NOTE: LUIS is not configured. To enable all capabilities, add 'LuisAppId', 'LuisAPIKey' and 'LuisAPIHostName' to the appsettings.json file.", inputHint: InputHints.IgnoringInput), cancellationToken);

                return await stepContext.NextAsync(null, cancellationToken);
            }

            // Use the text provided in FinalStepAsync or the default if it is the first time.
            var messageText = stepContext.Options?.ToString() ?? "Wat kan ik voor je doen?";
            var promptMessage = MessageFactory.Text(messageText, messageText, InputHints.ExpectingInput);
            return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = promptMessage }, cancellationToken);
        }

        private async Task<DialogTurnResult> ActStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if (!_luisRecognizer.IsConfigured)
            {
                // LUIS is not configured, feedback to user and Enddialog
                await stepContext.EndDialogAsync();
                return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = MessageFactory.Text("Luis is offline. Wellicht kan ik je op een later moment helpen?") }, cancellationToken);
            }

            // Call LUIS and check the intent of the user. Send them to the corresponding Dialog.
            var luisResult = await _luisRecognizer.RecognizeAsync<LuisHelper>(stepContext.Context, cancellationToken);
            switch (luisResult.TopIntent().intent)
            {
                case LuisHelper.Intent.Orders:
                    return await stepContext.BeginDialogAsync(nameof(OrdersDialog), cancellationToken);
                
                case LuisHelper.Intent.Greeting:
                    return await stepContext.BeginDialogAsync(nameof(GreetingDialog), cancellationToken);

                case LuisHelper.Intent.Products:
                    return await stepContext.BeginDialogAsync(nameof(ProductsDialog), cancellationToken);

                case LuisHelper.Intent.None:
                    return await stepContext.BeginDialogAsync(nameof(NoneDialog), cancellationToken);

                default:
                    // Catch all for unhandled intents
                    var didntUnderstandMessageText = $"Sorry, Ik snap je niet helemaal. Kun je je vraag anders stellen? (intent was {luisResult.TopIntent().intent})";
                    var didntUnderstandMessage = MessageFactory.Text(didntUnderstandMessageText, didntUnderstandMessageText, InputHints.IgnoringInput);
                    await stepContext.Context.SendActivityAsync(didntUnderstandMessage, cancellationToken);
                    break;
            }

            return await stepContext.NextAsync(null, cancellationToken);
        }

        private async Task<DialogTurnResult> FinalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            //Geef hier de juiste feedback om het gesprek af te handelen
           

            // Restart the main dialog with a different message the second time around
            var promptMessage = "Wat kan ik nog meer voor je doen?";
            return await stepContext.ReplaceDialogAsync(InitialDialogId, promptMessage, cancellationToken);
        }
    }
}
