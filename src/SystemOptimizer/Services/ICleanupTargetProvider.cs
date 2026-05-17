using System.Collections.Generic;

namespace SystemOptimizer.Services;

public interface ICleanupTargetProvider
{
    string CategoryKey { get; }

    IEnumerable<CleanupTarget> GetTargets();
}
