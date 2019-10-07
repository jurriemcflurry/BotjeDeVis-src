using Gremlin.Net.Driver;
using Gremlin.Net.Structure.IO.GraphSON;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.BotBuilderSamples.Dialogs;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CoreBot.Dialogs
{
    public class OrdersDialog : CancelAndHelpDialog
    {
        private GremlinServer gremlinServer;
        
        public OrdersDialog(IConfiguration configuration) : base(nameof(OrdersDialog))
        {
            string hostname = configuration["cosgraphendpoint"];
            int port = 443;
            string authKey = configuration["cosgraphkey"];
            string database = "botjedevis";
            string collection = "webshop";
            this.gremlinServer = new GremlinServer(hostname, port, enableSsl: true,
                                                            username: "/dbs/" + database + "/colls/" + collection,
                                                            password: authKey);

            AddDialog(new TextPrompt(nameof(TextPrompt)));
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                ConfirmOrdersIntent,
                FinalStepAsync,
            }));

            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> ConfirmOrdersIntent(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            //onderstaand is een test om de Gremlinconnectie uit te voeren; werkende code
            var gremlinClient = new GremlinClient(gremlinServer, new GraphSON2Reader(), new GraphSON2Writer(), GremlinClient.GraphSON2MimeType);
            string query = "g.V().hasLabel('person')";
            var results = await gremlinClient.SubmitAsync<dynamic>(query);
            foreach(var result in results)
            {
                string output = JsonConvert.SerializeObject(result);
                var jsonObj = JObject.Parse(output);
                string userId = (string)jsonObj["id"];

                

                var properties = jsonObj["properties"];
                var name = properties["name"];
                var personNameArray = name[0];
                var personName = personNameArray["value"];

                await stepContext.Context.SendActivityAsync(personName.ToString());
            }
            //var confirmOrdersIntent = "Je bent nu in de OrdersDialog!";
            
            return await stepContext.NextAsync(null, cancellationToken);
        }

        private async Task<DialogTurnResult> FinalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            return await stepContext.EndDialogAsync();
        }
    }
}
