using Microsoft.Bot.Schema;
using ServiceStack.Support.Markdown;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CoreBot.Models
{
    public static class Cards
    {
        public static SigninCard GetSigninCard()
        {
            var signinCard = new SigninCard
            {
                Text = "Please login to be helped more effectively",
                Buttons = new List<CardAction> { new CardAction(ActionTypes.Signin, "Sign-in", value: "https://login.microsoftonline.com/") },
            };

            return signinCard;
        }

        public static ThumbnailCard GetThumbnailCard(string productName, string productInfo)
        {
            var thumbCard = new ThumbnailCard
            {
                Title = productName,
                Subtitle = "Productinformatie",
                Text = productInfo,
               // Images = new List<CardImage> { new CardImage("https://sec.ch9.ms/ch9/7ff5/e07cfef0-aa3b-40bb-9baa-7c9ef8ff7ff5/buildreactionbotframework_960.jpg") },
                Buttons = new List<CardAction> { new CardAction(ActionTypes.PostBack, "Bestellen", null, productName + " bestellen", "Product wordt toegevoegd aan de bestelling...", productName) },
            };

            return thumbCard;
        }
    }
}
