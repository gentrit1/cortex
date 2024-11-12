﻿using System;
namespace Cortex.Streams.Abstractions
{
    public interface ISourceOperator<TOutput>
    {
        void Start(Action<TOutput> emit);
        void Stop();
    }
}
