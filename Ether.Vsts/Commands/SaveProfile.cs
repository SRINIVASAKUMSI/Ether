﻿using Ether.Contracts.Interfaces.CQS;
using Ether.ViewModels;

namespace Ether.Vsts.Commands
{
    public class SaveProfile : ICommand
    {
        public VstsProfileViewModel Profile { get; set; }
    }
}
