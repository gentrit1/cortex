﻿using Cortex.Core.Pipelines;
using Cortex.Core.Streams;

namespace Cortex.Core.Abstractions.Pipelines
{
    public interface IPipelineStreamBuilder
    {
        IPipelineStreamBuilder AddStream(string name, IStreamBuilder streamBuilder);
        IPipeline Build();
    }
}
