using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace CleanArchitecture.UnitTests.Fakes;

public static class Caches
{
    public static HybridCache Create()
    {
        var services = new ServiceCollection();
        services.AddHybridCache();

        return services.BuildServiceProvider().GetRequiredService<HybridCache>();
    }
}
