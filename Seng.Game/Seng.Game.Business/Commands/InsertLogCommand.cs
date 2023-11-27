using System;
using System.Collections.Generic;
using System.Text;

namespace Seng.Game.Business.Commands
{
    public class InsertLogCommand : ICommand<bool>
    {
        public string Activity { get; set; }
        public DateTime Time { get; set; }
    }
}
