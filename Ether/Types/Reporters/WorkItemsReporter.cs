﻿using Ether.Interfaces;
using Ether.Types.Configuration;
using Ether.Types.Data;
using Ether.Types.DTO.Reports;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static Ether.Types.DTO.Reports.WorkItemsReport;

namespace Ether.Types.Reporters
{
    public class WorkItemsReporter : ReporterBase
    {
        private const string WorkItemsQuery = @"SELECT [System.Id] FROM WorkItems WHERE [System.WorkItemType] IN ('Bug', 'Task') AND 
                        ([System.State] NOT IN ('Resolved', 'Closed', 'Done') AND [System.AssignedTo] IN ({0})) OR
                        ([Microsoft.VSTS.Common.ResolvedBy] IN ({0}) AND [Microsoft.VSTS.Common.ResolvedDate] >= '{2}' AND [Microsoft.VSTS.Common.ResolvedDate] <= '{3}') OR 
                        ([Microsoft.VSTS.Common.ClosedBy] IN ({0}) AND [Microsoft.VSTS.Common.ClosedDate] >= '{2}' AND [Microsoft.VSTS.Common.ClosedDate] <= '{3}')
                        ASOF '{1}'";
        private static readonly DateTime VSTSMaxDate = new DateTime(9999, 1, 1);
        private static readonly Guid _reporterId = Guid.Parse("54c62ebe-cfef-46d5-b90f-ebb00a1611b7");

        private readonly VSTSClient _client;

        public WorkItemsReporter(VSTSClient client, IRepository repository, IOptions<VSTSConfiguration> configuration, ILogger<WorkItemsReporter> logger) 
            : base(repository, configuration, logger)
        {
            _client = client;
        }

        public override string Name => "Work items report";
        public override Guid Id => _reporterId;
        public override Type ReportType => typeof(WorkItemsReport);

        protected override async Task<ReportResult> ReportInternal()
        {
            var workItemsIds = await GetWorkItemsForPeriod();
            var resolutions = new List<WorkItemResolution>(workItemsIds.Count() * 2);
            foreach (var id in workItemsIds)
                resolutions.AddRange(await GetResolutionsFor(id));

            var result = new WorkItemsReport();
            var resolutionsByTeamMember = resolutions
                .GroupBy(r => r.TeamMember);
            result.IndividualReports = new List<IndividualWorkItemsReport>(resolutionsByTeamMember.Count());
            foreach (var resolution in resolutionsByTeamMember)
            {
                var individualReport = new IndividualWorkItemsReport();
                individualReport.WorkItems = resolution
                    .ToList()
                    .OrderBy(r => r.Reason)
                    .ThenBy(r => r.Id);
                individualReport.TeamMember = resolution.Key;
                result.IndividualReports.Add(individualReport);
            }
            return result;
        }

        private async Task<IEnumerable<int>> GetWorkItemsForPeriod()
        {
            var projectsWithWorkItems = Input.Projects.Where(p => !p.DoesNotHaveWorkItems);
            var emails = Input.Members.Aggregate(string.Empty, (a, s) => a += $",'{s.Email}'").Trim(',');
            var dateDiff = Input.ActualEndDate - Input.Query.StartDate;
            var workItemsIds = new List<int>();
            for (int i = 0; i <= dateDiff.Days; i++)
            {
                foreach (var project in projectsWithWorkItems)
                {
                    var currentDate = Input.Query.StartDate.AddDays(i);
                    var queryString = string.Format(WorkItemsQuery, emails, currentDate.ToString("MM/dd/yyyy"), Input.Query.StartDate.ToString("MM/dd/yyyy"), Input.ActualEndDate.ToString("MM/dd/yyyy"));
                    var query = new ItemsQuery(queryString);
                    var wiqlEndPoint = $"https://{_configuration.InstanceName}.visualstudio.com/{project.Name}/_apis/wit/wiql?api-version=3.0";
                    var workItemsResponse = await _client.ExecutePost<WorkItemsQueryResponse>(wiqlEndPoint, query);
                    var newItems = workItemsResponse.WorkItems
                        .Where(w => !workItemsIds.Contains(w.Id))
                        .Select(w => w.Id)
                        .Distinct();
                    workItemsIds.AddRange(newItems);
                }
            }

            return workItemsIds;
        }

