﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Autofac.Features.Indexed;
using Ether.Contracts.Dto.Reports;
using Ether.Contracts.Interfaces;
using Ether.Core.Types;
using Ether.Core.Types.Commands;
using Ether.Core.Types.Handlers.Commands;
using Ether.Tests.Extensions;
using Ether.ViewModels;
using Ether.ViewModels.Types;
using FizzWare.NBuilder;
using FizzWare.NBuilder.Dates;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace Ether.Tests.Handlers.Commands
{
    [TestFixture]
    public class GeneratePullRequestsReportHandlerTests : BaseHandlerTest
    {
        private const string DataSourceType = "FooSource";
        private Mock<IIndex<string, IDataSource>> _dataSourceProviderMock;
        private Mock<IDataSource> _dataSourceMock;
        private RandomGenerator _generator = new RandomGenerator();
        private GeneratePullRequestsReportHandler _handler;

        [Test]
        public void ShouldThrowExceptionIfDataSourceTypeIsNullOrEmpty([Values(null, "")]string dataSourceType)
        {
            _handler.Awaiting(h => h.Handle(new GeneratePullRequestsReport { DataSourceType = dataSourceType }))
                .Should().Throw<ArgumentNullException>();
        }

        [Test]
        public void ShouldThrowExceptionIfNotSupportedDataSourceType()
        {
            const string FakeDataSource = "Bla";
            IDataSource ds;
            _dataSourceProviderMock.Setup(p => p.TryGetValue(FakeDataSource, out ds)).Returns(false);
            _handler.Awaiting(h => h.Handle(new GeneratePullRequestsReport { DataSourceType = FakeDataSource }))
                .Should().Throw<ArgumentException>();
        }

        [Test]
        public void ShouldThrowExceptionIfProfileNotFound()
        {
            SetupGetProfile(null);

            _handler.Awaiting(h => h.Handle(new GeneratePullRequestsReport { DataSourceType = DataSourceType, Profile = Guid.NewGuid() }))
                .Should().Throw<ArgumentException>();

            _dataSourceMock.Verify();
        }

        [Test]
        public async Task ShouldReturnEmptyReportForEachMemberIfNoPullRequests()
        {
            const int expectedTeamMembersCount = 6;
            var members = Builder<TeamMemberViewModel>.CreateListOfSize(expectedTeamMembersCount)
                .Build();
            var profile = Builder<ProfileViewModel>.CreateNew()
                .With(p => p.Members = members.Select(m => m.Id).ToArray())
                .With(p => p.Repositories = new Guid[0])
                .Build();
            SetupGetProfile(profile);
            SetupGetMember(members);
            SetupGetPullRequests(Enumerable.Empty<PullRequestViewModel>());

            var command = new GeneratePullRequestsReport { DataSourceType = DataSourceType, Profile = Guid.NewGuid() };
            await InvokeAndVerify(command, (report, reportId) =>
            {
                report.Should().NotBeNull();
                report.Id.Should().Be(reportId);
                report.IndividualReports.Should().HaveCount(expectedTeamMembersCount);
                report.IndividualReports.Should().OnlyContain(r => r.IsEmpty);
            });

            _dataSourceMock.VerifyAll();
            RepositoryMock.Verify();
        }

        [Test]
        public async Task ShouldReturnEmptyReportIfNoMembers()
        {
            var members = Enumerable.Empty<TeamMemberViewModel>();
            var profile = Builder<ProfileViewModel>.CreateNew()
                .With(p => p.Members = new Guid[0])
                .With(p => p.Repositories = new Guid[0])
                .Build();
            SetupGetProfile(profile);
            SetupGetMember(members);
            SetupGetPullRequests(Enumerable.Empty<PullRequestViewModel>());

            var command = new GeneratePullRequestsReport { DataSourceType = DataSourceType, Profile = Guid.NewGuid() };
            await InvokeAndVerify(command, (report, reportId) =>
            {
                report.Should().NotBeNull();
                report.Id.Should().Be(reportId);
                report.IndividualReports.Should().BeEmpty();
            });

            RepositoryMock.Verify();
        }

        [Test]
        public async Task ShouldCorrectlyCalculateMetricsForEachMember()
        {
            const int expectedTeamMembersCount = 2;
            var start = DateTime.Now.AddDays(-7);
            var end = DateTime.Now;
            var members = Builder<TeamMemberViewModel>.CreateListOfSize(4)
                .Build();
            var memberIds = members.Take(2).Select(m => m.Id).ToArray();
            var additionalMemberIds = members.TakeLast(2).Select(m => m.Id).ToArray();
            var repositories = new[] { Guid.NewGuid(), Guid.NewGuid() };
            var profile = Builder<ProfileViewModel>.CreateNew()
                .With(p => p.Members = memberIds)
                .With(p => p.Repositories = repositories)
                .Build();

            var pullRequests = GetPullRequests(start, end, memberIds, repositories)
                .Union(GetPullRequests(start, end, memberIds, new[] { Guid.NewGuid(), Guid.NewGuid() }))
                .Union(GetPullRequests(start, end, additionalMemberIds, repositories))
                .ToArray();

            SetupGetProfile(profile);
            SetupGetMember(members);
            SetupGetPullRequests(pullRequests);

            var command = new GeneratePullRequestsReport { DataSourceType = DataSourceType, Profile = Guid.NewGuid(), Start = start, End = end };
            await InvokeAndVerify(command, (report, reportId) =>
            {
                report.Should().NotBeNull();
                report.IndividualReports.Should().HaveCount(expectedTeamMembersCount);
                var firstMemberReport = report.IndividualReports.First();
                var secondMemberReport = report.IndividualReports.Last();

                firstMemberReport.Created.Should().Be(7);
                firstMemberReport.Active.Should().Be(3);
                firstMemberReport.Completed.Should().Be(3);
                firstMemberReport.Abandoned.Should().Be(2);
                firstMemberReport.TotalIterations.Should().Be(8);
                firstMemberReport.TotalComments.Should().Be(16);

                secondMemberReport.Created.Should().Be(7);
                secondMemberReport.Active.Should().Be(2);
                secondMemberReport.Completed.Should().Be(6);
                secondMemberReport.Abandoned.Should().Be(1);
                secondMemberReport.TotalIterations.Should().Be(9);
                secondMemberReport.TotalComments.Should().Be(18);
            });

            _dataSourceMock.VerifyAll();
            RepositoryMock.Verify();
        }

        [Test]
        public async Task ShouldSetReportMetadata()
        {
            var members = Enumerable.Empty<TeamMemberViewModel>();
            var profile = Builder<ProfileViewModel>.CreateNew()
                .With(p => p.Members = new Guid[0])
                .With(p => p.Repositories = new Guid[0])
                .Build();
            SetupGetProfile(profile);
            SetupGetMember(members);
            SetupGetPullRequests(Enumerable.Empty<PullRequestViewModel>());

            var command = new GeneratePullRequestsReport { DataSourceType = DataSourceType, Profile = Guid.NewGuid() };
            await InvokeAndVerify(command, (report, reportId) =>
            {
                report.Should().NotBeNull();
                report.Id.Should().Be(reportId);
                report.DateTaken.Should().BeCloseTo(DateTime.UtcNow);
                report.StartDate.Should().Be(command.Start);
                report.EndDate.Should().Be(command.End);
                report.ProfileId.Should().Be(profile.Id);
                report.ProfileName.Should().Be(profile.Name);
                report.ReportType.Should().Be(Constants.PullRequestsReportType);
                report.ReportName.Should().Be(Constants.PullRequestsReportName);
            });

            RepositoryMock.Verify();
        }

        protected override void Initialize()
        {
            _dataSourceMock = new Mock<IDataSource>(MockBehavior.Strict);
            var ds = _dataSourceMock.Object;
            _dataSourceProviderMock = new Mock<IIndex<string, IDataSource>>(MockBehavior.Strict);
            _dataSourceProviderMock.Setup(p => p.TryGetValue(DataSourceType, out ds)).Returns(true);

            _handler = new GeneratePullRequestsReportHandler(_dataSourceProviderMock.Object, RepositoryMock.Object, Mock.Of<ILogger<GeneratePullRequestsReportHandler>>());
        }

        private void SetupGetProfile(ProfileViewModel profile)
        {
            _dataSourceMock.Setup(d => d.GetProfile(It.IsAny<Guid>()))
                .ReturnsAsync(profile)
                .Verifiable();
        }

        private void SetupGetMember(IEnumerable<TeamMemberViewModel> allMembers)
        {
            _dataSourceMock.Setup(d => d.GetTeamMember(It.IsAny<Guid>()))
                .Returns<Guid>(id => Task.FromResult(allMembers.SingleOrDefault(m => m.Id == id)));
        }

        private void SetupGetPullRequests(IEnumerable<PullRequestViewModel> allPullRequests)
        {
            _dataSourceMock.Setup(d => d.GetPullRequests(It.IsAny<Expression<Func<PullRequestViewModel, bool>>>()))
                .Returns<Expression<Func<PullRequestViewModel, bool>>>(e => Task.FromResult(allPullRequests.Where(e.Compile())));
        }

        private async Task InvokeAndVerify(GeneratePullRequestsReport command, Action<PullRequestsReport, Guid> verify)
        {
            PullRequestsReport report = null;
            RepositoryMock.Setup(r => r.CreateAsync(It.IsAny<PullRequestsReport>()))
                .Callback<PullRequestsReport>(r => report = r)
                .ReturnsAsync(true);

            var reportId = await _handler.Handle(command);

            verify(report, reportId);
        }

        private IEnumerable<PullRequestViewModel> GetPullRequests(DateTime start, DateTime end, Guid[] members, Guid[] repositories)
        {
            // 3 Active PRs in range for 1st member
            yield return GetActivePullRequest(members[0], repositories[0], start, end);
            yield return GetActivePullRequest(members[0], repositories[1], start, end);
            yield return GetActivePullRequest(members[0], repositories[0], start, end);

            // 2 Completed PRs in range for 1st member
            yield return GetCompletedPullRequest(members[0], repositories[0], start, end);
            yield return GetCompletedPullRequest(members[0], repositories[1], start, end);

            // 2 Abandoned PRs in range for 1st member
            yield return GetCompletedPullRequest(members[0], repositories[0], start, end, state: PullRequestState.Abandoned);
            yield return GetCompletedPullRequest(members[0], repositories[1], start, end, state: PullRequestState.Abandoned);

            // 1 Completed PR created out of range for 1st member
            yield return GetCompletedPullRequest(members[0], repositories[0], start, end, createdInThePast: true);

            // 1 Active PR created out of range for 1st member
            yield return GetActivePullRequest(members[0], repositories[1], start, end, createdInThePast: true);

            // 2nd member

            // 2 Active PRs in range for 2nd member
            yield return GetActivePullRequest(members[1], repositories[0], start, end);
            yield return GetActivePullRequest(members[1], repositories[1], start, end);

            // 4 Completed PRs in range for 2nd member
            yield return GetCompletedPullRequest(members[1], repositories[0], start, end);
            yield return GetCompletedPullRequest(members[1], repositories[1], start, end);
            yield return GetCompletedPullRequest(members[1], repositories[0], start, end);
            yield return GetCompletedPullRequest(members[1], repositories[1], start, end);

            // 1 Abandoned PRs in range for 2nd member
            yield return GetCompletedPullRequest(members[1], repositories[0], start, end, state: PullRequestState.Abandoned);

            // 2 Completed PR created out of range for 1st member
            yield return GetCompletedPullRequest(members[1], repositories[0], start, end, createdInThePast: true);
            yield return GetCompletedPullRequest(members[1], repositories[1], start, end, createdInThePast: true);

            // 3 Active PR created out of range for 1st member
            yield return GetActivePullRequest(members[1], repositories[1], start, end, createdInThePast: true);
            yield return GetActivePullRequest(members[1], repositories[0], start, end, createdInThePast: true);
            yield return GetActivePullRequest(members[1], repositories[1], start, end, createdInThePast: true);
        }

        private PullRequestViewModel GetActivePullRequest(Guid author, Guid repository, DateTime start, DateTime end, bool createdInThePast = false)
        {
            var pr = GetPullRequest(author, repository, start, end, createdInThePast);
            pr.State = PullRequestState.Active;
            return pr;
        }

        private PullRequestViewModel GetCompletedPullRequest(Guid author, Guid repository, DateTime start, DateTime end, bool createdInThePast = false, PullRequestState state = PullRequestState.Completed)
        {
            var pr = GetPullRequest(author, repository, start, end, createdInThePast);
            pr.State = state;
            pr.Completed = _generator.Next(start, end);
            return pr;
        }

        private PullRequestViewModel GetPullRequest(Guid author, Guid repository, DateTime start, DateTime end, bool createdInThePast = false)
        {
            return new PullRequestViewModel
            {
                Iterations = 1,
                Comments = 2,
                State = PullRequestState.NotSet,
                Completed = null,
                AuthorId = author,
                Repository = repository,
                Created = !createdInThePast ? _generator.Next(start, end) : start.AddDays(-_generator.Next(1, 10))
            };
        }
    }
}
