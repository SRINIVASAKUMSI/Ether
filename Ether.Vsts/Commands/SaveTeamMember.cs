﻿using Ether.Contracts.Interfaces.CQS;
using Ether.ViewModels;

namespace Ether.Vsts.Commands
{
    public class SaveTeamMember : ICommand
    {
        public TeamMemberViewModel TeamMember { get; set; }
    }
}
