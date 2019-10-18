using CoreBot.Database;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Bot.Schema;
using Microsoft.BotBuilderSamples.Dialogs;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CoreBot.Dialogs
{
    public class OrderStatusDialog : CancelAndHelpDialog
    {
        private GremlinHelper gremlinHelper;

        public OrderStatusDialog(IConfiguration configuration) : base(nameof(OrderStatusDialog))
        {
            gremlinHelper = new GremlinHelper(configuration);
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]{
                GetOrderNumberAsync,
                CheckOrderStatusAsync,
                FinalStepAsync,
            }));

            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> GetOrderNumberAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var messageText = "Wat is het ordernummer? Dan ga ik voor je op zoek.";
            var promptMessage = MessageFactory.Text(messageText, messageText, InputHints.ExpectingInput);
            return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = promptMessage }, cancellationToken);
        }

        private async Task<DialogTurnResult> CheckOrderStatusAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            int orderNumber = Int32.Parse((string)stepContext.Result);
            string status = await gremlinHelper.GetOrderStatusAsync(orderNumber);
            await stepContext.Context.SendActivityAsync("De status van je order is dat deze door ons is: " + status);
            return await stepContext.NextAsync();
        }

        private async Task<DialogTurnResult> FinalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            return await stepContext.EndDialogAsync();
        }
    }
}
