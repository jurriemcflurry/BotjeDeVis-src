using CoreBot.Database;
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
    public class ChangeOrderDialog : CancelAndHelpDialog
    {
        private GremlinHelper gremlinHelper;

        public ChangeOrderDialog(IConfiguration configuration) : base(nameof(ChangeOrderDialog))
        {
            //gremlinHelper om later snel met de database te kunnen werken
            gremlinHelper = new GremlinHelper(configuration);

            AddDialog(new TextPrompt(nameof(TextPrompt)));
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog),new WaterfallStep[]
            {
                AnswerQuestion,
                FinishDialog,
            }));

            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> AnswerQuestion(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            string antwoord = await gremlinHelper.AnswerQuestion("bestelling wijzigen");
            await stepContext.Context.SendActivityAsync(antwoord);

            return await stepContext.NextAsync();
        }

        private async Task<DialogTurnResult> FinishDialog(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            return await stepContext.EndDialogAsync();
        }
    }
}
