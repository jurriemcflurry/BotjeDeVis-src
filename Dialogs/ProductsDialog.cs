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
                ConfirmProductsIntentAsync,
                FinalStepAsync,
        }));

            InitialDialogId = nameof(WaterfallDialog);
        }

        //Hier moet het product worden opgehaald waar een vraag over is, of anders worden gevraagd over welk product een vraag is
        //vervolgens moet de info over dat product worden getoond
        //uitzoeken of alles getoond wordt, of dat specifieke info ook uit de tekst gehaald kan worden (bijv entiteit ' status' over de beschikbaarheid)

        private async Task<DialogTurnResult> ConfirmProductsIntentAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
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
