﻿using System.Threading.Tasks;

namespace Ether.Contracts.Interfaces.CQS
{
    public interface ICommandHandler<TCommand, TResult>
    {
        Task<TResult> Handle(TCommand input);
    }
}