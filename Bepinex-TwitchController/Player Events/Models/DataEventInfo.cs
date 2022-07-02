namespace TwitchController.Player_Events.Models
{
    using System;

    /// <summary>
    /// DataEvents can make use of users messages when subbing, donating bits, or redeeming channel points
    /// </summary>
    public class DataEventInfo : EventInfo
    {
        public Action<string, string, string> DataAction;
        public string UserInput;

        public DataEventInfo(Action<string, string> action, int bitCost, Action<string, string, string> dataAction) : base(action, bitCost, 0)
        {
            DataAction = dataAction;
            UserInput = "";
        }

        public DataEventInfo(string perp, DataEventInfo dataEventInfo, string userInput) : base(perp, dataEventInfo)
        {
            DataAction = dataEventInfo.DataAction;
            UserInput = userInput;
        }
    }
}
