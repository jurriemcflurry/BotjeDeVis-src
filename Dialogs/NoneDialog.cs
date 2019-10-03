using Microsoft.Bot.Builder.Dialogs;
using Microsoft.BotBuilderSamples.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CoreBot.Dialogs
{
    public class NoneDialog : CancelAndHelpDialog
    {
        public NoneDialog() : base(nameof(GreetingDialog))
        {
            AddDialog(new TextPrompt(nameof(TextPrompt)));
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                ConfirmNoneIntent,
                FinalStepAsync,
            }));

            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> ConfirmNoneIntent(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var confirmOrdersIntent = "Sorry, dit is niet iets waar ik je mee kan helpen. Kies een onderwerp om mee verder te gaan!";

            // verder uitwerken en opties geven wat de bot kan doen
            await stepContext.Context.SendActivityAsync(confirmOrdersIntent);
            return await stepContext.NextAsync(null, cancellationToken);
        }

        private async Task<DialogTurnResult> FinalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            return await stepContext.EndDialogAsync();
        }
    }
}
