using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CoreBot.Models;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.AI.Luis;
using Newtonsoft.Json;

namespace CoreBot.CognitiveModels
{
    public class LuisHelper : IRecognizerConvert
    {
        public string Text;
        public string AlteredText;
        public enum Intent
        {
            Greeting,
            Orders,
            Products,
            Retour,
            changeOrder,
            orderStatus,
            Payment,
            Complaint,
            Delivery,
            Login,
            Logout,
            Cancel,
            Repair,
            Warranty,
            None
        }
        public Dictionary<Intent, IntentScore> Intents;

        public class _Entities
        {

            // Built-in entities
            public DateTimeSpec[] datetime;

            // Lists
           
            [JsonProperty("product")]
            public string[] products;

            [JsonProperty("bestelling")]
            public string[] order;

            // Composites

            // Instance
            public class _Instance
            {
                public InstanceData[] datetime;
            }
            [JsonProperty("$instance")]
            public _Instance _instance;
        }
        public _Entities Entities;

        [JsonExtensionData(ReadData = true, WriteData = true)]
        public IDictionary<string, object> Properties { get; set; }

        public void Convert(dynamic result)
        {
            var app = JsonConvert.DeserializeObject<LuisHelper>(JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }));
            Text = app.Text;
            AlteredText = app.AlteredText;
            Intents = app.Intents;
            Entities = app.Entities;
            Properties = app.Properties;
        }

        public (Intent intent, double score) TopIntent()
        {
            Intent maxIntent = Intent.None;
            var max = 0.0;
            foreach (var entry in Intents)
            {
                if (entry.Value.Score > max)
                {
                    maxIntent = entry.Key;
                    max = entry.Value.Score.Value;
                }
            }
            return (maxIntent, max);
        }
    }
}
