using System;
using System.Collections.Generic;
using System.Text;

namespace Seng.Game.Business.Commands
{
    public class InsertActionToActionHistoryCommand : ICommand<bool>
    {
        public int GameActionId { get; set; }
    }
}
