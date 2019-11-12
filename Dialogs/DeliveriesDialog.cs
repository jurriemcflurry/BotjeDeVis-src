using CoreBot.CognitiveModels;
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
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CoreBot.Dialogs
{
    public class DeliveriesDialog : CancelAndHelpDialog
    {
        private GremlinHelper gremlinHelper;
        private string request;
        private int orderNumber = 0;
        private string status;
        private int daysToDelivery;
        private string deliveryMoment;

        public DeliveriesDialog(IConfiguration configuration) : base(nameof(DeliveriesDialog))
        {
            gremlinHelper = new GremlinHelper(configuration);

            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                GetOrderNumberAsync,
                CheckIfOrderExistsAsync,
                CheckIfOrderReadyForDeliveryAsync,
                SetDeliveryDateAsync,
                SetDeliveryTimeAsync,
                ConfirmDeliveryMomentAsync,
                StoreDeliveryAsync,
                FinalStepAsync,
            }));
        }

        private async Task<DialogTurnResult> GetOrderNumberAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            string type = stepContext.Options.GetType().ToString();

            if (type.Equals("CoreBot.CognitiveModels.LuisHelper"))
            {
                LuisHelper luisResult = (LuisHelper)stepContext.Options;
                request = luisResult.Text;
            }
            else if (type.Equals("System.String"))
            {
                request = (string)stepContext.Options;
            }

            bool containsNumber = request.Any(Char.IsDigit);

            if (containsNumber)
            {
                string result = Regex.Match(request, @"\d+").Value;
                orderNumber = Int32.Parse(result);
                return await stepContext.NextAsync();
            }
            else
            {
                var messageText = "Wat is het ordernummer? Dan ga ik voor je op zoek.";
                var promptMessage = MessageFactory.Text(messageText, messageText, InputHints.ExpectingInput);
                return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = promptMessage }, cancellationToken);
            }
        }

        private async Task<DialogTurnResult> CheckIfOrderExistsAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if (orderNumber.Equals(0))
            {
                orderNumber = Int32.Parse((string)stepContext.Result);
            }

            bool orderExists = await gremlinHelper.OrderExistsByNumberAsync(orderNumber);

            if (orderExists)
            {
                return await stepContext.NextAsync();
            }
            else
            {
                await stepContext.Context.SendActivityAsync("Er is geen order gevonden met ordernummer " + orderNumber + ". Probeer opnieuw.");
                return await stepContext.ReplaceDialogAsync(nameof(PaymentDialog));
            }
        }

        private async Task<DialogTurnResult> CheckIfOrderReadyForDeliveryAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            status = await gremlinHelper.GetOrderStatusAsync(orderNumber);

            if (status.Equals("payment received"))
            {
                await stepContext.Context.SendActivityAsync("Deze bestelling is gereed om bezorgd te worden.");
                return await stepContext.PromptAsync(nameof(ChoicePrompt), new PromptOptions
                {
                    Prompt = MessageFactory.Text("Wil je de bezorging nu inplannen?"),
                    RetryPrompt = MessageFactory.Text("Probeer het nog een keer"),
                    Choices = ChoiceFactory.ToChoices(new List<string> { "Bezorging inplannen", "Annuleren" })
                }, cancellationToken);
            }
            else
            {
                await stepContext.Context.SendActivityAsync("Deze bestelling is niet gereed om bezorgd te worden of hier is al een bezorging voor ingepland.");
                await stepContext.Context.SendActivityAsync("De status van deze bestelling is: " + status);
                return await stepContext.EndDialogAsync();
            }
        }

        private async Task<DialogTurnResult> SetDeliveryDateAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            return await stepContext.PromptAsync(nameof(ChoicePrompt), new PromptOptions
            {
                Prompt = MessageFactory.Text("Bestelling " + orderNumber + " kan op de volgende momenten worden bezorgd: (kies een optie uit de lijst)"),
                RetryPrompt = MessageFactory.Text("Probeer het nog een keer"),
                Choices = ChoiceFactory.ToChoices(new List<string> { "Morgen", "Overmorgen", "Over 2 dagen", "Annuleren" })
            }, cancellationToken);
        }

        private async Task<DialogTurnResult> SetDeliveryTimeAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            FoundChoice choice = (FoundChoice)stepContext.Result;

            if(choice.Index != 3)
            {
                if (choice.Value.Equals("Morgen"))
                {
                    daysToDelivery = 1;
                }
                else if (choice.Value.Equals("Overmorgen"))
                {
                    daysToDelivery = 2;
                }
                else
                {
                    daysToDelivery = 3;
                }                
            }
            else
            {
                return await stepContext.EndDialogAsync();
            }
            

            return await stepContext.PromptAsync(nameof(ChoicePrompt), new PromptOptions
            {
                Prompt = MessageFactory.Text("Maak een keuze uit de volgende momenten:"),
                RetryPrompt = MessageFactory.Text("Probeer het nog een keer"),
                Choices = ChoiceFactory.ToChoices(new List<string> { "Ochtend", "Middag", "Avond", "Annuleren" })
            }, cancellationToken);
        }

        private async Task<DialogTurnResult> ConfirmDeliveryMomentAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            FoundChoice choice = (FoundChoice)stepContext.Result;
            
            if (choice.Index != 3)
            {
                deliveryMoment = choice.Value;
            }
            else
            {
                return await stepContext.EndDialogAsync();
            }

            return await stepContext.PromptAsync(nameof(ChoicePrompt), new PromptOptions
            {
                Prompt = MessageFactory.Text("Je bestelling zal worden geleverd over " + daysToDelivery + " dagen in de " + deliveryMoment),
                RetryPrompt = MessageFactory.Text("Probeer het nog een keer"),
                Choices = ChoiceFactory.ToChoices(new List<string> { "Bevestigen", "Annuleren" })
            }, cancellationToken);
        }

        private async Task<DialogTurnResult> StoreDeliveryAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            FoundChoice choice = (FoundChoice)stepContext.Result;

            if(choice.Index == 0)
            {
                bool storeDeliverySuccesful = await gremlinHelper.SetDeliveryStatusAsync(orderNumber, daysToDelivery, deliveryMoment);

                if (storeDeliverySuccesful)
                {
                    await stepContext.Context.SendActivityAsync("Je bezorgdatum is vastgesteld!");
                }
                else
                {
                    await stepContext.Context.SendActivityAsync("De bezorging kon helaas niet worden vastgesteld. Probeer het later opnieuw.");
                }

                return await stepContext.NextAsync();
            }
            else
            {
                return await stepContext.EndDialogAsync();
            }
        }

        private async Task<DialogTurnResult> FinalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            orderNumber = 0;
            status = "";
            daysToDelivery = 0;
            deliveryMoment = "";
            return await stepContext.EndDialogAsync();
        }
    }
}
