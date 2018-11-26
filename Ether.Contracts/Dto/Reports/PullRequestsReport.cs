﻿using System;
using System.Collections.Generic;
using System.Linq;
using Ether.Contracts.Attributes;

namespace Ether.Contracts.Dto.Reports
{
    [DbName(nameof(ReportResult))]
    public class PullRequestsReport : ReportResult
    {
        public PullRequestsReport(int individualReportsCount)
        {
            IndividualReports = new List<IndividualPRReport>(individualReportsCount);
        }

        public int TotalCompleted => IndividualReports.Sum(r => r.Completed);

        public int TotalAbandoned => IndividualReports.Sum(r => r.Abandoned);

        public int TotalActive => IndividualReports.Sum(r => r.Active);

        public int TotalCreated => IndividualReports.Sum(r => r.Created);

        public double AverageIterations => IndividualReports.Count == 0 ? 0 : IndividualReports.Sum(r => r.AverageIterations) / IndividualReports.Count;

        public double AverageComments => IndividualReports.Count == 0 ? 0 : IndividualReports.Sum(r => r.AverageComments) / IndividualReports.Count;

        public double CodeQuality => IndividualReports.Count == 0 ? 0 : IndividualReports.Sum(r => r.CodeQuality) / IndividualReports.Count;

        public TimeSpan AveragePRLifespan => IndividualReports.Count == 0 ? TimeSpan.Zero : TimeSpan.FromSeconds(IndividualReports.Sum(r => r.AveragePRLifespan.TotalSeconds) / IndividualReports.Count);

        public IList<IndividualPRReport> IndividualReports { get; set; }

        public void AddReport(IndividualPRReport report)
        {
            IndividualReports.Add(report);
        }

        public void AddEmpty(string memberName)
        {
            IndividualReports.Add(IndividualPRReport.GetEmptyFor(memberName));
        }

        public class IndividualPRReport
        {
            public string TeamMember { get; set; }

            public int Completed { get; set; }

            public int Created { get; set; }

            public int Active { get; set; }

            public int Abandoned { get; set; }

            public int TotalIterations { get; set; }

            public int TotalComments { get; set; }

            public TimeSpan AveragePRLifespan { get; set; }

            public double CodeQuality => ((double)TotalPullRequestsCount / TotalIterations) * 100;

            public double AverageIterations => TotalIterations / (double)TotalPullRequestsCount;

            public double AverageComments => TotalComments / (double)TotalPullRequestsCount;

            public int TotalPullRequestsCount => Completed + Active + Abandoned;

            public bool IsEmpty => Completed == 0 && Created == 0 && Active == 0 && Abandoned == 0 && TotalIterations == 0 && TotalComments == 0 && AveragePRLifespan == TimeSpan.Zero;

            public static IndividualPRReport GetEmptyFor(string userDisplayName)
            {
                return new IndividualPRReport { TeamMember = userDisplayName };
            }
        }
    }
}
