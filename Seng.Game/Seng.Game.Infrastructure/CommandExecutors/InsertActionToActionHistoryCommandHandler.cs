using Dapper;
using Seng.Game.Business.Commands;
using Seng.Game.Infrastructure.Database;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Seng.Game.Infrastructure.CommandExecutors
{
    class InsertActionToActionHistoryCommandHandler : ICommandHandler<InsertActionToActionHistoryCommand, bool>
    {
        private const string SqlQuery = @"INSERT INTO [history.ActionHistory] (
                                                        GameActionId
                                                    )
                                                    VALUES (
                                                        @GameActionId
                                                    );";

        private readonly IDbConnectionCreator _dbConnectionCreator;

        public InsertActionToActionHistoryCommandHandler(IDbConnectionCreator dbConnectionCreator)
        {
            _dbConnectionCreator = dbConnectionCreator;
        }

        public async Task<bool> Handle(InsertActionToActionHistoryCommand command, CancellationToken cancellationToken)
        {
            using (var dbConnection = _dbConnectionCreator.CreateOpenConnection())
            {
                int updateLogsNumber = await dbConnection.ExecuteAsync(SqlQuery, command);
                return updateLogsNumber > 0;
            }
        }
    }
}
