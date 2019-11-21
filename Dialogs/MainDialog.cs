using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CoreBot.CognitiveModels;
using CoreBot.ConversationState;
using CoreBot.Dialogs;
using CoreBot.Models;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Microsoft.BotBuilderSamples.Dialogs
{
    public class MainDialog : ComponentDialog
    {
        protected IStatePropertyAccessor<AuthenticationState> _authenticationState;
        private readonly WebshopRecognizer _luisRecognizer;
        protected readonly ILogger Logger;
        private LuisHelper luisResult;
        private AuthenticationModel auth;

        // Dependency injection uses this constructor to instantiate MainDialog
        public MainDialog(WebshopRecognizer luisRecognizer, ILogger<MainDialog> logger, IConfiguration configuration, ConversationState state)
            : base(nameof(MainDialog))
        {
            _luisRecognizer = luisRecognizer;
            Logger = logger;
            auth = AuthenticationModel.Instance();

            AddDialog(new TextPrompt(nameof(TextPrompt)));
            AddDialog(new ChoicePrompt(nameof(ChoicePrompt)));
            AddDialog(new OrdersDialog(configuration, luisRecognizer));
            AddDialog(new ChangeOrderDialog(configuration));
            AddDialog(new OrderStatusDialog(configuration));
            AddDialog(new ProductsDialog(configuration));
            AddDialog(new PaymentDialog(configuration));
            AddDialog(new ComplaintDialog(configuration));
            AddDialog(new DeliveriesDialog(configuration));
            AddDialog(new LoginDialog(configuration));
            AddDialog(new CreateAccountDialog(configuration));
            AddDialog(new RetourDialog(configuration));
            AddDialog(new GreetingDialog());
            AddDialog(new NoneDialog());
            AddDialog(new CancelAndHelpDialog(nameof(CancelAndHelpDialog)));
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                PromptStepAsync,
                LoginOrCreateAccountAsync,
                IntroStepAsync,
                ActStepAsync,
                CheckForNextStepAsync,
                FinalStepAsync,
            }));

            // The initial child Dialog to run.
            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> PromptStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if(!auth.GetAuthenticationState())
            {
                return await stepContext.PromptAsync(nameof(ChoicePrompt), new PromptOptions
                {
                    Prompt = MessageFactory.Text("Als u inlogt kunt u sneller geholpen worden. Log alstublieft in."),
                    RetryPrompt = MessageFactory.Text("Probeer het nog een keer"),
                    Choices = ChoiceFactory.ToChoices(new List<string> { "Inloggen", "Account aanmaken", "Doorgaan zonder in te loggen" })
                }, cancellationToken);
            }
            else
            {
                return await stepContext.NextAsync();
            }

                     
        }

        private async Task<DialogTurnResult> LoginOrCreateAccountAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if (auth.GetAuthenticationState())
            {
                return await stepContext.NextAsync();
            }

            FoundChoice choice = (FoundChoice)stepContext.Result;

            if(choice.Index == 0)
            {
                return await stepContext.BeginDialogAsync(nameof(LoginDialog), cancellationToken);
            }
            else if(choice.Index == 1)
            {
                return await stepContext.BeginDialogAsync(nameof(CreateAccountDialog), cancellationToken);
            }
            else
            {
                return await stepContext.NextAsync();
            }
        }

        private async Task<DialogTurnResult> IntroStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            await stepContext.Context.SendActivityAsync("Je bent ingelogd als: " + auth.GetLoggedInUser());

            if (!_luisRecognizer.IsConfigured)
            {
                await stepContext.Context.SendActivityAsync(
                    MessageFactory.Text("Luis is offline. Maak hieronder je keuze om toch van mijn diensten gebruik te kunnen maken."));

                //keuzes voor als Luis niet beschikbaar is
                return await stepContext.PromptAsync(nameof(ChoicePrompt), new PromptOptions
                {
                    Prompt = MessageFactory.Text("Wat kan ik voor je doen?"),
                    RetryPrompt = MessageFactory.Text("Probeer het nog een keer"),
                    Choices = ChoiceFactory.ToChoices(new List<string> { "Product bestellen", "Bestelling wijzigen", "Annuleren" }) //annuleren nog niet uitgewerkt
                }, cancellationToken);
            }

            // Use the text provided in FinalStepAsync or the default if it is the first time.
            var messageText = stepContext.Options?.ToString() ?? "Wat kan ik voor je doen?";
            var promptMessage = MessageFactory.Text(messageText, messageText, InputHints.ExpectingInput);
            return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = promptMessage }, cancellationToken);
        }

        private async Task<DialogTurnResult> ActStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            try
            {
                FoundChoice choice = (FoundChoice)stepContext.Result;
                if (choice != null)
                {
                    switch (choice.Index)
                    {
                        case 0:
                            return await stepContext.BeginDialogAsync(nameof(OrdersDialog), cancellationToken);

                        case 1:
                            return await stepContext.BeginDialogAsync(nameof(ChangeOrderDialog), cancellationToken);
                    }
                }
            }
            catch
            {

            }
            

            if (!_luisRecognizer.IsConfigured)
            {
                // LUIS is not configured, feedback to user and Enddialog
                await stepContext.EndDialogAsync();
                return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = MessageFactory.Text("Luis is offline. Wellicht kan ik je op een later moment helpen?") }, cancellationToken);
            }

            // Call LUIS and check the intent of the user. Send them to the corresponding Dialog.
            luisResult = await _luisRecognizer.RecognizeAsync<LuisHelper>(stepContext.Context, cancellationToken);
            switch (luisResult.TopIntent().intent)
            {
                case LuisHelper.Intent.Orders:                   
                    return await stepContext.BeginDialogAsync(nameof(OrdersDialog), luisResult, cancellationToken);
                
                case LuisHelper.Intent.Greeting:
                    return await stepContext.BeginDialogAsync(nameof(GreetingDialog), cancellationToken);

                case LuisHelper.Intent.Products:
                    return await stepContext.BeginDialogAsync(nameof(ProductsDialog), luisResult, cancellationToken);

                case LuisHelper.Intent.None:
                    return await stepContext.BeginDialogAsync(nameof(NoneDialog), cancellationToken);

                case LuisHelper.Intent.changeOrder:
                    return await stepContext.BeginDialogAsync(nameof(ChangeOrderDialog), luisResult, cancellationToken);

                case LuisHelper.Intent.Retour:
                    return await stepContext.BeginDialogAsync(nameof(RetourDialog), luisResult, cancellationToken);

                case LuisHelper.Intent.orderStatus:
                    return await stepContext.BeginDialogAsync(nameof(OrderStatusDialog), luisResult, cancellationToken);
                // case LuisHelper.Intent.Cancel;

                case LuisHelper.Intent.Payment:
                    return await stepContext.BeginDialogAsync(nameof(PaymentDialog), luisResult, cancellationToken);
                // implementeren

                case LuisHelper.Intent.Complaint:
                    return await stepContext.BeginDialogAsync(nameof(ComplaintDialog), luisResult, cancellationToken);

                case LuisHelper.Intent.Delivery:
                    return await stepContext.BeginDialogAsync(nameof(DeliveriesDialog), luisResult, cancellationToken);

                default:
                    // Catch all for unhandled intents
                    var didntUnderstandMessageText = $"Sorry, Ik snap je niet helemaal. Kun je je vraag anders stellen?";
                    var didntUnderstandMessage = MessageFactory.Text(didntUnderstandMessageText, didntUnderstandMessageText, InputHints.IgnoringInput);
                    await stepContext.Context.SendActivityAsync(didntUnderstandMessage, cancellationToken);
                    return await stepContext.NextAsync(null, cancellationToken);
            }           
        }

        private async Task<DialogTurnResult> CheckForNextStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            string message = "";

            if(stepContext.Result != null)
            {
                message = (string)stepContext.Result;
            }

            if (message.Contains("betalen"))
            {
                return await stepContext.BeginDialogAsync(nameof(PaymentDialog), message, cancellationToken);
            }
            else if (message.Contains("bezorgen"))
            {
                return await stepContext.BeginDialogAsync(nameof(DeliveriesDialog), message, cancellationToken);
            }
            else if (message.Contains("inloggen"))
            {
                return await stepContext.ReplaceDialogAsync(InitialDialogId, cancellationToken);
            }

            return await stepContext.NextAsync();
        }

        private async Task<DialogTurnResult> FinalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            //Geef hier de juiste feedback om het gesprek af te handelen
           

            // Restart the main dialog with a different message the second time around
            var promptMessage = "Kan ik iets anders voor je doen?";
            return await stepContext.ReplaceDialogAsync(InitialDialogId, promptMessage, cancellationToken);
        }
    }
}
