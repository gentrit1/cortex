﻿using System.Collections.Generic;

namespace Cortex.States.Operators
{
    public interface IStatefulOperator
    {
        IEnumerable<IStateStore> GetStateStores();
    }
}
