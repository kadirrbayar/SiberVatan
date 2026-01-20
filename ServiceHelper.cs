using Microsoft.Extensions.DependencyInjection;

namespace SiberVatan
{
    public static class ServiceHelper
    {
        public static T GetService<T>(IServiceScope scope) where T : class
        {
            return scope == null ? throw new ArgumentNullException(nameof(scope)) : scope.ServiceProvider.GetRequiredService<T>();
        }
    }
}