        private async Task<IEnumerable<WorkItemResolution>> GetResolutionsFor(int id)
        {
            var updatesUrl = $"https://{_configuration.InstanceName}.visualstudio.com/DefaultCollection/_apis/wit/WorkItems/{id}/updates?api-version=3.0";
            var updates = await _client.ExecuteGet<ValueResponse<WorkItemUpdate>>(updatesUrl);
            var initialUpdate = updates.Value.First();
            var actualUpdates = updates.Value
                .SkipWhile(u => u.RevisedDate != VSTSMaxDate && u.RevisedDate <= Input.Query.StartDate)
                .TakeWhile(u => u.RevisedDate == VSTSMaxDate || u.RevisedDate <= Input.ActualEndDate)
                .ToList();

            var itemType = initialUpdate.WorkItemType.NewValue;
            var title = updates.Value.Last(u => !string.IsNullOrEmpty(u.Title.NewValue)).Title.NewValue;
            var resolvedItem = actualUpdates.LastOrDefault(i => (itemType == "Bug" && i.State.NewValue == "Resolved" && Input.Members.Any(m => i.ResolvedBy.NewValue.Contains(m.Email))));
            var investigatedItem = actualUpdates.LastOrDefault(i =>
                i.AreaPath.IsValueChanged() &&
                i.AssignedTo.IsValueCleared() &&
                i.State.OldValue == "Active" &&
                i.State.NewValue == "New" &&
                Input.Members.Any(m => i.AssignedTo.OldValue.Contains(m.Email)));

            List<WorkItemResolution> result = new List<WorkItemResolution>(2);
            if (resolvedItem != null)
            {
                result.Add(WorkItemResolution.GetResolved(id, title, itemType, resolvedItem));
            }

            if (investigatedItem != null)
            {
                result.Add(WorkItemResolution.GetInvestigated(id, title, itemType, investigatedItem));
            }

            return result;
        }

        #region Private classes
        private struct ItemsQuery
        {
            public ItemsQuery(string query)
            {
                Query = query;
            }

            public string Query { get; set; }
        }

        private class WorkItemsQueryResponse
        {
            public WorkItemLink[] WorkItems { get; set; }

            public class WorkItemLink
            {
                public int Id { get; set; }

                public string Url { get; set; }
            }
        }

        private class ValueResponse<T> where T : class, new()
        {
            public T[] Value { get; set; }
        }

        public class WorkItemUpdate
        {
            public DateTime RevisedDate { get; set; }

            public Dictionary<string, UpdateValue> Fields { get; set; }

            public UpdateValue Reason => this["System.Reason"];
            public UpdateValue AreaPath => this["System.AreaPath"];
            public UpdateValue ResolvedBy => this["Microsoft.VSTS.Common.ResolvedBy"];
            public UpdateValue AssignedTo => this["System.AssignedTo"];
            public UpdateValue State => this["System.State"];
            public UpdateValue WorkItemType => this["System.WorkItemType"];
            public UpdateValue Title => this["System.Title"];

            public UpdateValue this[string key]
            {
                get
                {
                    if (Fields == null || !Fields.ContainsKey(key))
                        return new UpdateValue();

                    return Fields[key];
                }
            }

            public struct UpdateValue
            {
                public string NewValue { get; set; }
                public string OldValue { get; set; }

                public bool IsValueChanged()
                {
                    return !string.Equals(NewValue, OldValue);
                }

                public bool IsValueCleared()
                {
                    return !string.IsNullOrEmpty(OldValue) && string.IsNullOrEmpty(NewValue);
                }
            }
        }
        #endregion
    }
}
