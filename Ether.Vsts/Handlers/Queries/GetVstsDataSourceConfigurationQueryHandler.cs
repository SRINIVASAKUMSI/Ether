﻿using System.Threading.Tasks;
using Ether.Contracts.Interfaces;
using Ether.Contracts.Interfaces.CQS;
using Ether.ViewModels;
using Ether.Vsts.Dto;
using Ether.Vsts.Queries;

namespace Ether.Vsts.Handlers.Queries
{
    public class GetVstsDataSourceConfigurationQueryHandler : IQueryHandler<GetVstsDataSourceConfigurationQuery, VstsDataSourceViewModel>
    {
        private readonly IRepository _repository;

        public GetVstsDataSourceConfigurationQueryHandler(IRepository repository)
        {
            _repository = repository;
        }

        public async Task<VstsDataSourceViewModel> Handle(GetVstsDataSourceConfigurationQuery input)
        {
            var setting = await _repository.GetSingleAsync<VstsDataSourceSettings>(s => s.Type == "Vsts");
            if (setting == null)
            {
                return null;
            }

            return new VstsDataSourceViewModel
            {
                Id = setting.Id,
                DefaultToken = setting.DefaultToken,
                InstanceName = setting.InstanceName
            };
        }
    }
}
