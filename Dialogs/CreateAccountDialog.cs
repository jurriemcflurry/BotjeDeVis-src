using CoreBot.Database;
using CoreBot.Models;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.BotBuilderSamples.Dialogs;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CoreBot.Dialogs
{
    public class CreateAccountDialog : CancelAndHelpDialog
    {
        private GremlinHelper gremlinHelper;
        private AuthenticationModel auth;
        private string username;
        private string password;
        private string confirmPassword;

        public CreateAccountDialog(IConfiguration configuration) : base(nameof(CreateAccountDialog))
        {
            gremlinHelper = new GremlinHelper(configuration);
            auth = AuthenticationModel.Instance();

            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                CreateUsernameAsync,
                SetPasswordAsync,
                ConfirmPasswordAsync,
                StoreUserAsync,
                FinalStepAsync,
            }));

            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> CreateUsernameAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = MessageFactory.Text("Wat is je naam?") });
        }

        private async Task<DialogTurnResult> SetPasswordAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            username = (string)stepContext.Result;

            return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = MessageFactory.Text("Kies een wachtwoord") });
        }

        private async Task<DialogTurnResult> ConfirmPasswordAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            password = (string)stepContext.Result;

            return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = MessageFactory.Text("Herhaal je wachtwoord") });
        }

        private async Task<DialogTurnResult> StoreUserAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            confirmPassword = (string)stepContext.Result;
            if (!password.Equals(confirmPassword))
            {
                await stepContext.Context.SendActivityAsync("Wachtwoorden komen niet overeen. Probeer het aub opnieuw.");
                return await stepContext.ReplaceDialogAsync(nameof(CreateAccountDialog));
            }

            bool accountCreated = await gremlinHelper.CreateAccountAsync(username, password);

            if (accountCreated)
            {
                await stepContext.Context.SendActivityAsync("Account succesvol aangemaakt! Je bent direct ingelogd.");
                auth.SetAuthenticated(username);
            }
            else
            {
                await stepContext.Context.SendActivityAsync("Account kan op dit moment niet aangemaakt worden. Probeer het later opnieuw.");
            }

            return await stepContext.NextAsync();
        }

        private async Task<DialogTurnResult> FinalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            return await stepContext.EndDialogAsync();
        }
    }
}
