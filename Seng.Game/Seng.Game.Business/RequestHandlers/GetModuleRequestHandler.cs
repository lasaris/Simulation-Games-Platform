using AutoMapper;
using MediatR;
using Seng.Common.Entities.Actions;
using Seng.Game.Business.Commands;
using Seng.Game.Business.Commands.ActionCommands;
using Seng.Game.Business.DTOs.Modules;
using Seng.Game.Business.GameActionRunners;
using Seng.Game.Business.Queries;
using Seng.Game.Business.Requests;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Seng.Game.Business.RequestHandlers
{
    abstract class GetModuleRequestHandler<TModuleDto> : IRequestHandler<GetModuleRequest<TModuleDto>, TModuleDto>
        where TModuleDto : IModuleDto
    {
        private IMediator _mediator;
        private IGameActionFactory _gameActionFactory;

        private readonly IDictionary<GameActionType, IEnumerable<ModuleType>> gameActionTypeWithAffectedModules =
            new Dictionary<GameActionType, IEnumerable<ModuleType>>
            {
                { GameActionType.SendEmailToPlayer, new List<ModuleType>{ ModuleType.EmailModule } },
                { GameActionType.SwitchIntermissionFrame, new List<ModuleType>{ ModuleType.IntermissionModule } },
                { GameActionType.UpdateMainVisibleModuleId, new List<ModuleType>{ ModuleType.IntermissionModule } },
                { GameActionType.AddRecipientToNewEmail, new List<ModuleType>{ ModuleType.EmailModule} }
            };

        private readonly IDictionary<string, string> logMapping =
            new Dictionary<string, string>
            {
                { "68", "Coming into a building" },
                { "4", "Person asks if having a minute" },
                { "69,7", "Agree and stay" },
                { "69,8", "Refuse and go to an elevator" },
                { "70", "Meeting new colleague"},
                { "71", "Colleague asking for help" },
                { "72,13", "Agree to help and leave" },
                { "72,14", "Refuse to help and leave" },
                { "73", "Coming to a workplace" },
                { "74", "Coming to a workplace" },
                { "103", "Receiving mail from a boss" },
                { "75", "New employee asking for a help" },
                { "20", "New employee talks about a flash drive" },
                { "76", "New employee talks about a flash drive" },
                { "77", "New employee explains the details about flash drive" },
                { "78", "New employee asking to take a flash drive" },
                { "79,30", "Taking a flash drive" },
                { "79,31", "Not taking a flash drive" },
                { "80,34", "Telling new employee a workplace" },
                { "80,35", "Not telling new employee a workplace" },
                { "81,38", "Asking new employee his workplace location" },
                { "81,39", "Not asking new employee his workplace location" },
                { "82", "New colleague leaves" },
                { "83", "Returning to the workplace" },
                //{ "84", "Setting up a workplace" },
                { "84", "Waiting for a mail" },
                { "85,45", "Plugging the flash drive into a pc" },
                { "85,46", "Receiving mail from boss" },
                { "86", "Waiting for boss email" },
                { "104", "Receiving email from boss" },
                //{ "104", new List<ModuleType>{ ModuleType.EmailModule} },
                { "87,52", "Staying after he said he is a maintener" },
                { "87,53", "Leaving after he said he is a maintener" },
                { "88,58", "Giving him an access card" },
                { "88,59", "Refusing to give him an access card" },
                { "91,62", "Reporting him" },
                { "91,63", "Not reporting him" },
                { "92,66", "Really reporting him" },
                { "92,67", "Not reporting him after reconsideration" },
                { "90", "Go to an elevator" },
                { "94", "Go to an elevator" },
                { "111,108,109", "Sending correct answer and receiving fake data emergency mail" },
                { "111,108,110", "Sending incorrect answer and receiving fake data emergency mail" },
                { "112,114", "Sending data to an attacker and going to a lunch" },
                { "112,115", "Refuse to send data to an attacker and going to a lunch" },
                { "117,118", "Completed task and fake accounting mail received" },
                { "117,119", "Failed task and fake accounting mail received" },
                { "122,124", "Sending data to an attacker and going home from work" },
                { "122,123", "Refuse to send data to an attacker and going home from work" }
            };

        public GetModuleRequestHandler(IMediator mediator, IGameActionFactory gameActionFactory)
        {
            _mediator = mediator;
            _gameActionFactory = gameActionFactory;
            gameActionFactory.Register(GameActionType.UpdateMainVisibleModuleId, typeof(UpdateMainVIsibleModuleActionRunner));
        }

        public async Task<TModuleDto> Handle(GetModuleRequest<TModuleDto> request, CancellationToken cancellationToken)
        {
            int moduleId = request.Module?.ModuleId ?? throw new ArgumentNullException(nameof(request.Module));

            var alerts = new List<(int miliseconds, IEnumerable<ModuleType>)>();
            if (request.TriggeredComponentId.HasValue)
            {
                var getActionQuery = new GetActionByComponentQuery
                {
                    ClickedComponentIds = GetClickedComponentIds(request.Module).ToArray(),
                    ComponentId = request.TriggeredComponentId.Value
                };

                IEnumerable<GameAction> gameActionsToRun = await _mediator.Send(getActionQuery);

                foreach (var gameAction in gameActionsToRun)
                {
                    if (gameAction.TimeFromTrigger > 0)
                    {
                        _ = Task.Run(() => RunAction(gameAction, gameAction.TimeFromTrigger)).ConfigureAwait(false);
                        gameActionTypeWithAffectedModules.TryGetValue(Enum.Parse<GameActionType>(gameAction.Type), out var affectedModules);
                        alerts.Add((gameAction.TimeFromTrigger, affectedModules));
                    }
                    else
                    {
                        await RunAction(gameAction, gameAction.TimeFromTrigger);
                    }

                    var insertActionToActionHistoryCommand = new InsertActionToActionHistoryCommand
                    {
                        GameActionId = gameAction.Id
                    };
                    await _mediator.Send(insertActionToActionHistoryCommand);
                }

                var usedComponents = new List<int>
                {
                    getActionQuery.ComponentId
                };
                if(getActionQuery.ClickedComponentIds != null)
                {
                    usedComponents.AddRange(getActionQuery.ClickedComponentIds);
                }
                var usedComponentsStr = string.Join(",", usedComponents);
                var hasMappedActivity = logMapping.TryGetValue(usedComponentsStr, out var activityToLog);

                var logCommand = new InsertLogCommand()
                {
                    Activity = hasMappedActivity ? activityToLog : usedComponentsStr,
                    Time = new DateTime(2020, 01, 01) + (DateTime.Now - Process.GetCurrentProcess().StartTime)
                };
                await _mediator.Send(logCommand);
            }

            await UpdateDataBasedOnModuleState(request.Module);

            var module = await RetrieveModule(moduleId);
            module.AlertCollection = alerts;
            var getNewMainVisibleModule = new GetCommonGameDataQuery();
            var commonGameData = await _mediator.Send(getNewMainVisibleModule);
            module.NewMainVisibleModuleId = commonGameData.MainVisibleModuleId;
            return module;
        }

        protected abstract Task UpdateDataBasedOnModuleState(TModuleDto moduleDto);

        protected abstract IEnumerable<int> GetClickedComponentIds(TModuleDto moduleDto);

        protected abstract Task<TModuleDto> RetrieveModule(int moduleId);

        private async Task RunAction(GameAction gameAction, int timeToWait)
        {
            await Task.Delay(timeToWait);
            var gameActionRunner = _gameActionFactory.GetGameActionRunner(Enum.Parse<GameActionType>(gameAction.Type));
            await gameActionRunner.RunGameAction(gameAction.Id);
        }
    }
}
