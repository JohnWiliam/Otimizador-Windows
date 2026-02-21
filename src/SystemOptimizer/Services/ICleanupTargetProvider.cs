using System.Collections.Generic;

namespace SystemOptimizer.Services;

public interface ICleanupTargetProvider
{
    IEnumerable<CleanupTarget> GetTargets();
}
