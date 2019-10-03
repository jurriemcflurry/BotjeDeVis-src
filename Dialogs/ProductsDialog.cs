using Microsoft.Bot.Builder.Dialogs;
using Microsoft.BotBuilderSamples.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CoreBot.Dialogs
{
    public class ProductsDialog : CancelAndHelpDialog
    {
        public ProductsDialog() : base(nameof(ProductsDialog))
        {
            AddDialog(new TextPrompt(nameof(TextPrompt)));
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
        {
                ConfirmProductsIntent,
                FinalStepAsync,
        }));

            InitialDialogId = nameof(WaterfallDialog);
        }
        private async Task<DialogTurnResult> ConfirmProductsIntent(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var confirmOrdersIntent = "Je bent nu in de ProductsDialog!";
            await stepContext.Context.SendActivityAsync(confirmOrdersIntent);
            return await stepContext.NextAsync(null, cancellationToken);
        }

        private async Task<DialogTurnResult> FinalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            return await stepContext.EndDialogAsync();
        }
    }
}
