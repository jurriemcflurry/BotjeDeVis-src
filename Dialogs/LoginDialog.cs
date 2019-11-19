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
    public class LoginDialog : CancelAndHelpDialog
    {
        private GremlinHelper gremlinHelper;
        private string username;
        private string password;
        private AuthenticationModel auth;

        public LoginDialog(IConfiguration configuration) : base(nameof(LoginDialog))
        {
            gremlinHelper = new GremlinHelper(configuration);
            auth = AuthenticationModel.Instance();

            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                AskForUsernameAsync,
                AskForPasswordAsync,
                CheckCredentialsAsync,
                SetAuthenticationStateAsync,
                FinalStepAsync,
            }));

            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> AskForUsernameAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions
            {
                Prompt = MessageFactory.Text("Wat is je naam?")
            }, cancellationToken);
        }

        private async Task<DialogTurnResult> AskForPasswordAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            username = (string)stepContext.Result;

            return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions
            {
                Prompt = MessageFactory.Text("Wat is je wachtwoord?")
            }, cancellationToken);
        }

        private async Task<DialogTurnResult> CheckCredentialsAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            password = (string)stepContext.Result;

            bool authenticationCorrect = await gremlinHelper.CheckCredentialsAsync(username, password);

            if (!authenticationCorrect)
            {
                await stepContext.Context.SendActivityAsync("Gebruikersnaam of wachtwoord is niet correct. Probeer aub opnieuw");
                return await stepContext.ReplaceDialogAsync(nameof(LoginDialog));
            }

            return await stepContext.NextAsync();
        }

        private async Task<DialogTurnResult> SetAuthenticationStateAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            auth.SetAuthenticated(username);
            await stepContext.Context.SendActivityAsync("Succesvol ingelogd!");
            return await stepContext.NextAsync();
        }

        private async Task<DialogTurnResult> FinalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            return await stepContext.EndDialogAsync();
        }
    }
}
